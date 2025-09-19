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
        // Config model used by MainForm only (reads config.json)
        private class AppConfig
        {
            public string DeploymentFlavor { get; set; } = "prod";
            public string SitesRootPath { get; set; } = @"F:\inetpub\wwwroot";
            public string SitesDrive { get; set; } = "F:";
            public string ValidatePath { get; set; } = @"G:\inetpub\wwwroot";
            public string ValidateDrive { get; set; } = "G:";
            public string InternalPath { get; set; } = @"C:\inetpub\wwwroot";
            public string InternalDrive { get; set; } = "C:";
        }

        private readonly AppConfig appConfig;

        // Where to find config.json relative to exe; change if you store it somewhere else
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

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
                    $"Invalid DeploymentFlavor: '{cfg.DeploymentFlavor}'. Must be 'prod', 'test', or 'internal'.\nThe program will now exit.",
                    "Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1); // terminate immediately
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

        private async void BtnExecute_Click(object sender, EventArgs e)
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

                Action<string> log = line => confirmForm.AppendLog(line);

                try
                {
                    // Dispatch to the correct function based on config.DeploymentFlavor
                    var flavor = (appConfig.DeploymentFlavor ?? "prod").Trim().ToLowerInvariant();

                    switch (flavor)
                    {
                        case "internal":
                            await deploy_Internal(finalSelections, prodZip, eapZip, esubZip, dataAccessZip,
                                                  isCreate: false, log: log, confirmCtx: confirmForm,
                                                  internalRoot: appConfig.InternalPath);
                            break;

                        case "test":
                            await deploy_Test(finalSelections, prodZip, eapZip, esubZip, dataAccessZip,
                                              isCreate: false, log: log, confirmCtx: confirmForm,
                                              validateRoot: appConfig.ValidatePath);
                            break;

                        case "prod":
                        default:
                            // Production uses the canonical RunDeploymentAsync (unchanged)
                            await RunDeploymentAsync(finalSelections, prodZip, eapZip, esubZip, dataAccessZip,
                                                     isCreate: false, log: log, confirmCtx: confirmForm);
                            break;
                    }

                    // If abort was requested, we exited at a safe checkpoint.
                    confirmForm.MarkComplete(confirmForm.IsAbortRequested
                        ? "All updates completed (before the abort)."
                        : "All updates completed.");
                }
                catch (Exception ex)
                {
                    confirmForm.AppendLog("ERROR: " + ex.Message);
                    confirmForm.MarkComplete("Failed.");
                }
            };

            confirmForm.Show(this);
        }

        private async void btnSiteCreator_Click_1(object sender, EventArgs e)
        {
            using var creatorForm = new SiteCreatorForm();
            if (creatorForm.ShowDialog() != DialogResult.OK) return;

            string state = (creatorForm.State ?? "").Trim().ToUpperInvariant();
            string client = (creatorForm.Client ?? "").Trim();

            bool makeProd = creatorForm.CreateProd;
            bool makeEAP = creatorForm.CreateEAP;
            bool makeESub = creatorForm.CreateESub;
            bool makeDA = (makeEAP || makeESub);   // DA implied by externals

            if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(client))
            {
                MessageBox.Show("State and Client are required.", "Validation Error");
                return;
            }

            //Make sure site doesn't already exist
            var confirmedSites = new List<(string SiteName, bool Prod, bool EAP, bool eSub)> { (state + client, makeProd, makeEAP, makeESub) };

            // Read zips at confirm time
            string prodZip = txtProdPath.Text;
            string eapZip = txtEapPath.Text;
            string esubZip = txtESubPath.Text;
            string dataAccessZip = txtDataAccessPath.Text;

            // Build preview
            string siteName = state + client;
            string tag = TodayTag();

            // Use production SitesRootPath for previewing external path (consistent with deployment)
            string prodRoot = (appConfig?.SitesRootPath ?? @"F:\inetpub\wwwroot");
            string prodNew = Path.Combine(FileManager.ResolveProdBasePath(state, client), tag);
            string externalRoot = ResolveExternalRootPreviewUsingRoot(state, client, prodRoot);

            var preview = new List<(string Label, string Path)>();
            if (makeProd)
                preview.Add(($"{siteName}", prodNew));
            if (makeEAP)
                preview.Add(($"{siteName}CaseInfoSearch", Path.Combine(externalRoot, "CaseInfoSearch")));
            if (makeESub)
                preview.Add(($"{siteName}eSubpoena", Path.Combine(externalRoot, "eSubpoena")));

            // ⬇️ Always show DataAccess when implied
            if (makeDA)
            {
                string daFolder = Directory.Exists(Path.Combine(externalRoot, "PBKDataAccess"))
                    ? "PBKDataAccess" : "DataAccess";
                preview.Add(($"{siteName}{daFolder}", Path.Combine(externalRoot, daFolder)));
            }

            using var confirm = new SiteCreationConfirmationForm(preview);
            if (confirm.ShowDialog() != DialogResult.OK) return;

            try
            {
                // For creation, we must also respect the configured flavor.
                // If config is non-prod (test/internal) we route using the wrappers which will *disable externals*.
                var flavor = (appConfig.DeploymentFlavor ?? "prod").Trim().ToLowerInvariant();

                switch (flavor)
                {
                    case "internal":
                        await deploy_Internal(confirmedSites, prodZip, eapZip, esubZip, dataAccessZip,
                                              isCreate: true, log: null, confirmCtx: null, internalRoot: appConfig.InternalPath);
                        break;

                    case "test":
                        await deploy_Test(confirmedSites, prodZip, eapZip, esubZip, dataAccessZip,
                                          isCreate: true, log: null, confirmCtx: null, validateRoot: appConfig.ValidatePath);
                        break;

                    case "prod":
                    default:
                        await RunDeploymentAsync(confirmedSites, prodZip, eapZip, esubZip, dataAccessZip,
                                                 isCreate: true, log: null, confirmCtx: null);
                        break;
                }

                MessageBox.Show("Site creation complete.", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Create failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Refresh IIS list to show the new site(s)
                RefreshIisListQuietly(state + client);
            }
        }

        // -----------------------------
        // Non-prod wrappers for RunDeploymentAsync
        // -----------------------------
        private async Task deploy_Test(
            List<(string SiteName, bool Prod, bool EAP, bool eSub)> confirmedSites,
            string prodZip,
            string eapZip,
            string esubZip,
            string dataAccessZip,
            bool isCreate,
            Action<string>? log,
            ConfirmationForm? confirmCtx,
            string validateRoot)   // <-- pass this in (e.g. appConfig.ValidatePath)
        {
            if (confirmedSites == null) throw new ArgumentNullException(nameof(confirmedSites));

            // 1) sanitize: never run externals for test/validate
            var sanitized = confirmedSites
                .Select(s => (SiteName: s.SiteName, Prod: s.Prod, EAP: false, eSub: false))
                .ToList();

            // 2) set ProdBasePathOverride to route to ValidatePath
            var originalOverride = FileManager.ProdBasePathOverride;
            try
            {
                var validateRootTrimmed = (validateRoot ?? "").Trim();
                if (string.IsNullOrWhiteSpace(validateRootTrimmed))
                    throw new InvalidOperationException("ValidatePath not provided to deploy_Test.");

                FileManager.ProdBasePathOverride = (state, client) =>
                {
                    var s = (state ?? "").Trim().ToUpperInvariant();
                    var c = (client ?? "").Trim();
                    if (s.Length == 0) throw new ArgumentException("state required", nameof(state));
                    if (c.Length == 0) throw new ArgumentException("client required", nameof(client));
                    // flattened: {ValidatePath}\{STATE}{CLIENT}
                    return Path.Combine(validateRootTrimmed, s + c);
                };

                // 3) call central deployment (it will use ResolveProdBasePath which now routes to ValidatePath)
                await RunDeploymentAsync(sanitized, prodZip, eapZip, esubZip, dataAccessZip, isCreate, log, confirmCtx);
            }
            finally
            {
                // restore original behavior
                FileManager.ProdBasePathOverride = originalOverride;
            }
        }

        private async Task deploy_Internal(
            List<(string SiteName, bool Prod, bool EAP, bool eSub)> confirmedSites,
            string prodZip,
            string eapZip,
            string esubZip,
            string dataAccessZip,
            bool isCreate,
            Action<string>? log,
            ConfirmationForm? confirmCtx,
            string internalRoot)   // <-- pass this in (e.g. appConfig.InternalPath)
        {
            if (confirmedSites == null) throw new ArgumentNullException(nameof(confirmedSites));

            // sanitize: disable externals
            var sanitized = confirmedSites
                .Select(s => (SiteName: s.SiteName, Prod: s.Prod, EAP: false, eSub: false))
                .ToList();

            var originalOverride = FileManager.ProdBasePathOverride;
            try
            {
                var internalRootTrimmed = (internalRoot ?? "").Trim();
                if (string.IsNullOrWhiteSpace(internalRootTrimmed))
                    throw new InvalidOperationException("InternalPath not provided to deploy_Internal.");

                FileManager.ProdBasePathOverride = (state, client) =>
                {
                    var s = (state ?? "").Trim().ToUpperInvariant();
                    var c = (client ?? "").Trim();
                    if (s.Length == 0) throw new ArgumentException("state required", nameof(state));
                    if (c.Length == 0) throw new ArgumentException("client required", nameof(client));
                    // flattened: {InternalPath}\{STATE}{CLIENT}
                    return Path.Combine(internalRootTrimmed, s + c);
                };

                await RunDeploymentAsync(sanitized, prodZip, eapZip, esubZip, dataAccessZip, isCreate, log, confirmCtx);
            }
            finally
            {
                FileManager.ProdBasePathOverride = originalOverride;
            }
        }


        private async Task RunDeploymentAsync(
            List<(string SiteName, bool Prod, bool EAP, bool eSub)> confirmedSites,
            string prodZip,
            string eapZip,
            string esubZip,
            string dataAccessZip,
            bool isCreate,
            Action<string>? log,
            ConfirmationForm? confirmCtx = null) // ⬅️ optional: creation path stays unchanged
        {
            // Central validation (enforces DA when EAP/eSub selected)
            if (!ValidateZipSelections(confirmedSites, prodZip, eapZip, esubZip, dataAccessZip))
                throw new InvalidOperationException("Validation failed.");

            string tag = TodayTag();

            // Build plans + affected pools
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

            // ---- bootstrap directories on CREATE only ----
            if (isCreate)
            {
                foreach (var plan in sitePlans)
                {
                    if (plan.DoProd)
                    {
                        string prodBase = FileManager.ResolveProdBasePath(plan.State, plan.Client);
                        Directory.CreateDirectory(Path.Combine(prodBase, tag));
                    }

                    if (plan.DoEap || plan.DoESub)
                    {
                        string extRoot = ResolveExternalRoot(plan.State, plan.Client);
                        Directory.CreateDirectory(extRoot);

                        if (plan.DoEap) Directory.CreateDirectory(Path.Combine(extRoot, "CaseInfoSearch"));
                        if (plan.DoESub) Directory.CreateDirectory(Path.Combine(extRoot, "eSubpoena"));

                        string daFolder = Directory.Exists(Path.Combine(extRoot, "PBKDataAccess")) ? "PBKDataAccess" : "DataAccess";
                        Directory.CreateDirectory(Path.Combine(extRoot, daFolder));
                    }
                }
            }
            // ------------------------------------------------

            // The file work to perform (wrapped by pool stop/start for Update)
            Func<Task> work = async () =>
            {
                bool exitNow = false;

                for (int i = 0; i < sitePlans.Count; i++)
                {
                    var plan = sitePlans[i];

                    // If Abort was pressed between clients, honor it now.
                    if (confirmCtx?.IsAbortRequested == true)
                    {
                        log?.Invoke("Abort requested. Skipping remaining sites before starting next client.");
                        break;
                    }

                    // -------------------- PROD PHASE --------------------
                    if (plan.DoProd)
                    {
                        confirmCtx?.EnterProdPhase(plan.SiteName);
                        log?.Invoke($"{plan.SiteName}: Updating Production.");

                        await Task.Run(() =>
                        {
                            string basePath = FileManager.ResolveProdBasePath(plan.State, plan.Client);
                            string prevFolder = isCreate ? string.Empty : (FindLatestVersionFolder(basePath) ?? string.Empty);

                            FileManager.DeployProduction_Update(
                                plan.State, plan.Client, prevFolder, tag, prodZip, onProgress: null);
                        });

                        confirmCtx?.LeavePhase();

                        // Abort during Prod: finish Prod (done), then skip this client's externals and the rest.
                        if (confirmCtx?.ShouldSkipExternalsFor(plan.SiteName) == true)
                        {
                            log?.Invoke($"Aborting {plan.SiteName} Production. Skipping externals and all remaining clients.");
                            exitNow = true;
                            break;
                        }
                    }

                    // ----------------- EXTERNALS PHASE -----------------
                    if (plan.DoEap || plan.DoESub)
                    {
                        confirmCtx?.EnterExternalsPhase(plan.SiteName);
                        log?.Invoke($"{plan.SiteName}: Initiating External Updates");

                        await Task.Run(() =>
                        {
                            string externalRoot = ResolveExternalRoot(plan.State, plan.Client);
                            string? cisZip = plan.DoEap ? eapZip : null;
                            string? esZip = plan.DoESub ? esubZip : null;

                            FileManager.DeployExternal_Update(
                                plan.State, plan.Client, externalRoot, tag,
                                cisZip, esZip, dataAccessZip,
                                onProgress: null);
                        });

                        confirmCtx?.LeavePhase();

                        // Abort during Externals: finish externals (done), then stop processing further clients.
                        if (confirmCtx?.ShouldStopAfterExternals(plan.SiteName) == true)
                        {
                            log?.Invoke($"Aborting {plan.SiteName} Externals. Skipping remaining clients.");
                            exitNow = true;
                            break;
                        }
                    }
                }

                // Update-only: clear ASP.NET temp after file ops (kept as-is)
                if (!isCreate)
                {
                    var tempTargets = poolsAffected
                        .Where(name => Directory.Exists(
                            Path.Combine(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files", name)))
                        .ToArray();

                    if (tempTargets.Length > 0)
                        await TempCleaner.ClearForAppPoolsAsync(tempTargets, line => log?.Invoke(line), maxDegreeOfParallelism: 4);
                }
            };

            // Update: stop/start only pools we touch; Create: just run
            if (isCreate)
                await work();
            else
                await IISManager.RunWithPoolsStoppedAsync(poolsAffected.ToArray(), work, log);

            // IIS mappings — same for both
            foreach (var plan in sitePlans)
            {
                if (plan.DoProd)
                {
                    string basePath = FileManager.ResolveProdBasePath(plan.State, plan.Client);
                    IISManager.EnsureAppUnderDefault(
                        plan.State + plan.Client,
                        Path.Combine(basePath, tag),
                        plan.SiteName);
                }

                if (plan.DoEap)
                {
                    string externalRoot = ResolveExternalRoot(plan.State, plan.Client);
                    IISManager.EnsureAppUnderDefault(
                        plan.State + plan.Client + "CaseInfoSearch",
                        Path.Combine(externalRoot, "CaseInfoSearch"),
                        plan.State + plan.Client + "CaseInfoSearch");
                }

                if (plan.DoESub)
                {
                    string externalRoot = ResolveExternalRoot(plan.State, plan.Client);
                    IISManager.EnsureAppUnderDefault(
                        plan.State + plan.Client + "eSubpoena",
                        Path.Combine(externalRoot, "eSubpoena"),
                        plan.State + plan.Client + "eSubpoena");
                }

                if (plan.DoEap || plan.DoESub)
                {
                    string externalRoot = ResolveExternalRoot(plan.State, plan.Client);
                    string daFolder = Directory.Exists(Path.Combine(externalRoot, "PBKDataAccess"))
                                      ? "PBKDataAccess" : "DataAccess";

                    IISManager.EnsureAppUnderDefault(
                        plan.State + plan.Client + daFolder,
                        Path.Combine(externalRoot, daFolder),
                        plan.State + plan.Client + daFolder);
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

        private string ResolveExternalRoot(string state, string client)
        {
            // Use configured SitesRootPath (production root)
            string wwwroot = appConfig.SitesRootPath;
            string basePath = Path.Combine(wwwroot, state);
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            // Find any folder whose name contains "external" (case-insensitive)
            string? externalBase = Directory.GetDirectories(basePath)
                .FirstOrDefault(d => d != null &&
                                     d.Split(Path.DirectorySeparatorChar).Last()
                                      .IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0);

            if (string.IsNullOrEmpty(externalBase))
            {
                externalBase = Path.Combine(basePath, "PbkExternal");
                Directory.CreateDirectory(externalBase);
            }

            // Client root under the external folder: …\External*\{STATE}{CLIENT}
            string clientRoot = Path.Combine(externalBase, state + client);
            Directory.CreateDirectory(clientRoot);
            return clientRoot;
        }

        private static string ResolveExternalRootPreviewUsingRoot(string state, string client, string wwwroot)
        {
            // base and "preferred" external folder
            string stateBase = Path.Combine(wwwroot, state);
            string pbk = Path.Combine(stateBase, "PbkExternal");

            // If the {STATE} folder doesn't exist yet, don't enumerate—just predict the final path.
            if (!Directory.Exists(stateBase))
                return Path.Combine(pbk, state + client);

            // Now it's safe to enumerate inside {STATE}.
            string externalBase = Directory.Exists(pbk)
                ? pbk
                : Directory.GetDirectories(stateBase, "*external*", SearchOption.TopDirectoryOnly)
                           .FirstOrDefault() ?? pbk;

            return Path.Combine(externalBase, state + client);
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

            // Filename convention checks (only validate if present)
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

        private static string ResolveExternalRootPreview(string state, string client)
        {
            // Deprecated: keep for backward compatibility. Prefer ResolveExternalRootPreviewUsingRoot + appConfig.SitesRootPath
            return ResolveExternalRootPreviewUsingRoot(state, client, @"F:\inetpub\wwwroot");
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

                // (optional) if your panel has a method to select/highlight a site, call it:
                // siteSelectorPanel.TrySelect(highlightSite);
            }
            catch
            {
                // Silent refresh: ignore errors (keeps UX clean after create)
            }
        }
    }
}
