using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TheTool
{
    public partial class ConfirmationForm : Form
    {
        public bool Confirmed { get; private set; } = false;

        // Log UI (created programmatically so we don't touch your designer layout)
        private Panel pnlLog;
        private Label lblLogHeader;
        private TextBox txtLog;

        // === Cooperative abort state ===
        private enum UpdatePhase { None, Prod, Externals }
        private enum AbortMode
        {
            None,
            // Abort clicked while in Prod for a client -> finish Prod, then skip this client's externals and all remaining clients
            SkipExternalsAndRestAfterCurrentProd,
            // Abort clicked while in Externals for a client -> finish all externals for this client, then skip remaining clients
            SkipRestAfterCurrentExternals
        }

        private volatile bool _abortRequested;
        private AbortMode _abortMode = AbortMode.None;
        private UpdatePhase _currentPhase = UpdatePhase.None;
        private string _currentSite = string.Empty;

        // Raised when the user presses Confirm and we should start the update.
        public event Action<List<(string SiteName, bool Prod, bool EAP, bool eSub)>>? RunConfirmed;

        // Raised immediately when Abort is clicked (visibility controlled by SwitchToLogView)
        public event Action? AbortRequested;

        public ConfirmationForm(List<(string SiteName, bool Prod, bool EAP, bool eSub)> selectedSites)
        {
            InitializeComponent();
            CreateLogControls();

            // Ensure Abort is hidden until we flip to log mode
            if (btnAbort != null)
            {
                btnAbort.Visible = false;
                btnAbort.Enabled = true;
            }

            foreach (var (site, prod, eap, esub) in selectedSites)
            {
                dgvSummary.Rows.Add(site, prod, eap, esub);
            }
        }

        private void CreateLogControls()
        {
            pnlLog = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = SystemColors.Window
            };

            lblLogHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold),
                Text = "Update log"
            };

            txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = false
            };

            pnlLog.Controls.Add(txtLog);
            pnlLog.Controls.Add(lblLogHeader);
            Controls.Add(pnlLog);
            pnlLog.BringToFront();
        }

        // === Buttons ===

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            Confirmed = true;
            SwitchToLogView("Update Log");
            RunConfirmed?.Invoke(GetUpdatedSelections());
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Confirmed = false;
            this.Close();
        }

        private void btnUnlock_Click(object sender, EventArgs e)
        {
            bool isNowEditable = dgvSummary.ReadOnly;
            dgvSummary.ReadOnly = !isNowEditable;

            foreach (DataGridViewColumn column in dgvSummary.Columns)
            {
                if (column is DataGridViewCheckBoxColumn)
                {
                    column.ReadOnly = !isNowEditable;
                    column.DefaultCellStyle.BackColor = column.ReadOnly
                        ? Color.LightGray
                        : Color.White;
                }
            }

            btnUnlock.Text = isNowEditable ? "Lock" : "Edit";
        }

        private void btnAbort_Click(object sender, EventArgs e)
        {
            // Visible only in log view
            btnAbort.Enabled = false; // debounce
            _abortRequested = true;

            // Capture the intended abort mode based on where we are right now
            _abortMode = _currentPhase switch
            {
                UpdatePhase.Prod => AbortMode.SkipExternalsAndRestAfterCurrentProd,
                UpdatePhase.Externals => AbortMode.SkipRestAfterCurrentExternals,
                _ => AbortMode.SkipExternalsAndRestAfterCurrentProd // default to earliest safe boundary
            };

            AppendLog("Abort Requested. Completing current update.");
            AbortRequested?.Invoke();
        }

        // === Selections and summary grid ===

        public List<(string SiteName, bool Prod, bool EAP, bool eSub)> GetUpdatedSelections()
        {
            var list = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>();
            foreach (DataGridViewRow row in dgvSummary.Rows)
            {
                string siteName = row.Cells["colSiteName"].Value?.ToString() ?? string.Empty;
                bool prod = Convert.ToBoolean(row.Cells["colProd"].Value ?? false);
                bool eap = Convert.ToBoolean(row.Cells["colEAP"].Value ?? false);
                bool esub = Convert.ToBoolean(row.Cells["colESub"].Value ?? false);
                list.Add((siteName, prod, eap, esub));
            }
            return list;
        }

        // === Live log API (thread-safe) ===

        public void SwitchToLogView(string header)
        {
            // Hide the grid, show the log panel
            if (dgvSummary.Visible) dgvSummary.Visible = false;

            if (pnlLog != null)
            {
                lblLogHeader.Text = string.IsNullOrWhiteSpace(header) ? "Update log" : header;
                pnlLog.Visible = true;
                pnlLog.BringToFront();
            }

            // Hide edit/confirm in log mode; show Abort
            btnConfirm.Visible = false;
            btnUnlock.Visible = false;

            if (btnAbort != null)
            {
                btnAbort.Enabled = true;
                btnAbort.Visible = true;
                btnAbort.BringToFront();
            }

            this.AcceptButton = null;
            txtLog?.Focus();
        }

        public void AppendLog(string line)
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(AppendLog), line); }
                catch { }
                return;
            }

            if (!string.IsNullOrEmpty(line))
                txtLog.AppendText(line + Environment.NewLine);
        }

        public void MarkComplete(string footerMessage)
        {
            AppendLog(string.IsNullOrWhiteSpace(footerMessage) ? "Done." : footerMessage);

            if (IsDisposed) return;

            void apply()
            {
                if (btnAbort != null) btnAbort.Visible = false;
                btnCancel.Text = "Close";
                btnCancel.Focus();
            }

            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(apply)); } catch { }
            }
            else
            {
                apply();
            }
        }

        // === Phase tracking + checkpoint helpers (used by your MainForm loop) ===
        //
        // Usage pattern in MainForm (pseudocode):
        //
        // confirmForm.EnterProdPhase(site);
        // await DoProdAsync(site);
        // if (confirmForm.ShouldSkipExternalsFor(site)) { break or continue-all-skip; }
        //
        // confirmForm.EnterExternalsPhase(site);
        // await DoAllExternalsAsync(site); // EAP + eSub + DataAccess as applicable
        // if (confirmForm.ShouldStopAfterExternals(site)) { break; }

        public void EnterProdPhase(string siteName)
        {
            _currentSite = siteName ?? string.Empty;
            _currentPhase = UpdatePhase.Prod;
        }

        public void EnterExternalsPhase(string siteName)
        {
            _currentSite = siteName ?? string.Empty;
            _currentPhase = UpdatePhase.Externals;
        }

        public void LeavePhase() // optional: call after finishing a phase
        {
            _currentPhase = UpdatePhase.None;
        }

        /// <summary>
        /// True when Abort was requested during the Prod phase of this site.
        /// Honor this immediately after finishing Prod: skip this site's externals and skip all remaining clients.
        /// </summary>
        public bool ShouldSkipExternalsFor(string siteName)
        {
            if (!_abortRequested) return false;
            if (_abortMode != AbortMode.SkipExternalsAndRestAfterCurrentProd) return false;
            return string.Equals(_currentSite, siteName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when Abort was requested during the Externals phase of this site.
        /// Honor this after finishing all externals for this client: skip all remaining clients.
        /// </summary>
        public bool ShouldStopAfterExternals(string siteName)
        {
            if (!_abortRequested) return false;
            if (_abortMode != AbortMode.SkipRestAfterCurrentExternals) return false;
            return string.Equals(_currentSite, siteName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Exposes whether Abort was requested at all (useful for between-client boundary checks).
        /// </summary>
        public bool IsAbortRequested => _abortRequested;
    }
}
