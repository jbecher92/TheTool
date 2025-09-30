using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace TheTool
{
    public partial class SiteCreatorForm : Form
    {
        private readonly AppConfig _config;

        public SiteCreatorForm(AppConfig config)
        {
            InitializeComponent();

            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Live validation hooks
            txtState.Leave += (_, __) => ValidateInputs(updateUiOnly: true);
            txtClient.TextChanged += (_, __) => ValidateInputs(updateUiOnly: true);
            chkProduction.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);
            chkCaseInfoSearch.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);
            chkESubpoena.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);

            ValidateInputs(updateUiOnly: true);
        }

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

            bool anyRole = CreateProd || CreateEAP || CreateESub;

            btnCreate.Enabled = stateOk && clientOk && anyRole;

            // No preview building or updates here anymore.

            if (!updateUiOnly)
            {
                if (!stateOk) MessageBox.Show("Enter a valid 2-letter US state (e.g., KS).", "Validation");
                if (!clientOk) MessageBox.Show("Enter a client name.", "Validation");
                if (!anyRole) MessageBox.Show("Select at least one site/app to create.", "Validation");
            }

            return stateOk && clientOk && anyRole;
        }
    }
}
