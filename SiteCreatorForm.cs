using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TheTool
{
    public partial class SiteCreatorForm : Form
    {
        public SiteCreatorForm()
        {
            InitializeComponent();

            // Live validation/preview hooks
            txtState.Leave += (_, __) => ValidateInputs(updateUiOnly: true);
            txtClient.TextChanged += (_, __) => ValidateInputs(updateUiOnly: true);
            chkProduction.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);
            chkCaseInfoSearch.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);
            chkESubpoena.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);

            ValidateInputs(updateUiOnly: true);
        }

        // === Public read-only outputs for MainForm ===
        public string State => (txtState.Text ?? "").Trim().ToUpperInvariant();
        public string Client => (txtClient.Text ?? "").Trim();
        public bool CreateProd => chkProduction.Checked;
        public bool CreateEAP => chkCaseInfoSearch.Checked;
        public bool CreateESub => chkESubpoena.Checked;

        private void btnCreate_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs(updateUiOnly: false)) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private bool ValidateInputs(bool updateUiOnly)
        {
            bool stateOk = State.Length == 2
                            && State.All(char.IsLetter)
                            && StateHash.ValidStates.Contains(State);
            bool clientOk = !string.IsNullOrWhiteSpace(Client);

            // Require at least one real role; DA is implied by externals
            bool anyRole = CreateProd || CreateEAP || CreateESub;

            btnCreate.Enabled = stateOk && clientOk && anyRole;

            try { txtPreview.Text = BuildPreviewText(); } catch { /* keep silent in preview */ }

            if (!updateUiOnly)
            {
                if (!stateOk) MessageBox.Show("Enter a valid 2-letter US state (e.g., KS).", "Validation");
                if (!clientOk) MessageBox.Show("Enter a client name.", "Validation");
                if (!anyRole) MessageBox.Show("Select at least one site/app to create.", "Validation");
            }

            return stateOk && clientOk && anyRole;
        }

        private string BuildPreviewText()
        {
            // Preview only — no IO creation here.
            string site = State + Client;

            // Production preview target: …\{STATE}{CLIENT}\{tag}
            string prodBase = FileManager.ResolveProdBasePath(State, Client);
            string prodTarget = Path.Combine(prodBase, TodayTag());

            // External client root: …\{STATE}\PbkExternal\{STATE}{CLIENT} (predicted safely)
            string clientRoot = ResolveExternalRootForPreview(State, Client);

            var lines = new List<string>();

            if (CreateProd)
                lines.Add($"{site} → {prodTarget}");

            if (CreateEAP)
                lines.Add($"{site}CaseInfoSearch → {Path.Combine(clientRoot, "CaseInfoSearch")}");

            if (CreateESub)
                lines.Add($"{site}eSubpoena → {Path.Combine(clientRoot, "eSubpoena")}");

            // DataAccess is implied if any external is selected
            if (CreateEAP || CreateESub)
            {
                // Prefer PBKDataAccess if already present under the *existing* client root; otherwise show DataAccess.
                // IMPORTANT: In preview we don't enumerate if the state folder doesn't exist yet.
                string daFolder = Directory.Exists(Path.Combine(clientRoot, "PBKDataAccess"))
                                  ? "PBKDataAccess"
                                  : "DataAccess";
                lines.Add($"{site}{daFolder} → {Path.Combine(clientRoot, daFolder)}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string ResolveExternalRootForPreview(string state, string client, string wwwrootBase = @"F:\inetpub\wwwroot")
        {
            // If state is invalid, just predict a normalized path — main form will reject on OK anyway.
            if (string.IsNullOrWhiteSpace(state) || state.Length != 2 || !StateHash.ValidStates.Contains(state))
                return Path.Combine(wwwrootBase, state, "PbkExternal", state + client);

            string stateBase = Path.Combine(wwwrootBase, state);
            string pbk = Path.Combine(stateBase, "PbkExternal");

            // If {STATE} folder doesn't exist, don't enumerate — just predict PbkExternal\{STATE}{CLIENT}
            if (!Directory.Exists(stateBase))
                return Path.Combine(pbk, state + client);

            // If it exists, we can safely check for an existing "*external*" variant; otherwise default to PbkExternal.
            string externalBase = Directory.Exists(pbk)
                ? pbk
                : Directory.GetDirectories(stateBase, "*external*", SearchOption.TopDirectoryOnly)
                           .FirstOrDefault() ?? pbk;

            return Path.Combine(externalBase, state + client);
        }

        // Match MainForm’s tag format ("MMddyyyy")
        private static string TodayTag() => DateTime.Now.ToString("MMddyyyy");
    }
}
