using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace TheTool
{
    public partial class SiteSelectorPanel : UserControl
    {
        public SiteSelectorPanel()
        {
            InitializeComponent();
        }

        /// Loads a list of site names into the grid.
        public void LoadSites(IEnumerable<string> siteNames)
        {
            dgvSites.Rows.Clear();

            foreach (string site in siteNames)
            {
                dgvSites.Rows.Add(false, site, false, false, false);
            }
        }

        /// Returns selected sites and their corresponding update flags.
        public List<(string SiteName, bool Prod, bool EAP, bool eSub)> GetSelectedSites()
        {
            var selected = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>();

            foreach (DataGridViewRow row in dgvSites.Rows)
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colSelected"].Value ?? false);
                if (!isChecked) continue;

                string siteName = row.Cells["colSiteName"].Value?.ToString() ?? string.Empty;
                bool prod = Convert.ToBoolean(row.Cells["colProd"].Value ?? false);
                bool ext = Convert.ToBoolean(row.Cells["colExt"].Value ?? false);
                bool other = Convert.ToBoolean(row.Cells["colOther"].Value ?? false);

                selected.Add((siteName, prod, ext, other));
            }

            return selected;
        }

        /// Filters the grid rows based on the text input.
        private void TxtFilter_TextChanged(object sender, EventArgs e)
        {
            string filter = txtFilter.Text.Trim().ToLower();

            foreach (DataGridViewRow row in dgvSites.Rows)
            {
                string siteName = row.Cells["colSiteName"].Value?.ToString()?.ToLower() ?? "";
                row.Visible = siteName.Contains(filter);
            }
        }

        //event handler for checkboxes in the IIS list
        private void dgvSites_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dgvSites.Rows[e.RowIndex];

            bool prodChecked = Convert.ToBoolean(row.Cells["colProd"].Value ?? false);
            bool eapChecked = Convert.ToBoolean(row.Cells["colExt"].Value ?? false);
            bool esubChecked = Convert.ToBoolean(row.Cells["colOther"].Value ?? false);

            row.Cells["colSelected"].Value = prodChecked || eapChecked || esubChecked;
        }

        private void dgvSites_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvSites.IsCurrentCellDirty)
            {
                dgvSites.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        //checkbox/build validation
        private void dgvSites_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string colName = dgvSites.Columns[e.ColumnIndex].Name;

            if (colName == "colProd" || colName == "colExt" || colName == "colOther")
            {
                string type = colName switch
                {
                    "colProd" => "Prod",
                    "colExt" => "EAP",
                    "colOther" => "eSub", 
                    _ => ""
                };

                if (ZipValidationFunc != null && !ZipValidationFunc(type))
                {
                    MessageBox.Show($"A valid .zip file must be selected for {type}.", "Missing ZIP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    dgvSites.CancelEdit();
                }
            }
        }

        public Func<string, bool> ZipValidationFunc { get; set; } = _ => true;
    }
}