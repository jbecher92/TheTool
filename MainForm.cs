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

            // ------------------------------
            // Validate DeploymentFlavor here
            // ------------------------------
            string flavor = (cfg.DeploymentFlavor ?? "").Trim().ToLowerInvariant();
            if (!new[] { "prod", "test", "internal" }.Contains(flavor))
            {
                MessageBox.Show(
                    $"Invalid Config Setting: '{cfg.DeploymentFlavor}'. Must be 'prod', 'test', or 'internal'.\nThe program will now exit.",
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
                        log("Sites requiring manual config review (missing/seeded app\\environments\\config.json):");
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
            string resolvedRoot)
        {
            if (!ValidateZipSelections(confirmedSites, prodZip, eapZip, esubZip, dataAccessZip))
                throw new InvalidOperationException("Validation failed.");

            if (string.IsNullOrWhiteSpace(resolvedRoot))
                throw new InvalidOperationException("resolvedRoot must be provided (Option-1 rule).");

            _sitesNeedingReview.Clear();              // reset roll-up list
            string tag = TodayTag();                  // <-- ALWAYS today's MMDDYYYY

            var sitePlans = new List<(string SiteName, string State, string Client, bool DoProd, bool DoEap, bool DoESub)>();
            var poolsAffected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var externalRootCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (siteName, prod, eap, esub) in confirmedSites)
            {
                if (string.IsNullOrWhiteSpace(siteName) || siteName.Length < 3)
                {
                    log?.Invoke($"Skipping invalid selection: '{siteName}'");
                    continue;
                }

                string state = siteName.Substring(0, 2).ToUpperInvariant();
                string client = siteName.Substring(2);

                if (prod) poolsAffected.Add(siteName);
                if (eap) poolsAffected.Add(state + client + "CaseInfoSearch");
                if (esub) poolsAffected.Add(state + client + "eSubpoena");
                if (eap || esub)
                {
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

            if (isCreate)
            {
                foreach (var plan in sitePlans)
                {
                    if (plan.DoProd)
                    {
                        string prodBase = FileManager.ResolveProdBasePath(resolvedRoot, plan.State, plan.Client);
                        Directory.CreateDirectory(Path.Combine(prodBase, tag));
                    }

                    if (plan.DoEap || plan.DoESub)
                    {
                        string externalRoot = FileManager.ResolveExternalRoot(plan.State, plan.Client, AppConfigManager.Config.DeploymentFlavor ?? "prod", resolvedRoot, externalRootCache);
                        Directory.CreateDirectory(externalRoot);

                        if (plan.DoEap) Directory.CreateDirectory(Path.Combine(externalRoot, "CaseInfoSearch"));
                        if (plan.DoESub) Directory.CreateDirectory(Path.Combine(externalRoot, "eSubpoena"));

                        string daFolder = Directory.Exists(Path.Combine(externalRoot, "PBKDataAccess")) ? "PBKDataAccess" : "DataAccess";
                        Directory.CreateDirectory(Path.Combine(externalRoot, daFolder));
                    }
                }
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
                        // PRE-STATE 
                        string prodBasePath = FileManager.ResolveProdBasePath(resolvedRoot, plan.State, plan.Client);
                        string prevFolder = isCreate ? string.Empty : (FindLatestVersionFolder(prodBasePath) ?? string.Empty);

                        bool prevProdHadConfig = false;
                        if (!string.IsNullOrWhiteSpace(prevFolder))
                            prevProdHadConfig = File.Exists(Path.Combine(prodBasePath, prevFolder, @"app\environments\config.json"));

                        // Resolve external root then check actual existence on disk
                        string externalRootForChecks = FileManager.ResolveExternalRoot(
                            plan.State, plan.Client, AppConfigManager.Config.DeploymentFlavor ?? "prod", resolvedRoot, externalRootCache);

                        bool extRootExistsBefore = Directory.Exists(externalRootForChecks);

                        // Determine whether each external app folder exists before the update
                        bool cisExistsBefore = extRootExistsBefore && Directory.Exists(Path.Combine(externalRootForChecks, "CaseInfoSearch"));
                        bool esubExistsBefore = extRootExistsBefore && Directory.Exists(Path.Combine(externalRootForChecks, "eSubpoena"));

                        // Only look for config if the app actually exists
                        bool prevCisHadConfig = cisExistsBefore &&
                            File.Exists(Path.Combine(externalRootForChecks, "CaseInfoSearch", @"app\environments\config.json"));
                        bool prevEsubHadConfig = esubExistsBefore &&
                            File.Exists(Path.Combine(externalRootForChecks, "eSubpoena", @"app\environments\config.json"));


                        // PRODUCTION
                        if (plan.DoProd)
                        {
                            confirmCtx?.EnterProdPhase(plan.SiteName);
                            //log?.Invoke($"{plan.SiteName}: Updating Production.");

                            await Task.Run(() =>
                            {
                                FileManager.DeployProduction_Update(
                                    plan.State, plan.Client, resolvedRoot,
                                    prevFolder, tag, prodZip, onProgress: log);
                            });

                            confirmCtx?.LeavePhase();

                            string newProdCfg = ProdConfigPath(resolvedRoot, plan.State, plan.Client, tag);
                            bool nowHasProdConfig = File.Exists(newProdCfg);
                            if (!prevProdHadConfig || !nowHasProdConfig)
                                NoteForReview(plan.SiteName, "Production");

                            if (confirmCtx?.ShouldSkipExternalsFor(plan.SiteName) == true)
                            {
                                //log?.Invoke($"Aborting {plan.SiteName} Production. Skipping externals and all remaining clients.");
                                break;
                            }
                        }

                        // EXTERNALS
                        if (plan.DoEap || plan.DoESub)
                        {
                            confirmCtx?.EnterExternalsPhase(plan.SiteName);
                            log?.Invoke($"{plan.SiteName}: Initiating External Updates");

                            await Task.Run(() =>
                            {
                                string? cisZip = plan.DoEap ? eapZip : null;
                                string? esZip = plan.DoESub ? esubZip : null;

                                FileManager.DeployExternal_Update(
                                    resolvedRoot, plan.State, plan.Client, tag,
                                    cisZip, esZip, dataAccessZip, onProgress: log);
                            });

                            confirmCtx?.LeavePhase();

                            string extRoot = FileManager.ResolveExternalRoot(
                                plan.State, plan.Client, AppConfigManager.Config.DeploymentFlavor ?? "prod", resolvedRoot, externalRootCache);

                            // Check what exists AFTER the update
                            bool extRootExistsAfter = Directory.Exists(extRoot);
                            bool cisExistsAfter = extRootExistsAfter && Directory.Exists(Path.Combine(extRoot, "CaseInfoSearch"));
                            bool esuExistsAfter = extRootExistsAfter && Directory.Exists(Path.Combine(extRoot, "eSubpoena"));

                            if (plan.DoEap)
                            {
                                if (cisExistsAfter)
                                {
                                    bool nowHasCisConfig = File.Exists(ExternalConfigPath(extRoot, "CaseInfoSearch"));
                                    if (!prevCisHadConfig || !nowHasCisConfig)
                                        NoteForReview(plan.SiteName, "CaseInfoSearch");
                                }
                                else
                                {
                                    // App folder not present 
                                    log?.Invoke($"{plan.SiteName}[Skip]: CaseInfoSearch not present.");
                                }
                            }

                            if (plan.DoESub)
                            {
                                if (esuExistsAfter)
                                {
                                    bool nowHasEsuConfig = File.Exists(ExternalConfigPath(extRoot, "eSubpoena"));
                                    if (!prevEsubHadConfig || !nowHasEsuConfig)
                                        NoteForReview(plan.SiteName, "eSubpoena");
                                }
                                else
                                {
                                    // App folder not present
                                    log?.Invoke($"{plan.SiteName}[Skip]: eSubpoena not present.");
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

            // Final IIS binding
            // Resolve once per batch 
            string deploymentFlavor = AppConfigManager.Config.DeploymentFlavor ?? "prod";

            foreach (var plan in sitePlans)
            {
                if (plan.DoProd)
                {
                    string prodBasePath = FileManager.ResolveProdBasePath(resolvedRoot, plan.State, plan.Client);
                    IISManager.EnsureAppUnderDefault(
                        plan.State + plan.Client,
                        Path.Combine(prodBasePath, tag),
                        plan.SiteName);
                }
                if (plan.DoEap || plan.DoESub)
                {
                    string externalRoot = FileManager.ResolveExternalRoot(
                        plan.State, plan.Client, deploymentFlavor, resolvedRoot, externalRootCache);

                    if (plan.DoEap)
                    {
                        string eapName = plan.State + plan.Client + "CaseInfoSearch";
                        IISManager.EnsureAppUnderDefault(
                            eapName,
                            Path.Combine(externalRoot, "CaseInfoSearch"),
                            eapName);
                    }
                    if (plan.DoESub)
                    {
                        string esubName = plan.State + plan.Client + "eSubpoena";
                        IISManager.EnsureAppUnderDefault(
                            esubName,
                            Path.Combine(externalRoot, "eSubpoena"),
                            esubName);
                    }
                    // Execution-time check 
                    string daFolder = Directory.Exists(Path.Combine(externalRoot, "PBKDataAccess"))
                                      ? "PBKDataAccess"
                                      : "DataAccess";

                    string daName = plan.State + plan.Client + daFolder;
                    IISManager.EnsureAppUnderDefault(
                        daName,
                        Path.Combine(externalRoot, daFolder),
                        daName);
                }
            }
        }

        // Populate site selector panel with IIS app pools on load
        private void LoadAppPoolsIntoPanel()
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
                if (filtered.Count == 0)
                {
                    MessageBox.Show("No IIS application pools were found on this machine.");
                    return;
                }

                siteSelectorPanel.LoadSites(filtered);
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

        // absolute path to Prod config.json for a given tag
        private static string ProdConfigPath(string resolvedRoot, string state, string client, string tag)
        {
            var prodBase = FileManager.ResolveProdBasePath(resolvedRoot, state, client);
            return Path.Combine(prodBase, tag, @"app\environments\config.json");
        }

        // absolute path to External role config.json
        private static string ExternalConfigPath(string externalRoot, string roleFolderName)
        {
            return Path.Combine(externalRoot, roleFolderName, @"app\environments\config.json");
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
