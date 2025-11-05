using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TheTool
{
    public partial class MainForm : Form
    {
        private readonly AppConfig appConfig;

        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        private readonly List<string> _sitesNeedingReview = new List<string>();

        public MainForm()
        {
            InitializeComponent();

            appConfig = LoadConfigOrDefault();

            LoadAppPoolsIntoPanel();
        }

        private AppConfig LoadConfigOrDefault()
        {
            AppConfig cfg = new AppConfig(); // default

            try
            {
                if (File.Exists(ConfigPath))
                {
                    var txt = File.ReadAllText(ConfigPath);
                    cfg = JsonSerializer.Deserialize<AppConfig>(txt, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new AppConfig();
                }
            }
            catch
            {
                // fallback silently to default config
                cfg = new AppConfig();
            }

            string flavor = (cfg.DeploymentFlavor ?? "").Trim().ToLowerInvariant();
            if (!new[] { "prod", "test", "internal", "az" }.Contains(flavor))
            {
                MessageBox.Show(
                    $"Invalid Config Setting: '{cfg.DeploymentFlavor}'. Must be 'prod', 'test', 'internal', or 'az'.\nThe program will now exit.",
                    "Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            return cfg;
        }

        private void BtnBrowseProd_Click(object sender, EventArgs e)
        {
            string selected = OpenZipFile();
            if (!string.IsNullOrEmpty(selected))
                txtProdPath.Text = selected;
        }

        private void BtnBrowseEap_Click(object sender, EventArgs e)
        {
            string selected = OpenZipFile();
            if (!string.IsNullOrEmpty(selected))
                txtEapPath.Text = selected;
        }

        private void BtnBrowseESub_Click(object sender, EventArgs e)
        {
            string selected = OpenZipFile();
            if (!string.IsNullOrEmpty(selected))
                txtESubPath.Text = selected;
        }

        private void BtnBrowseDataAccess_Click(object sender, EventArgs e)
        {
            string selected = OpenZipFile();
            if (!string.IsNullOrEmpty(selected))
                txtDataAccessPath.Text = selected;
        }

        private void BtnExecute_Click(object sender, EventArgs e)
        {
            var selectedSites = siteSelectorPanel.GetSelectedSites();
            if (!selectedSites.Any())
            {
                MessageBox.Show("No sites selected for update.");
                return;
            }

            var confirmForm = new ConfirmationForm(selectedSites);

            confirmForm.RunConfirmed += async (finalSelections) =>
            {
                string prodZip = txtProdPath.Text;
                string eapZip = txtEapPath.Text;
                string esubZip = txtESubPath.Text;
                string dataAccessZip = txtDataAccessPath.Text;

                Action<string> log = msg =>
                {
                    try
                    {
                        if (confirmForm.IsHandleCreated && confirmForm.InvokeRequired)
                            confirmForm.BeginInvoke(new Action(() => confirmForm.AppendLog(msg)));
                        else
                            confirmForm.AppendLog(msg);
                    }
                    catch { /* ignore */ }
                };

                try
                {
                    string resolvedRoot = NormalizeRoot(
                        ResolveRootPath(appConfig.DeploymentFlavor ?? "prod", appConfig.SitesRootPath ?? string.Empty));

                    await RunDeploymentAsync(finalSelections, prodZip, eapZip, esubZip, dataAccessZip,
                                             isCreate: false, log: log, confirmCtx: confirmForm,
                                             resolvedRoot: resolvedRoot);

                    string completionMsg;
                    if (_sitesNeedingReview.Count > 0)
                    {
                        log("");
                        log("Sites requiring manual config.json review");
                        foreach (var line in _sitesNeedingReview.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            log("  - " + line);

                        completionMsg = confirmForm.IsAbortRequested
                            ? "All updates completed (before abort). Some sites require manual config review."
                            : "All updates completed. Some sites require manual config review.";
                    }
                    else
                    {
                        completionMsg = confirmForm.IsAbortRequested
                            ? "All updates completed (before abort)."
                            : "All updates completed.";
                    }

                    confirmForm.MarkComplete(completionMsg);
                }
                catch (Exception ex)
                {
                    confirmForm.AppendLog("ERROR: " + ex.Message);
                    confirmForm.MarkComplete("Failed.");
                }
            };

            confirmForm.Show(this);
        }

        private void BtnSiteCreator_Click_1(object sender, EventArgs e)
        {
            using var creatorForm = new SiteCreatorForm(appConfig);
            if (creatorForm.ShowDialog() != DialogResult.OK) return;

            string state = (creatorForm.State ?? "").Trim().ToUpperInvariant();
            string client = (creatorForm.Client ?? "").Trim();

            bool makeProd = creatorForm.CreateProd;
            bool makeEAP = creatorForm.CreateEAP;
            bool makeESub = creatorForm.CreateESub;

            if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(client))
            {
                MessageBox.Show("State and Client are required.", "Validation Error");
                return;
            }

            if (string.IsNullOrWhiteSpace(appConfig?.SitesRootPath))
                throw new InvalidOperationException("SitesRootPath is not defined in config.json");
            if (string.IsNullOrWhiteSpace(appConfig?.DeploymentFlavor))
                throw new InvalidOperationException("DeploymentFlavor is not defined in config.json");

            string resolvedRoot = NormalizeRoot(
                ResolveRootPath(appConfig.DeploymentFlavor, appConfig.SitesRootPath));

            var confirmedSites = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>
            {
                (state + client, makeProd, makeEAP, makeESub)
            };

            string prodZip = txtProdPath.Text;
            string eapZip = txtEapPath.Text;
            string esubZip = txtESubPath.Text;
            string dataAccessZip = txtDataAccessPath.Text;

            using var confirm = new SiteCreationConfirmationForm(
                resolvedRoot: resolvedRoot,
                state: state,
                client: client,
                createProd: makeProd,
                createEAP: makeEAP,
                createESub: makeESub);

            confirm.CreateConfirmed += async () =>
            {
                try
                {
                    await RunDeploymentAsync(
                        confirmedSites,
                        prodZip, eapZip, esubZip, dataAccessZip,
                        isCreate: true,
                        log: null,
                        confirmCtx: null,
                        resolvedRoot: resolvedRoot);

                    confirm.MarkComplete("Site creation complete.");
                }
                catch (Exception ex)
                {
                    confirm.MarkComplete("Failed: " + ex.Message);
                }
                finally
                {
                    RefreshIisListQuietly(state + client);
                }
            };

            confirm.ShowDialog(this);
        }

        private async Task RunDeploymentAsync(
    List<(string SiteName, bool Prod, bool EAP, bool eSub)> confirmedSites,
    string prodZip,
    string eapZip,
    string esubZip,
    string dataAccessZip,
    bool isCreate,
    Action<string>? log,
    ConfirmationForm? confirmCtx,
    string resolvedRoot) // global root from config; IIS bases are discovered per-role
        {
            if (!ValidateZipSelections(confirmedSites, prodZip, eapZip, esubZip, dataAccessZip))
                throw new InvalidOperationException("Validation failed.");

            _sitesNeedingReview.Clear();
            string tag = TodayTag();

            var sitePlans = new List<(string SiteName, string State, string Client, bool DoProd, bool DoEap, bool DoESub)>();
            var poolsAffected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (siteName, prod, eap, esub) in confirmedSites)
            {
                if (string.IsNullOrWhiteSpace(siteName) || siteName.Length < 3)
                {
                    log?.Invoke($"Skipping invalid selection: '{siteName}'");
                    continue;
                }

                string state = siteName.Substring(0, 2).ToUpperInvariant();
                string client = siteName.Substring(2);

                // ONLY add pools for roles actually selected — do not touch externals unless selected
                if (prod) poolsAffected.Add(siteName);
                if (eap)
                {
                    poolsAffected.Add(state + client + "CaseInfoSearch");
                    poolsAffected.Add(state + client + "DataAccess");
                    poolsAffected.Add(state + client + "PBKDataAccess");
                }
                if (esub)
                {
                    poolsAffected.Add(state + client + "eSubpoena");
                    poolsAffected.Add(state + client + "DataAccess");
                    poolsAffected.Add(state + client + "PBKDataAccess");
                }

                sitePlans.Add((siteName, state, client, prod, eap, esub));
            }

            if (sitePlans.Count == 0)
            {
                log?.Invoke("Nothing to do.");
                return;
            }

            static string NormalizeIisBase(string path)
            {
                string full = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string last = Path.GetFileName(full);
                bool IsTag(string s) => !string.IsNullOrEmpty(s) && s.Length == 8 && s.All(char.IsDigit);
                return IsTag(last) ? Path.GetDirectoryName(full)! : full;
            }

            Func<Task> work = async () =>
            {
                foreach (var plan in sitePlans)
                {
                    if (confirmCtx?.IsAbortRequested == true)
                    {
                        log?.Invoke("Abort requested. Skipping remaining sites before starting next client.");
                        break;
                    }

                    try
                    {
                        // ======================
                        // PRODUCTION (if selected)
                        // ======================
                        string? prodBaseFromIis = null;
                        if (plan.DoProd)
                        {
                            confirmCtx?.EnterProdPhase(plan.SiteName);

                            if (IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.SiteName, out var phys))
                            {
                                prodBaseFromIis = NormalizeIisBase(phys);
                            }
                            else
                            {
                                log?.Invoke($"{plan.SiteName}[IIS][FATAL]: Could not resolve IIS physicalPath for production pool. Skipping.");
                                confirmCtx?.LeavePhase();
                                continue;
                            }

                            string prevFolder = isCreate ? string.Empty : (FindLatestVersionFolder(prodBaseFromIis) ?? string.Empty);

                            bool prevProdHadConfig = (!string.IsNullOrWhiteSpace(prevFolder)) &&
                                File.Exists(Path.Combine(prodBaseFromIis!, prevFolder, @"app\environments\config.json"));

                            await Task.Run(() =>
                            {
                                FileManager.DeployProduction_Update(
                                    plan.State, plan.Client,
                                    prodBaseFromIis!,   // Option-1: pass IIS base
                                    prevFolder, tag, prodZip, onProgress: log);
                            });

                            confirmCtx?.LeavePhase();

                            string newProdCfg = Path.Combine(prodBaseFromIis!, tag, @"app\environments\config.json");
                            bool nowHasProdConfig = File.Exists(newProdCfg);
                            if (!nowHasProdConfig)
                                NoteForReview(plan.SiteName, "Production");

                            if (confirmCtx?.ShouldSkipExternalsFor(plan.SiteName) == true)
                                break;
                        }

                        // ======================
                        // EXTERNALS (only if selected)
                        // ======================
                        if (plan.DoEap || plan.DoESub)
                        {
                            confirmCtx?.EnterExternalsPhase(plan.SiteName);
                            log?.Invoke($"{plan.SiteName}: Initiating External Updates");

                            string? externalRootFromIis = null;
                            bool got = false;

                            if (!got && plan.DoEap && IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.State + plan.Client + "CaseInfoSearch", out var cisPath))
                            {
                                string cisBase = NormalizeIisBase(cisPath);
                                externalRootFromIis = Path.GetDirectoryName(cisBase);
                                if (!string.IsNullOrEmpty(externalRootFromIis))
                                {
                                    //log?.Invoke($"{plan.SiteName}CaseInfoSearch[IIS]: Base resolved → {cisBase}");
                                    got = true;
                                }
                            }
                            if (!got && plan.DoESub && IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.State + plan.Client + "eSubpoena", out var esuPath))
                            {
                                string esuBase = NormalizeIisBase(esuPath);
                                externalRootFromIis = Path.GetDirectoryName(esuBase);
                                if (!string.IsNullOrEmpty(externalRootFromIis))
                                {
                                    //log?.Invoke($"{plan.SiteName}eSubpoena[IIS]: Base resolved → {esuBase}");
                                    got = true;
                                }
                            }
                            if (!got && IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.State + plan.Client + "DataAccess", out var daPath))
                            {
                                string daBase = NormalizeIisBase(daPath);
                                externalRootFromIis = Path.GetDirectoryName(daBase);
                                if (!string.IsNullOrEmpty(externalRootFromIis))
                                {
                                    //log?.Invoke($"{plan.SiteName}DataAccess[IIS]: Base resolved → {daBase}");
                                    got = true;
                                }
                            }
                            if (!got && IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.State + plan.Client + "PBKDataAccess", out var pdaPath))
                            {
                                string pdaBase = NormalizeIisBase(pdaPath);
                                externalRootFromIis = Path.GetDirectoryName(pdaBase);
                                if (!string.IsNullOrEmpty(externalRootFromIis))
                                {
                                    //log?.Invoke($"{plan.SiteName}PBKDataAccess[IIS]: Base resolved → {pdaBase}");
                                    got = true;
                                }
                            }

                            if (!got || string.IsNullOrWhiteSpace(externalRootFromIis))
                            {
                                log?.Invoke($"{plan.SiteName}[IIS][Skip]: Could not resolve external root via IIS. Skipping externals.");
                                confirmCtx?.LeavePhase();
                            }
                            else
                            {
                                string? cisZip = plan.DoEap ? eapZip : null;
                                string? esuZip = plan.DoESub ? esubZip : null;

                                await Task.Run(() =>
                                {
                                    FileManager.DeployExternal_Update(
                                        externalRootFromIis!, // Option-1: pass IIS external root
                                        plan.State, plan.Client, tag,
                                        cisZip, esuZip, dataAccessZip, onProgress: log);
                                });

                                confirmCtx?.LeavePhase();

                                string cisRoot = Path.Combine(externalRootFromIis!, "CaseInfoSearch");
                                string esuRoot = Path.Combine(externalRootFromIis!, "eSubpoena");

                                if (plan.DoEap && Directory.Exists(cisRoot))
                                {
                                    bool hasCfg = File.Exists(Path.Combine(cisRoot, @"app\environments\config.json"));
                                    if (!hasCfg) NoteForReview(plan.SiteName, "CaseInfoSearch");
                                }
                                if (plan.DoESub && Directory.Exists(esuRoot))
                                {
                                    bool hasCfg = File.Exists(Path.Combine(esuRoot, @"app\environments\config.json"));
                                    if (!hasCfg) NoteForReview(plan.SiteName, "eSubpoena");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"Error deploying {plan.SiteName}: {ex.Message}");
                    }
                }

                if (!isCreate)
                {
                    var tempTargets = poolsAffected
                        .Where(name => Directory.Exists(
                            Path.Combine(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files", name)))
                        .ToArray();

                    if (tempTargets.Length > 0)
                        await TempCleaner.ClearForAppPoolsAsync(tempTargets, log, maxDegreeOfParallelism: 4);
                }
            };

            if (isCreate)
                await work();
            else
                await IISManager.RunWithPoolsStoppedAsync(poolsAffected.ToArray(), work, log);

            // Final IIS binding: use the same 'tag' computed above to avoid midnight rollover issues.
            foreach (var plan in sitePlans)
            {
                if (plan.DoProd)
                {
                    if (IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.SiteName, out var phys))
                    {
                        string prodBase = NormalizeIisBase(phys);
                        IISManager.EnsureAppUnderDefault(
                            plan.State + plan.Client,
                            Path.Combine(prodBase, tag),
                            plan.SiteName);
                    }
                }
                if (plan.DoEap || plan.DoESub)
                {
                    string? externalRootFromIis = null;
                    if (IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.State + plan.Client + "CaseInfoSearch", out var cisPath))
                        externalRootFromIis = Path.GetDirectoryName(NormalizeIisBase(cisPath));
                    else if (IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.State + plan.Client + "eSubpoena", out var esuPath))
                        externalRootFromIis = Path.GetDirectoryName(NormalizeIisBase(esuPath));
                    else if (IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.State + plan.Client + "DataAccess", out var daPath))
                        externalRootFromIis = Path.GetDirectoryName(NormalizeIisBase(daPath));
                    else if (IISManager.IisReadHelpers.TryGetPhysicalPathForPool(plan.State + plan.Client + "PBKDataAccess", out var pdaPath))
                        externalRootFromIis = Path.GetDirectoryName(NormalizeIisBase(pdaPath));

                    if (!string.IsNullOrWhiteSpace(externalRootFromIis))
                    {
                        if (plan.DoEap)
                        {
                            string eapName = plan.State + plan.Client + "CaseInfoSearch";
                            IISManager.EnsureAppUnderDefault(
                                eapName,
                                Path.Combine(externalRootFromIis!, "CaseInfoSearch"),
                                eapName);
                        }
                        if (plan.DoESub)
                        {
                            string esubName = plan.State + plan.Client + "eSubpoena";
                            IISManager.EnsureAppUnderDefault(
                                esubName,
                                Path.Combine(externalRootFromIis!, "eSubpoena"),
                                esubName);
                        }

                        string daFolder = Directory.Exists(Path.Combine(externalRootFromIis!, "PBKDataAccess")) ? "PBKDataAccess" : "DataAccess";
                        string daName = plan.State + plan.Client + daFolder;
                        IISManager.EnsureAppUnderDefault(
                            daName,
                            Path.Combine(externalRootFromIis!, daFolder),
                            daName);
                    }
                }
            }
        }



        // Populate site selector panel with IIS app pools on load
        // Populate site selector panel with IIS app pools on load
        private void LoadAppPoolsIntoPanel()
        {
            try
            {
                // Get all app pools (prod + externals)
                var allPools = IISManager.GetIISAppPools();

                // Filter to only "main" pools for display (no externals)
                var filtered = allPools
                    .Where(p =>
                        !p.EndsWith("CaseInfoSearch", StringComparison.OrdinalIgnoreCase) &&
                        !p.EndsWith("eSubpoena", StringComparison.OrdinalIgnoreCase) &&
                        !p.EndsWith("DataAccess", StringComparison.OrdinalIgnoreCase) &&
                        !p.EndsWith("PBKDataAccess", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filtered.Count == 0)
                {
                    MessageBox.Show("No IIS application pools were found on this machine.");
                    return;
                }

                // Build role availability map:
                //   key = base site (e.g., "MOClient")
                //   value = (HasCIS, HasESub, HasDA)
                var availability = new Dictionary<string, (bool HasCIS, bool HasESub, bool HasDA)>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var pool in allPools)
                {
                    string key = pool;
                    bool hasCis = false;
                    bool hasESub = false;
                    bool hasDa = false;

                    if (pool.EndsWith("CaseInfoSearch", StringComparison.OrdinalIgnoreCase))
                    {
                        key = pool.Substring(0, pool.Length - "CaseInfoSearch".Length);
                        hasCis = true;
                    }
                    else if (pool.EndsWith("eSubpoena", StringComparison.OrdinalIgnoreCase))
                    {
                        key = pool.Substring(0, pool.Length - "eSubpoena".Length);
                        hasESub = true;
                    }
                    else if (pool.EndsWith("PBKDataAccess", StringComparison.OrdinalIgnoreCase))
                    {
                        key = pool.Substring(0, pool.Length - "PBKDataAccess".Length);
                        hasDa = true;
                    }
                    else if (pool.EndsWith("DataAccess", StringComparison.OrdinalIgnoreCase))
                    {
                        key = pool.Substring(0, pool.Length - "DataAccess".Length);
                        hasDa = true;
                    }
                    else
                    {
                        // Ensure the base pool itself has an entry, even if no externals exist
                        if (!availability.ContainsKey(key))
                            availability[key] = (HasCIS: false, HasESub: false, HasDA: false);
                        continue;
                    }

                    if (!availability.TryGetValue(key, out var existing))
                        existing = (HasCIS: false, HasESub: false, HasDA: false);

                    availability[key] = (
                        HasCIS: existing.HasCIS || hasCis,
                        HasESub: existing.HasESub || hasESub,
                        HasDA: existing.HasDA || hasDa
                    );
                }

                // Load visible sites into the grid
                siteSelectorPanel.LoadSites(filtered);

                // Apply role availability so EAP/eSub checkboxes are disabled where app pools are missing
                siteSelectorPanel.ApplyRoleAvailability(availability);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Access denied reading IIS configuration. Try running TheTool as Administrator.",
                    "IIS Access",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (DllNotFoundException)
            {
                MessageBox.Show(
                    "Microsoft.Web.Administration not found. Ensure IIS and 'IIS Management Scripts and Tools' are installed.",
                    "IIS API Missing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load IIS app pools:\n" + ex.Message,
                    "IIS Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        private static string NormalizeRoot(string root)
        {
            if (root.Length == 2 && root[1] == ':')
                root += Path.DirectorySeparatorChar;
            return Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        }

        private string ResolveRootPath(string? deploymentFlavor, string prodRoot)
        {
            deploymentFlavor = (deploymentFlavor ?? "").Trim().ToLowerInvariant();

            return deploymentFlavor switch
            {
                "internal" => appConfig?.InternalPath?.Trim() ?? throw new InvalidOperationException("InternalPath not configured."),
                "test" => appConfig?.ValidatePath?.Trim() ?? throw new InvalidOperationException("ValidatePath not configured."),
                "az" => prodRoot, //uses SitesRootPath (prod)
                _ => prodRoot
            };
        }

        // ===== File Helpers =====

        private string OpenZipFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                // UI accepts .zip and .7z; FileManager currently deploys .zip and supports .7z extraction.
                dialog.Filter = "Archive files (*.zip;*.7z)|*.zip;*.7z";
                dialog.Title = "Select a Build Archive (.zip or .7z)";
                dialog.Multiselect = false;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : string.Empty;
            }
        }

        private bool ValidateZipSelections(
            List<(string SiteName, bool Prod, bool EAP, bool eSub)> confirmedSites,
            string prodZip,
            string eapZip,
            string esubZip,
            string dataAccessZip)
        {
            bool anyProd = confirmedSites.Any(s => s.Prod);
            bool anyEAP = confirmedSites.Any(s => s.EAP);
            bool anyESub = confirmedSites.Any(s => s.eSub);

            // Mandatory presence checks
            if (anyProd && !IsZipAvailable(prodZip))
            {
                MessageBox.Show("A Production build is required based on your final selections.");
                return false;
            }
            if (anyEAP && !IsZipAvailable(eapZip))
            {
                MessageBox.Show("A CaseInfoSearch build is required based on your final selections.");
                return false;
            }
            if (anyESub && !IsZipAvailable(esubZip))
            {
                MessageBox.Show("An eSubpoena build is required based on your final selections.");
                return false;
            }
            if ((anyEAP || anyESub) && !IsZipAvailable(dataAccessZip))
            {
                MessageBox.Show("A DataAccess build is required for EAP or eSub based on your final selections.");
                return false;
            }

            // Filename convention checks 
            var allZips = new[]
            {
                (prodZip, "production"),
                (eapZip, "caseinfosearch"),
                (esubZip, "esubpoena"),
                (dataAccessZip, "dataaccess")
            };

            foreach (var (zip, type) in allZips)
            {
                if (!string.IsNullOrWhiteSpace(zip) && !FileManager.IsValidBuildName(zip, type))
                {
                    MessageBox.Show($"Invalid build archive name for {type}.", "Filename Check");
                    return false;
                }
            }

            return true;
        }

        private static bool IsZipAvailable(string path) { return !string.IsNullOrWhiteSpace(path) && File.Exists(path); }
        private static string TodayTag() => DateTime.Now.ToString("MMddyyyy");

        private static string FindLatestVersionFolder(string basePath)
        {
            if (!Directory.Exists(basePath)) return "";
            var latest = Directory.GetDirectories(basePath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name) && name.Length == 8 && name.All(char.IsDigit))
                .OrderByDescending(n => n)
                .FirstOrDefault();
            return latest ?? "";
        }

        private void RefreshIisListQuietly(string? highlightSite = null)
        {
            try
            {
                var appPools = IISManager.GetIISAppPools();
                var filtered = appPools
                    .Where(p =>
                        !p.EndsWith("CaseInfoSearch", StringComparison.OrdinalIgnoreCase) &&
                        !p.EndsWith("eSubpoena", StringComparison.OrdinalIgnoreCase) &&
                        !p.EndsWith("DataAccess", StringComparison.OrdinalIgnoreCase) &&
                        !p.EndsWith("PBKDataAccess", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                siteSelectorPanel.LoadSites(filtered);

            }
            catch
            {
                //refresh silently
            }
        }

       

        // note site/role for manual review 
        private void NoteForReview(string siteName, string roleLabel)
        {
            if (string.Equals(roleLabel, "DataAccess", StringComparison.OrdinalIgnoreCase))
                return; //never surface DA unless the update actually failed

            var line = $"{siteName} — {roleLabel}";
            if (!_sitesNeedingReview.Contains(line, StringComparer.OrdinalIgnoreCase))
                _sitesNeedingReview.Add(line);
        }
    }
}