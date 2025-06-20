using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

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
        //private void LoadAppPoolsIntoPanel()
        //{
        //    var appPools = IISManager.GetAppPools();
        //    var siteNames = appPools.Select(p => p.FullName);
        //    siteSelectorPanel.LoadSites(siteNames);
        //}

        //Browser handlers
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
        
        //.zip checker
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

        //
        private void BtnExecute_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProdPath.Text))
            {
                MessageBox.Show("Please select the production build .zip file.");
                return;
            }

            string prodZip = txtProdPath.Text;
            string eapZip = txtEapPath.Text;
            string esubZip = txtESubPath.Text;
            string dataAccessZip = txtDataAccessPath.Text;

            string tempDir = Path.Combine(Path.GetTempPath(), "TempDirectory_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            string prodExtractPath = Path.Combine(tempDir, "Prod");
            string eapExtractPath = Path.Combine(tempDir, "EAP");
            string esubExtractPath = Path.Combine(tempDir, "eSub");
            string dataAccessExtractPath = Path.Combine(tempDir, "DataAccess");

            try
            {
                ZipFile.ExtractToDirectory(prodZip, prodExtractPath);
                if (File.Exists(eapZip)) ZipFile.ExtractToDirectory(eapZip, eapExtractPath);
                if (File.Exists(esubZip)) ZipFile.ExtractToDirectory(esubZip, esubExtractPath);
                if (File.Exists(dataAccessZip)) ZipFile.ExtractToDirectory(dataAccessZip, dataAccessExtractPath);

                var selectedSites = siteSelectorPanel.GetSelectedSites();

                if (!selectedSites.Any())
                {
                    MessageBox.Show("No sites selected for update.");
                    return;
                }

                using (var confirmForm = new ConfirmationForm(selectedSites))
                {
                    var result = confirmForm.ShowDialog();
                    if (confirmForm.ShowDialog(this) == DialogResult.OK)
                        return;
                }

                foreach (var (siteName, prod, eap, esub) in selectedSites)
                {
                    string targetPath = AppPoolPathUtil.GetSitePath(siteName);

                    if (prod)
                    {
                        FileManager.ApplyBuild(targetPath, prodExtractPath, isProd: true);
                    }

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
    }
}
