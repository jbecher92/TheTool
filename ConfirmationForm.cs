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

        // Log UI
        private Panel pnlLog;
        private Label lblLogHeader;
        private TextBox txtLog;

        // Build availability flags for later (to keep disabled even when "Edit" toggled)
        //private bool _prodAvailable;
        //private bool _eapAvailable;
        //private bool _esubAvailable;
        //private bool _dataAccessAvailable;

        // Raised when the user presses Confirm and we should start the update.
        public event Action<List<(string SiteName, bool Prod, bool EAP, bool eSub)>>? RunConfirmed;

        public ConfirmationForm(List<(string SiteName, bool Prod, bool EAP, bool eSub)> selectedSites)
        {
            InitializeComponent();
            CreateLogControls();

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

        /// <summary>
        /// Disable/enable checkbox columns based on which builds are available.
        /// - If DataAccess is missing, both EAP and eSubpoena are disabled regardless of their own zips.
        /// </summary>
        

        

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
                        ? System.Drawing.Color.LightGray
                        : System.Drawing.Color.White;
                }
            }

            btnUnlock.Text = isNowEditable ? "Lock" : "Edit";
        }

        //second validation based on edited user input
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
            // Hide the grid, show the log
            if (dgvSummary.Visible) dgvSummary.Visible = false;

            if (pnlLog != null)
            {
                lblLogHeader.Text = header ?? "Update log";
                pnlLog.Visible = true;
                pnlLog.BringToFront();
            }

            // Disable buttons except Cancel (rename to Close when done)
            btnConfirm.Enabled = false;
            btnUnlock.Enabled = false;
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
            if (!IsDisposed)
            {
                if (InvokeRequired)
                {
                    try { BeginInvoke(new Action(() => btnCancel.Text = "Close")); } catch { }
                }
                else
                {
                    btnCancel.Text = "Close";
                }
            }
        }

        private void DisableOrEnableColumn(string colName, bool enabled)
        {
            if (!dgvSummary.Columns.Contains(colName)) return;

            var col = dgvSummary.Columns[colName];
            col.ReadOnly = !enabled;
            col.DefaultCellStyle.BackColor = enabled ? Color.White : Color.LightGray;
        }

        private bool GetColumnEnabled(string colName)
        {
            if (!dgvSummary.Columns.Contains(colName)) return false;
            return !dgvSummary.Columns[colName].ReadOnly;
        }

        private void ClearColumnChecks(string colName)
        {
            foreach (DataGridViewRow row in dgvSummary.Rows)
            {
                if (row.Cells[colName] is DataGridViewCheckBoxCell cell)
                    cell.Value = false;
            }
        }
    }
}
