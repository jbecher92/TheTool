using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TheTool
{
    public partial class SiteCreationConfirmationForm : Form
    {
        public bool Confirmed { get; private set; } = false;

        // Fire when user clicks Confirm so MainForm can start the work
        public event Action? CreateConfirmed;

        // Inputs for preview (no disk/IIS work here)
        private readonly string _resolvedRoot;
        private readonly string _state;
        private readonly string _client;
        private readonly bool _createProd;
        private readonly bool _createEAP;
        private readonly bool _createESub;

        // Minimal progress UI (no logs)
        private Panel? pnlProgress;
        private Label? lblProgress;
        private ProgressBar? prg;

        public SiteCreationConfirmationForm(
            string resolvedRoot,
            string state,
            string client,
            bool createProd,
            bool createEAP,
            bool createESub)
        {
            InitializeComponent();

            _resolvedRoot = resolvedRoot ?? throw new ArgumentNullException(nameof(resolvedRoot));
            _state = (state ?? "").Trim().ToUpperInvariant();
            _client = (client ?? "").Trim();
            _createProd = createProd;
            _createEAP = createEAP;
            _createESub = createESub;

            InitializePreviewGrid();
            PopulatePreview(BuildPreview());
        }

        private void InitializePreviewGrid()
        {
            if (dgvPreview.Columns.Count == 0)
            {
                dgvPreview.Columns.Add("colSite", "Site Name");
                dgvPreview.Columns.Add("colPath", "Path");
                dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                dgvPreview.AllowUserToAddRows = false;
                dgvPreview.RowHeadersVisible = false;
                dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            }
        }

        private List<(string Label, string Path)> BuildPreview()
        {
            var rows = new List<(string Label, string Path)>();
            string site = _state + _client;

            // Option-1 respected: caller passed _resolvedRoot. These are pure string resolvers.
            string prodBase = FileManager.ResolveProdBasePath(_resolvedRoot, _state, _client);
            string externalRoot = FileManager.ResolveExternalRoot(_state, _client, AppConfigManager.Config.DeploymentFlavor, _resolvedRoot);

            if (_createProd)
                rows.Add((site, prodBase));

            if (_createEAP)
                rows.Add(($"{site}CaseInfoSearch", System.IO.Path.Combine(externalRoot, "CaseInfoSearch")));

            if (_createESub)
                rows.Add(($"{site}eSubpoena", System.IO.Path.Combine(externalRoot, "eSubpoena")));

            if (_createEAP || _createESub)
                rows.Add(($"{site}DataAccess", System.IO.Path.Combine(externalRoot, "DataAccess")));

            return rows;
        }

        private void PopulatePreview(IEnumerable<(string Label, string Path)> sites)
        {
            dgvPreview.Rows.Clear();

            foreach (var (label, path) in sites)
                dgvPreview.Rows.Add(label ?? string.Empty, path ?? string.Empty);

            if (!sites.Any())
                dgvPreview.Rows.Add("(no sites)", "");
        }

        // ---- Minimal progress surface ----
        private void EnsureProgressUi()
        {
            if (pnlProgress != null) return;

            pnlProgress = new Panel { Dock = DockStyle.Fill, Visible = false };

            lblProgress = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold),
                Text = "Creation in progress…"
            };

            prg = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 14,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };

            pnlProgress.Controls.Add(prg);
            pnlProgress.Controls.Add(lblProgress);
            Controls.Add(pnlProgress);
        }

        public void SwitchToInProgress(string? header = null)
        {
            EnsureProgressUi();

            if (dgvPreview.Visible) dgvPreview.Visible = false;

            if (!string.IsNullOrWhiteSpace(header) && lblProgress != null)
                lblProgress.Text = header;

            if (pnlProgress != null)
            {
                pnlProgress.Visible = true;
                pnlProgress.BringToFront();
            }

            // Hide Confirm while running; Cancel acts as Cancel/Close depending on completion
            btnConfirm.Enabled = false;
            btnConfirm.Visible = false;

            btnCancel.Enabled = true;
            btnCancel.Text = "Cancel";
        }

        public void MarkComplete(string message = "Creation complete.")
        {
            if (lblProgress != null) lblProgress.Text = message;

            if (prg != null)
            {
                prg.MarqueeAnimationSpeed = 0;
                prg.Style = ProgressBarStyle.Continuous;
                prg.Value = prg.Maximum;
            }

            btnCancel.Text = "Close";
            btnCancel.Enabled = true;
        }

        // ---- Buttons ----

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            // Do NOT close here; flip to in-progress and let MainForm run the work.
            Confirmed = true;
            SwitchToInProgress("Creation in progress…");
            CreateConfirmed?.Invoke();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            // If running, this acts as Cancel; if finished, as Close.
            // You can decide abort semantics in MainForm; here we just close.
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
