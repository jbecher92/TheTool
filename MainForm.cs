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
                    var flavor = (appConfig.DeploymentFlavor ?? "prod").Trim().ToLowerInvariant();

                    // Resolve once (Option-1) and pass down
                    string resolvedRoot = NormalizeRoot(
                        ResolveRootPath(appConfig.DeploymentFlavor, appConfig.SitesRootPath));
                        
                    await RunDeploymentAsync(finalSelections, prodZip, eapZip, esubZip, dataAccessZip,
                                                     isCreate: false, log: log, confirmCtx: confirmForm,
                                                     resolvedRoot: resolvedRoot);
                            
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



        private async void BtnSiteCreator_Click_1(object sender, EventArgs e)
        {
            // Gather inputs from the site creator dialog
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

            // Config validation
            if (string.IsNullOrWhiteSpace(appConfig?.SitesRootPath))
                throw new InvalidOperationException("SitesRootPath is not defined in config.json");
            if (string.IsNullOrWhiteSpace(appConfig?.DeploymentFlavor))
                throw new InvalidOperationException("DeploymentFlavor is not defined in config.json");

            // Option-1: resolve root once here
            string resolvedRoot = NormalizeRoot(
                ResolveRootPath(appConfig.DeploymentFlavor, appConfig.SitesRootPath));

            // Build the single-site plan we’ll pass to the deployer
            var confirmedSites = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>
    {
        (state + client, makeProd, makeEAP, makeESub)
    };

            // Zips picked in the main window
            string prodZip = txtProdPath.Text;
            string eapZip = txtEapPath.Text;
            string esubZip = txtESubPath.Text;
            string dataAccessZip = txtDataAccessPath.Text;

            // Preview/confirm form (builds its own preview; no disk I/O)
            using var confirm = new SiteCreationConfirmationForm(
                resolvedRoot: resolvedRoot,
                state: state,
                client: client,
                createProd: makeProd,
                createEAP: makeEAP,
                createESub: makeESub);

            // When the user clicks Confirm inside the dialog, it switches to "in progress"
            // and fires this event. We run the creation and then mark complete.
            confirm.CreateConfirmed += async () =>
            {
                try
                {
                    await RunDeploymentAsync(
                        confirmedSites,
                        prodZip, eapZip, esubZip, dataAccessZip,
                        isCreate: true,
                        log: null,            // no streaming to the dialog
                        confirmCtx: null,     // not using the update ConfirmationForm
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

            // Show modal; user sees preview first. On Confirm, the same window shows "Creation in progress..."
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
    string resolvedRoot) // REQUIRED, passed by caller per Option-1
        {
            if (!ValidateZipSelections(confirmedSites, prodZip, eapZip, esubZip, dataAccessZip))
                throw new InvalidOperationException("Validation failed.");

            if (string.IsNullOrWhiteSpace(resolvedRoot))
                throw new InvalidOperationException("resolvedRoot must be provided (Option-1 rule).");

            string tag = TodayTag();

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

            // CREATE: bootstrap dirs
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
                        string externalRoot = FileManager.ResolveExternalRoot(plan.State, plan.Client, AppConfigManager.Config.DeploymentFlavor, resolvedRoot, externalRootCache);
                        Directory.CreateDirectory(externalRoot);

                        if (plan.DoEap) Directory.CreateDirectory(Path.Combine(externalRoot, "CaseInfoSearch"));
                        if (plan.DoESub) Directory.CreateDirectory(Path.Combine(externalRoot, "eSubpoena"));

                        string daFolder = Directory.Exists(Path.Combine(externalRoot, "PBKDataAccess")) ? "PBKDataAccess" : "DataAccess";
                        Directory.CreateDirectory(Path.Combine(externalRoot, daFolder));
                    }
                }
            }

            // WORK
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
                        if (plan.DoProd)
                        {
                            confirmCtx?.EnterProdPhase(plan.SiteName);
                            log?.Invoke($"{plan.SiteName}: Updating Production.");

                            await Task.Run(() =>
                            {
                                string prodBasePath = FileManager.ResolveProdBasePath(resolvedRoot, plan.State, plan.Client);
                                string prevFolder = isCreate ? string.Empty : (FindLatestVersionFolder(prodBasePath) ?? string.Empty);

                                FileManager.DeployProduction_Update(
                                    plan.State, plan.Client, resolvedRoot, prevFolder, tag, prodZip, onProgress: log);
                            });

                            confirmCtx?.LeavePhase();

                            if (confirmCtx?.ShouldSkipExternalsFor(plan.SiteName) == true)
                            {
                                log?.Invoke($"Aborting {plan.SiteName} Production. Skipping externals and all remaining clients.");
                                break;
                            }
                        }

                        if (plan.DoEap || plan.DoESub)
                        {
                            confirmCtx?.EnterExternalsPhase(plan.SiteName);
                            log?.Invoke($"{plan.SiteName}: Initiating External Updates");

                            await Task.Run(() =>
                            {
                                string externalRoot = FileManager.ResolveExternalRoot(plan.State, plan.Client, AppConfigManager.Config.DeploymentFlavor, resolvedRoot, externalRootCache);
                                string? cisZip = plan.DoEap ? eapZip : null;
                                string? esZip = plan.DoESub ? esubZip : null;

                                FileManager.DeployExternal_Update(
                                    resolvedRoot, plan.State, plan.Client, tag,
                                    cisZip, esZip, dataAccessZip,
                                    onProgress: log);

                            });

                            confirmCtx?.LeavePhase();

                            if (confirmCtx?.ShouldStopAfterExternals(plan.SiteName) == true)
                            {
                                log?.Invoke($"Aborting {plan.SiteName} Externals. Skipping remaining clients.");
                                break;
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

            // Final IIS binding phase
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

                if (plan.DoEap)
                {
                    string externalRoot = FileManager.ResolveExternalRoot(plan.State, plan.Client, AppConfigManager.Config.DeploymentFlavor, resolvedRoot, externalRootCache);
                    IISManager.EnsureAppUnderDefault(
                        plan.State + plan.Client + "CaseInfoSearch",
                        Path.Combine(externalRoot, "CaseInfoSearch"),
                        plan.State + plan.Client + "CaseInfoSearch");
                }

                if (plan.DoESub)
                {
                    string externalRoot = FileManager.ResolveExternalRoot(plan.State, plan.Client, AppConfigManager.Config.DeploymentFlavor, resolvedRoot, externalRootCache);
                    IISManager.EnsureAppUnderDefault(
                        plan.State + plan.Client + "eSubpoena",
                        Path.Combine(externalRoot, "eSubpoena"),
                        plan.State + plan.Client + "eSubpoena");
                }

                if (plan.DoEap || plan.DoESub)
                {
                    string externalRoot = FileManager.ResolveExternalRoot(plan.State, plan.Client, AppConfigManager.Config.DeploymentFlavor, resolvedRoot, externalRootCache);
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


        //private string ResolveExternalRootPreviewUsingRoot(string state, string client, string root)
        //{
        //    if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(client))
        //        throw new ArgumentException("State and client must be provided.");

        //    string s = state.ToUpperInvariant();
        //    string flavor = appConfig.DeploymentFlavor?.Trim().ToLowerInvariant() ?? "prod";
        //    string basePath = flavor == "prod" ? Path.Combine(root, s) : root;

        //    // Try to find any container with "external" in its name
        //    string? container = null;
        //    try
        //    {
        //        if (Directory.Exists(basePath))
        //        {
        //            container = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
        //                .FirstOrDefault(d =>
        //                    Path.GetFileName(d).IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0);
        //        }
        //    }
        //    catch { }

        //    if (string.IsNullOrEmpty(container))
        //        container = Path.Combine(basePath, "PbkExternal");

        //    return NormalizeRoot(Path.Combine(container, s + client));
        //}


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

        private static string GuessDAFolderNameForPreview(string dataAccessZip)
        {
            var name = Path.GetFileName(dataAccessZip ?? "");
            if (name.StartsWith("PBKDataAccess_", StringComparison.OrdinalIgnoreCase)) return "PBKDataAccess";
            if (name.StartsWith("DataAccess_", StringComparison.OrdinalIgnoreCase)) return "DataAccess";
            return "DataAccess";
        }


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
