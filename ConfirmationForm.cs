using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TheTool
{
    public partial class ConfirmationForm : Form
    {
        public bool Confirmed { get; private set; } = false;
              
        public ConfirmationForm(List<(string SiteName, bool Prod, bool EAP, bool eSub)> selectedSites)
        {
            InitializeComponent();

            foreach (var (site, prod, eap, esub) in selectedSites)
            {
                dgvSummary.Rows.Add(site, prod, eap, esub);
            }
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            Confirmed = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Confirmed = false;
            this.DialogResult = DialogResult.Cancel;
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
            var updatedSelections = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>();

            foreach (DataGridViewRow row in dgvSummary.Rows)
            {
                string siteName = row.Cells["colSiteName"].Value?.ToString() ?? string.Empty;
                bool prod = Convert.ToBoolean(row.Cells["colProd"].Value ?? false);
                bool eap = Convert.ToBoolean(row.Cells["colEAP"].Value ?? false);
                bool esub = Convert.ToBoolean(row.Cells["colESub"].Value ?? false);

                updatedSelections.Add((siteName, prod, eap, esub));
            }

            return updatedSelections;
        }



    }
}
