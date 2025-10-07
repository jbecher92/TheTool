using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TheTool
{
    public partial class SiteSelectorPanel : UserControl
    {
        // prevents recursive event loops when we set cells programmatically
        private bool _suppressCascade;

        public SiteSelectorPanel()
        {
            InitializeComponent();

            // ensure edit commits so CellValueChanged fires immediately for checkboxes
            dgvSites.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvSites.IsCurrentCellDirty)
                    dgvSites.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            dgvSites.CellValueChanged += dgvSites_CellValueChanged;
            dgvSites.CellContentClick += dgvSites_CellContentClick;
        }

        /// Loads a list of site names into the grid.
        public void LoadSites(IEnumerable<string> siteNames)
        {
            dgvSites.Rows.Clear();

            foreach (string site in siteNames)
            {
                // colSelected (parent), colSiteName (text), colProd, colExt, colESub
                dgvSites.Rows.Add(false, site, false, false, false);
            }
        }

        /// Returns selected sites and their corresponding update flags.
        public List<(string SiteName, bool Prod, bool EAP, bool eSub)> GetSelectedSites()
        {
            var selected = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>();

            foreach (DataGridViewRow row in dgvSites.Rows)
            {
                if (row.IsNewRow) continue; // avoid placeholder row

                bool parentChecked = ToBool(row, "colSelected");
                if (!parentChecked) continue;

                string siteName = row.Cells["colSiteName"]?.Value?.ToString() ?? string.Empty;
                bool prod = ToBool(row, "colProd");

                // Prefer the dedicated EAP column if it exists; otherwise fall back to legacy "colExt"
                bool eap = dgvSites.Columns.Contains("colEAP")
                    ? ToBool(row, "colEAP")
                    : ToBool(row, "colExt");

                bool esub = ToBool(row, "colESub");

                selected.Add((siteName, prod, eap, esub));
            }

            return selected;

            // --- local helpers ---
            static bool ToBool(DataGridViewRow r, string colName)
            {
                object? v = null;
                try { v = r.Cells[colName]?.Value; } catch { }
                return v switch
                {
                    bool b => b,
                    CheckState cs => cs == CheckState.Checked,
                    _ => Convert.ToBoolean(v ?? false)
                };
            }

        }

        //public List<(string SiteName, bool Prod, bool EAP, bool eSub)> GetSelectedSites()
        //{
        //    var selected = new List<(string SiteName, bool Prod, bool EAP, bool eSub)>();

        //    foreach (DataGridViewRow row in dgvSites.Rows)
        //    {
        //        bool parentChecked = Convert.ToBoolean(row.Cells["colSelected"].Value ?? false);
        //        if (!parentChecked) continue;

        //        string siteName = row.Cells["colSiteName"].Value?.ToString() ?? string.Empty;
        //        bool prod = Convert.ToBoolean(row.Cells["colProd"].Value ?? false);
        //        bool ext = Convert.ToBoolean(row.Cells["colExt"].Value ?? false);
        //        bool esub = Convert.ToBoolean(row.Cells["colESub"].Value ?? false);

        //        selected.Add((siteName, prod, ext, esub));
        //    }

        //    return selected;
        //}

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

        // When any cell value changes, handle parent/children syncing.
        private void dgvSites_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_suppressCascade) return;

            var grid = dgvSites;
            var row = grid.Rows[e.RowIndex];
            string colName = grid.Columns[e.ColumnIndex].Name;

            // If the parent checkbox changed, mirror to all children.
            if (colName == "colSelected")
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colSelected"].Value ?? false);
                try
                {
                    _suppressCascade = true; // avoid re-entrancy while we set child cells
                    row.Cells["colProd"].Value = isChecked;
                    row.Cells["colExt"].Value = isChecked;
                    row.Cells["colESub"].Value = isChecked;
                }
                finally
                {
                    _suppressCascade = false;
                }
                return;
            }

            // If any child changed, update the parent to reflect OR of children.
            if (colName == "colProd" || colName == "colExt" || colName == "colESub")
            {
                bool prodChecked = Convert.ToBoolean(row.Cells["colProd"].Value ?? false);
                bool eapChecked = Convert.ToBoolean(row.Cells["colExt"].Value ?? false);
                bool esubChecked = Convert.ToBoolean(row.Cells["colESub"].Value ?? false);

                try
                {
                    _suppressCascade = true;
                    row.Cells["colSelected"].Value = prodChecked || eapChecked || esubChecked;
                }
                finally
                {
                    _suppressCascade = false;
                }
            }
        }

        public void ClearSelections()
        {
            foreach (DataGridViewRow row in dgvSites.Rows)
            {
                row.Cells["colProd"].Value = false;
                row.Cells["colEAP"].Value = false;
                row.Cells["colESub"].Value = false;
            }

            dgvSites.ClearSelection();
        }

        private void dgvSites_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvSites.IsCurrentCellDirty)
            {
                dgvSites.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // checkbox/build validation for child role columns
        private void dgvSites_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string colName = dgvSites.Columns[e.ColumnIndex].Name;

            if (colName == "colProd" || colName == "colExt" || colName == "colESub")
            {
                string type = colName switch
                {
                    "colProd" => "Prod",
                    "colExt" => "EAP",
                    "colESub" => "eSub",
                    _ => ""
                };

                if (ZipValidationFunc != null && !ZipValidationFunc(type))
                {
                    MessageBox.Show($"A valid .zip file must be selected for {type}.",
                        "Missing ZIP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    dgvSites.CancelEdit();
                }
            }
        }

        public Func<string, bool> ZipValidationFunc { get; set; } = _ => true;
    }
}
