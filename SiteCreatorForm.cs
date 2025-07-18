using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TheTool
{
    public partial class SiteCreatorForm : Form
    {
        private const string ExternalFolderFallback = "PbkExternal";

        public SiteCreatorForm()
        {
            InitializeComponent();
            txtState.Leave += txtState_Leave;
            txtClient.TextChanged += UpdatePreview;
            chkProduction.CheckedChanged += UpdatePreview;
            chkCaseInfoSearch.CheckedChanged += UpdatePreview;
            chkESubpoena.CheckedChanged += UpdatePreview;
            chkDataAccess.CheckedChanged += UpdatePreview;
        }

        private void txtState_Leave(object? sender, EventArgs e)
        {
            ValidateState();
        }

        private void ValidateState()
        {
            string state = txtState.Text.Trim().ToUpper();
            bool isValid = StateHash.ValidStates.Contains(state);

            picStateValidation.Image = isValid
                ? SystemIcons.Information.ToBitmap()
                : SystemIcons.Error.ToBitmap();

            btnCreate.Enabled = isValid && HasCheckedRoles();
        }

        private bool HasCheckedRoles() =>
            chkProduction.Checked || chkCaseInfoSearch.Checked || chkESubpoena.Checked || chkDataAccess.Checked;

        private void UpdatePreview(object? sender, EventArgs e)
        {
            string state = txtState.Text.Trim().ToUpper();
            string rawClient = txtClient.Text.Trim();
            string client = CapitalizeFirst(rawClient);

            if (!StateHash.ValidStates.Contains(state) || string.IsNullOrWhiteSpace(client))
            {
                txtPreview.Text = string.Empty;
                btnCreate.Enabled = false;
                return;
            }

            btnCreate.Enabled = HasCheckedRoles();

            string fullClientName = state + client;
            string basePath = Path.Combine("C:\\inetpub\\wwwroot", state);
            string siteRoot = Path.Combine(basePath, fullClientName);
            string externalRoot = ResolveOrCreateExternalPath(basePath);

            List<string> preview = new();

            if (chkProduction.Checked)
                preview.Add($"{fullClientName} -> {siteRoot}");

            if (chkCaseInfoSearch.Checked)
                preview.Add($"{fullClientName}CaseInfoSearch -> {Path.Combine(externalRoot, fullClientName, "CaseInfoSearch")}");

            if (chkESubpoena.Checked)
                preview.Add($"{fullClientName}eSubpoena -> {Path.Combine(externalRoot, fullClientName, "eSubpoena")}");

            if (chkDataAccess.Checked)
                preview.Add($"{fullClientName}DataAccess -> {Path.Combine(externalRoot, fullClientName, "DataAccess")}");

            txtPreview.Text = string.Join(Environment.NewLine, preview);
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            string state = txtState.Text.Trim().ToUpper();
            string rawClient = txtClient.Text.Trim();
            string client = CapitalizeFirst(rawClient);
            string fullClientName = state + client;

            string basePath = Path.Combine("C:\\inetpub\\wwwroot", state);
            string productionSiteRoot = Path.Combine(basePath, fullClientName);
            string externalRoot = ResolveOrCreateExternalPath(basePath);

            List<(string Label, string Path)> entries = new();

            if (chkProduction.Checked)
                entries.Add((fullClientName, productionSiteRoot));
            if (chkCaseInfoSearch.Checked)
                entries.Add(($"{fullClientName}CaseInfoSearch", Path.Combine(externalRoot, fullClientName, "CaseInfoSearch")));
            if (chkESubpoena.Checked)
                entries.Add(($"{fullClientName}eSubpoena", Path.Combine(externalRoot, fullClientName, "eSubpoena")));
            if (chkDataAccess.Checked)
                entries.Add(($"{fullClientName}DataAccess", Path.Combine(externalRoot, fullClientName, "DataAccess")));

            using var confirm = new SiteCreationConfirmationForm(entries);
            if (confirm.ShowDialog(this) != DialogResult.OK)
                return;

            bool openedExternal = false;
            bool openedProd = false;
            List<string> status = new();

            foreach (var (label, path) in entries)
            {
                try
                {
                    FileManager.CreateSiteDirectory(path);
                    status.Add($"✓ Created: {path}");

                    if (label == fullClientName)
                        openedProd = true;
                    else
                        openedExternal = true;
                }
                catch (IOException ex)
                {
                    status.Add($"✗ {ex.Message}");
                }
            }

            if (openedProd)
                FileManager.LaunchExplorer(productionSiteRoot);

            if (openedExternal)
                FileManager.LaunchExplorer(Path.Combine(externalRoot, fullClientName));

            txtPreview.Text = string.Join(Environment.NewLine, status);
        }

        private static string CapitalizeFirst(string input) =>
            string.IsNullOrWhiteSpace(input)
                ? input
                : char.ToUpper(input[0]) + input.Substring(1).ToLower();

        private static string ResolveOrCreateExternalPath(string basePath)
        {
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            string? existing = Directory.GetDirectories(basePath)
                .FirstOrDefault(d => Path.GetFileName(d)
                    .Contains("external", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(existing))
                return existing;

            string fallback = Path.Combine(basePath, ExternalFolderFallback);
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
