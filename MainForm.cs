using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace TheTool
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            LoadDummyAppPools();
            //LoadAppPoolsIntoPanel();
        }

        private void LoadDummyAppPools()
        {
            var dummySites = Enumerable.Range(1, 20).Select(i => $"ST{i:D2}_Site{i}");
            siteSelectorPanel.LoadSites(dummySites);
        }

        private void LoadAppPoolsIntoPanel() 
        { 

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

        private string OpenZipFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "ZIP files (*.zip)|*.zip";
                dialog.Title = "Select a Build .zip File";
                dialog.Multiselect = false;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : string.Empty;
            }
        }

        private bool IsZipAvailable(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        private bool ValidateFinalZipSelections(
            List<(string SiteName, bool Prod, bool EAP, bool eSub)> confirmedSites,
            string prodZip,
            string eapZip,
            string esubZip,
            string dataAccessZip)
        {
            bool anyProd = confirmedSites.Any(s => s.Prod);
            bool anyEAP = confirmedSites.Any(s => s.EAP);
            bool anyESub = confirmedSites.Any(s => s.eSub);

            if (anyProd && !IsZipAvailable(prodZip))
            {
                MessageBox.Show("A Production build is required based on your final selections.");
                return false;
            }
            if (anyEAP && !IsZipAvailable(eapZip))
            {
                MessageBox.Show("An EAP build is required based on your final selections.");
                return false;
            }
            if (anyESub && !IsZipAvailable(esubZip))
            {
                MessageBox.Show("An eSub build is required based on your final selections.");
                return false;
            }
            if ((anyEAP || anyESub) && !IsZipAvailable(dataAccessZip))
            {
                MessageBox.Show("A DataAccess build is required for EAP or eSub based on your final selections.");
                return false;
            }

            return true;
        }
        //validate selections and bring up confirmation screen, execute update if confirmed
        private void BtnExecute_Click(object sender, EventArgs e)
        {
            string prodZip = txtProdPath.Text;
            string eapZip = txtEapPath.Text;
            string esubZip = txtESubPath.Text;
            string dataAccessZip = txtDataAccessPath.Text;

            var selectedSites = siteSelectorPanel.GetSelectedSites();

            if (!selectedSites.Any())
            {
                MessageBox.Show("No sites selected for update.");
                return;
            }

            if (!ValidateSelections(selectedSites, prodZip, eapZip, esubZip, dataAccessZip))
                return;

            using (var confirmForm = new ConfirmationForm(selectedSites))
            {
                if (confirmForm.ShowDialog(this) != DialogResult.OK)
                    return;

                selectedSites = confirmForm.GetUpdatedSelections();

                if (!ValidateSelections(selectedSites, prodZip, eapZip, esubZip, dataAccessZip))
                    return;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "TempDirectory_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            string prodExtractPath = Path.Combine(tempDir, "Prod");
            string eapExtractPath = Path.Combine(tempDir, "EAP");
            string esubExtractPath = Path.Combine(tempDir, "eSub");
            string dataAccessExtractPath = Path.Combine(tempDir, "DataAccess");

            try
            {
                if (File.Exists(prodZip)) ZipFile.ExtractToDirectory(prodZip, prodExtractPath);
                if (File.Exists(eapZip)) ZipFile.ExtractToDirectory(eapZip, eapExtractPath);
                if (File.Exists(esubZip)) ZipFile.ExtractToDirectory(esubZip, esubExtractPath);
                if (File.Exists(dataAccessZip)) ZipFile.ExtractToDirectory(dataAccessZip, dataAccessExtractPath);

                foreach (var (siteName, prod, eap, esub) in selectedSites)
                {
                    string targetPath = AppPoolPathUtil.GetSitePath(siteName);

                    if (prod && Directory.Exists(prodExtractPath))
                        FileManager.ApplyBuild(targetPath, prodExtractPath, isProd: true);

                    if (eap && Directory.Exists(eapExtractPath))
                        FileManager.ApplyBuild(targetPath, eapExtractPath, isProd: false);

                    if (esub && Directory.Exists(esubExtractPath))
                        FileManager.ApplyBuild(targetPath, esubExtractPath, isProd: false);

                    if ((eap || esub) && Directory.Exists(dataAccessExtractPath))
                        FileManager.ApplyBuild(targetPath, dataAccessExtractPath, isProd: false);
                }

                MessageBox.Show("Update completed successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error during execution:\n" + ex.Message);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }

        //confirmation panel validator
        private bool ValidateSelections(IEnumerable<(string SiteName, bool Prod, bool EAP, bool eSub)> selections, string prodZip, string eapZip, string esubZip, string dataAccessZip)
        {
            if (selections.Any(s => s.Prod) && string.IsNullOrWhiteSpace(prodZip))
            {
                MessageBox.Show("Production build (.zip) is required for selected sites.", "Validation Error");
                return false;
            }
            if (selections.Any(s => s.EAP) && string.IsNullOrWhiteSpace(eapZip))
            {
                MessageBox.Show("EAP build (.zip) is required for selected sites.", "Validation Error");
                return false;
            }
            if (selections.Any(s => s.eSub) && string.IsNullOrWhiteSpace(esubZip))
            {
                MessageBox.Show("eSub build (.zip) is required for selected sites.", "Validation Error");
                return false;
            }
            if ((selections.Any(s => s.EAP || s.eSub)) && string.IsNullOrWhiteSpace(dataAccessZip))
            {
                MessageBox.Show("DataAccess build (.zip) is required for eSub or EAP updates.", "Validation Error");
                return false;
            }
            return true;
        }



    }
}
