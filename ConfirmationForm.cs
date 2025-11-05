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

        private Panel pnlLog = null!;
        private Label lblLogHeader = null!;
        private TextBox txtLog = null!;

        private enum UpdatePhase { None, Prod, Externals }
        private enum AbortMode
        {
            None,
            SkipExternalsAndRestAfterCurrentProd,
            SkipRestAfterCurrentExternals
        }

        private volatile bool _abortRequested;
        private AbortMode _abortMode = AbortMode.None;
        private volatile UpdatePhase _currentPhase = UpdatePhase.None;
        private string _currentSite = string.Empty;

        // Raised when the user presses Confirm and we should start the update.
        public event Action<List<(string SiteName, bool Prod, bool EAP, bool eSub)>>? RunConfirmed;

        // Raised immediately when Abort is clicked (visibility controlled by SwitchToLogView)
        public event Action? AbortRequested;

        public ConfirmationForm(List<(string SiteName, bool Prod, bool EAP, bool eSub)> selectedSites)
        {
            InitializeComponent();

            // keep designer columns, don't auto-gen
            dgvSummary.AutoGenerateColumns = false;

            CreateLogControls();

            // Abort is only visible in log mode
            if (btnAbort != null)
            {
                btnAbort.Visible = false;
                btnAbort.Enabled = true;
            }

            foreach (var (site, prod, eap, esub) in selectedSites)
            {
                dgvSummary.Rows.Add(site, prod, eap, esub);
            }

            ApplyRoleAvailabilityFromIis();
        }

        // ------------------------
        // Log panel + controls
        // ------------------------
        private void CreateLogControls()
        {
            // margins around the log panel
            int marginLeft = 12;
            int marginTop = 12;
            int marginRight = 12;
            int bottomGap = 60;   // space to leave for buttons at the bottom

            pnlLog = new Panel
            {
                Visible = false,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),

                // position and size so the bottom edge stays above the buttons
                Location = new Point(marginLeft, marginTop),
                Size = new Size(
                    ClientSize.Width - marginLeft - marginRight,
                    ClientSize.Height - marginTop - bottomGap),

                // resize with the form, keeping the same margins
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                         AnchorStyles.Left | AnchorStyles.Right
            };

            lblLogHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold),
                Text = "Update Log"
            };

            txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = false,
                BorderStyle = BorderStyle.Fixed3D
            };

            pnlLog.Controls.Add(txtLog);
            pnlLog.Controls.Add(lblLogHeader);

            // Add after other controls so it sits in the main area only
            Controls.Add(pnlLog);
            pnlLog.BringToFront();
        }


        // ------------------------
        // Buttons
        // ------------------------

        private void btnConfirm_Click(object? sender, EventArgs e)
        {
            Confirmed = true;
            SwitchToLogView("Update Log");
            RunConfirmed?.Invoke(GetUpdatedSelections());
        }

        private void btnCancel_Click(object? sender, EventArgs e)
        {
            Confirmed = false;
            Close();
        }

        private void btnUnlock_Click(object? sender, EventArgs e)
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

        private void btnAbort_Click(object? sender, EventArgs e)
        {
            // Visible only in log view
            btnAbort.Enabled = false;

            _abortMode = _currentPhase switch
            {
                UpdatePhase.Prod => AbortMode.SkipExternalsAndRestAfterCurrentProd,
                UpdatePhase.Externals => AbortMode.SkipRestAfterCurrentExternals,
                _ => AbortMode.SkipExternalsAndRestAfterCurrentProd
            };

            _abortRequested = true;
            AppendLog("Abort Requested. Completing current update.");
            AbortRequested?.Invoke();
        }

        // ------------------------
        // Selections + grid
        // ------------------------

        public List<(string SiteName, bool Prod, bool EAP, bool eSub)> GetUpdatedSelections()
        {
            var list = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>();

            foreach (DataGridViewRow row in dgvSummary.Rows)
            {
                if (row.IsNewRow) continue;

                string siteName = row.Cells[colSiteName.Index].Value?.ToString() ?? string.Empty;
                bool prod = Convert.ToBoolean(row.Cells[colProd.Index].Value ?? false);
                bool eap = Convert.ToBoolean(row.Cells[colEAP.Index].Value ?? false);
                bool esub = Convert.ToBoolean(row.Cells[colESub.Index].Value ?? false);

                list.Add((siteName, prod, eap, esub));
            }

            return list;
        }

        private void ApplyRoleAvailabilityFromIis()
        {
            Dictionary<string, (bool HasCIS, bool HasESub, bool HasDA)> availability;

            try
            {
                var allPools = IISManager.GetIISAppPools();
                availability = BuildAvailability(allPools);
            }
            catch
            {
                // fail soft: if IIS isn’t readable, don’t block the dialog
                return;
            }

            foreach (DataGridViewRow row in dgvSummary.Rows)
            {
                if (row.IsNewRow) continue;

                string siteKey = row.Cells[colSiteName.Index].Value?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(siteKey))
                    continue;

                if (!availability.TryGetValue(siteKey, out var roles))
                    continue;

                // EAP == CaseInfoSearch
                var eapCell = row.Cells[colEAP.Index] as DataGridViewCheckBoxCell;
                SetCheckboxReadOnly(eapCell, !roles.HasCIS);

                if (!roles.HasCIS)
                    row.Cells[colEAP.Index].Value = false;

                // eSubpoena
                var esubCell = row.Cells[colESub.Index] as DataGridViewCheckBoxCell;
                SetCheckboxReadOnly(esubCell, !roles.HasESub);

                if (!roles.HasESub)
                    row.Cells[colESub.Index].Value = false;
            }

            // ---- local helpers ----
            static Dictionary<string, (bool HasCIS, bool HasESub, bool HasDA)> BuildAvailability(IEnumerable<string> allPools)
            {
                var dict = new Dictionary<string, (bool HasCIS, bool HasESub, bool HasDA)>(StringComparer.OrdinalIgnoreCase);

                foreach (var pool in allPools)
                {
                    if (string.IsNullOrWhiteSpace(pool))
                        continue;

                    string key = pool;
                    bool hasCis = false, hasESub = false, hasDa = false;

                    if (pool.EndsWith("CaseInfoSearch", StringComparison.OrdinalIgnoreCase))
                    {
                        key = pool.Substring(0, pool.Length - "CaseInfoSearch".Length);
                        hasCis = true;
                    }
                    else if (pool.EndsWith("eSubpoena", StringComparison.OrdinalIgnoreCase))
                    {
                        key = pool.Substring(0, pool.Length - "eSubpoena".Length);
                        hasESub = true;
                    }
                    else if (pool.EndsWith("PBKDataAccess", StringComparison.OrdinalIgnoreCase))
                    {
                        key = pool.Substring(0, pool.Length - "PBKDataAccess".Length);
                        hasDa = true;
                    }
                    else if (pool.EndsWith("DataAccess", StringComparison.OrdinalIgnoreCase))
                    {
                        key = pool.Substring(0, pool.Length - "DataAccess".Length);
                        hasDa = true;
                    }
                    else
                    {
                        if (!dict.ContainsKey(key))
                            dict[key] = (HasCIS: false, HasESub: false, HasDA: false);
                        continue;
                    }

                    if (!dict.TryGetValue(key, out var existing))
                        existing = (HasCIS: false, HasESub: false, HasDA: false);

                    dict[key] = (
                        HasCIS: existing.HasCIS || hasCis,
                        HasESub: existing.HasESub || hasESub,
                        HasDA: existing.HasDA || hasDa
                    );
                }

                return dict;
            }

            static void SetCheckboxReadOnly(DataGridViewCheckBoxCell? cell, bool makeReadOnly)
            {
                if (cell == null) return;

                var grid = cell.DataGridView;
                cell.ReadOnly = makeReadOnly;

                var style = new DataGridViewCellStyle(cell.Style);
                if (makeReadOnly)
                {
                    style.BackColor = SystemColors.ControlLight;
                    style.ForeColor = SystemColors.GrayText;
                    style.SelectionBackColor = SystemColors.ControlLight;
                    style.SelectionForeColor = SystemColors.GrayText;

                    cell.ThreeState = false;
                    cell.Value = false;
                }
                else if (grid != null)
                {
                    style.BackColor = grid.DefaultCellStyle.BackColor;
                    style.ForeColor = grid.DefaultCellStyle.ForeColor;
                    style.SelectionBackColor = grid.DefaultCellStyle.SelectionBackColor;
                    style.SelectionForeColor = grid.DefaultCellStyle.SelectionForeColor;
                }

                cell.Style = style;
            }
        }

        // ------------------------
        // Live log API
        // ------------------------

        public void SwitchToLogView(string header)
        {
            // hide the grid, show log panel
            if (dgvSummary.Visible) dgvSummary.Visible = false;

            if (pnlLog != null)
            {
                lblLogHeader.Text = string.IsNullOrWhiteSpace(header) ? "Update log" : header;
                pnlLog.Visible = true;
                pnlLog.BringToFront();
            }

            // hide edit/confirm in log mode; show Abort; keep Cancel visible
            btnConfirm.Visible = false;
            btnUnlock.Visible = false;

            if (btnAbort != null)
            {
                btnAbort.Enabled = true;
                btnAbort.Visible = true;
                btnAbort.BringToFront();
            }

            AcceptButton = null;           // no default Enter action in log view
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

        // ------------------------
        // Phase tracking + abort helpers
        // ------------------------

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

        public void LeavePhase()
        {
            _currentPhase = UpdatePhase.None;
        }

        public bool ShouldSkipExternalsFor(string siteName)
        {
            if (!_abortRequested) return false;
            if (_abortMode != AbortMode.SkipExternalsAndRestAfterCurrentProd) return false;
            return string.Equals(_currentSite, siteName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public bool ShouldStopAfterExternals(string siteName)
        {
            if (!_abortRequested) return false;
            if (_abortMode != AbortMode.SkipRestAfterCurrentExternals) return false;
            return string.Equals(_currentSite, siteName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsAbortRequested => _abortRequested;
    }
}
