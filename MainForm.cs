using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TheTool
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            LoadAppPoolsIntoPanel();
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

                // Delegate log writer to confirmation form
                Action<string> log = line => confirmForm.AppendLog(line);

                try
                {
                    await RunDeploymentAsync(finalSelections,
                                             prodZip, eapZip, esubZip, dataAccessZip,
                                             isCreate: false,
                                             log: log);

                    confirmForm.MarkComplete("All updates completed.");
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
            bool makeDA = (makeEAP || makeESub);   // ⬅️ DA implied by externals

            if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(client))
            {
                MessageBox.Show("State and Client are required.", "Validation Error");
                return;
            }

            var confirmedSites = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>
    {
        (state + client, makeProd, makeEAP, makeESub)
    };

            // Read zips at confirm time
            string prodZip = txtProdPath.Text;
            string eapZip = txtEapPath.Text;
            string esubZip = txtESubPath.Text;
            string dataAccessZip = txtDataAccessPath.Text;

            // Build preview
            string siteName = state + client;
            string tag = TodayTag();
            string prodNew = Path.Combine(FileManager.ResolveProdBasePath(state, client), tag);
            string externalRoot = ResolveExternalRootPreview(state, client);

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
                await RunDeploymentAsync(confirmedSites,
                                         prodZip, eapZip, esubZip, dataAccessZip,
                                         isCreate: true,
                                         log: null);

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



        private async Task RunDeploymentAsync(
    List<(string SiteName, bool Prod, bool EAP, bool eSub)> confirmedSites,
    string prodZip,
    string eapZip,
    string esubZip,
    string dataAccessZip,
    bool isCreate,
    Action<string>? log)
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

            // ---- FIX #2: bootstrap directories on CREATE only ----
            if (isCreate)
            {
                foreach (var plan in sitePlans)
                {
                    // Production folder: ...\{STATE}{CLIENT}\{tag}
                    if (plan.DoProd)
                    {
                        string prodBase = FileManager.ResolveProdBasePath(plan.State, plan.Client);
                        Directory.CreateDirectory(Path.Combine(prodBase, tag));
                    }

                    // External roots & apps
                    if (plan.DoEap || plan.DoESub)
                    {
                        // Use creating resolver here (this is the real op, not preview)
                        string extRoot = ResolveExternalRoot(plan.State, plan.Client);
                        Directory.CreateDirectory(extRoot);

                        if (plan.DoEap)
                            Directory.CreateDirectory(Path.Combine(extRoot, "CaseInfoSearch"));
                        if (plan.DoESub)
                            Directory.CreateDirectory(Path.Combine(extRoot, "eSubpoena"));

                        // DataAccess implied by externals
                        string daFolder = Directory.Exists(Path.Combine(extRoot, "PBKDataAccess"))
                                          ? "PBKDataAccess" : "DataAccess";
                        Directory.CreateDirectory(Path.Combine(extRoot, daFolder));
                    }
                }
            }
            // ------------------------------------------------------

            // The file work to perform (wrapped by pool stop/start for Update)
            Func<Task> work = async () =>
            {
                foreach (var plan in sitePlans)
                {
                    if (plan.DoProd)
                    {
                        log?.Invoke($"{plan.SiteName}: Production deploy starting.");
                        await Task.Run(() =>
                        {
                            string basePath = FileManager.ResolveProdBasePath(plan.State, plan.Client);
                            string prevFolder = isCreate ? string.Empty : (FindLatestVersionFolder(basePath) ?? string.Empty);

                            FileManager.DeployProduction_Update(
                                plan.State, plan.Client, prevFolder, tag, prodZip, onProgress: null);
                        });
                    }

                    if (plan.DoEap || plan.DoESub)
                    {
                        log?.Invoke($"{plan.SiteName}: External deploy starting.");
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
                    }
                }

                // Update-only: clear ASP.NET temp after file ops
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

        private static string ResolveExternalRoot(string state, string client)
        {
            string basePath = Path.Combine(@"F:\inetpub\wwwroot", state);
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

        // ===== File Helpers =====

        private string OpenZipFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                // UI accepts .zip and .7z; FileManager currently deploys .zip only.
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

        private static bool IsZipAvailable(string path) {return !string.IsNullOrWhiteSpace(path) && File.Exists(path);}

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
            // base and "preferred" external folder
            string wwwroot = @"F:\inetpub\wwwroot";
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


        

