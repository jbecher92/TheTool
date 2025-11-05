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

        //private static void SetCheckboxIfWritable(DataGridViewRow row, string columnName, bool value)
        //{
        //    if (!row.DataGridView.Columns.Contains(columnName)) return;

        //    if (row.Cells[columnName] is DataGridViewCheckBoxCell cell)
        //    {
        //        if (cell.ReadOnly)
        //        {
        //            // enforce unchecked for disabled cells
        //            if (!Equals(cell.Value, false))
        //                cell.Value = false;
        //            return;
        //        }
        //        cell.Value = value;
        //    }
        //}


        public void ApplyRoleAvailability(Dictionary<string, (bool HasCIS, bool HasESub, bool HasDA)> availability)
        {
            if (availability == null || availability.Count == 0) return;

            foreach (DataGridViewRow row in dgvSites.Rows)
            {
                var key = row.Cells["colSiteName"].Value?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (!availability.TryGetValue(key, out var roles)) continue;

                // Columns assumed: colExt == CaseInfoSearch, colESub == eSubpoena.
                // If you have a dedicated DA column, handle it similarly; if not, you can ignore DA.
                SetCheckboxReadOnly(row.Cells["colExt"] as DataGridViewCheckBoxCell, !roles.HasCIS);
                SetCheckboxReadOnly(row.Cells["colESub"] as DataGridViewCheckBoxCell, !roles.HasESub);

                // OPTIONAL: If you show DA as a separate checkbox column (e.g., colDA),
                // uncomment this line and ensure the column exists.
                // SetCheckboxReadOnly(row.Cells["colDA"]   as DataGridViewCheckBoxCell,  !roles.HasDA);
            }
        }

        // Small helper to visually gray-out and disable a checkbox cell
        private static void SetCheckboxReadOnly(DataGridViewCheckBoxCell? cell, bool makeReadOnly)
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
                // use the grid’s default colors when re-enabling
                style.BackColor = grid.DefaultCellStyle.BackColor;
                style.ForeColor = grid.DefaultCellStyle.ForeColor;
                style.SelectionBackColor = grid.DefaultCellStyle.SelectionBackColor;
                style.SelectionForeColor = grid.DefaultCellStyle.SelectionForeColor;
            }

            cell.Style = style;
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

        // When any cell value changes, handle parent/children syncing.
        private void dgvSites_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_suppressCascade) return;

            var grid = dgvSites;
            var row = grid.Rows[e.RowIndex];
            string colName = grid.Columns[e.ColumnIndex].Name;

            // Helper for respecting ReadOnly state
            void SetCheckboxIfWritable(string columnName, bool value)
            {
                if (!grid.Columns.Contains(columnName)) return;
                if (row.Cells[columnName] is DataGridViewCheckBoxCell cell)
                {
                    if (cell.ReadOnly)
                    {
                        // Ensure disabled boxes stay unchecked
                        if (!Equals(cell.Value, false))
                            cell.Value = false;
                        return;
                    }
                    cell.Value = value;
                }
            }

            // -------------------------------
            // If the parent checkbox changed
            // -------------------------------
            if (colName == "colSelected")
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colSelected"].Value ?? false);
                try
                {
                    _suppressCascade = true; // avoid re-entrancy while we set child cells
                    SetCheckboxIfWritable("colProd", isChecked);
                    SetCheckboxIfWritable("colExt", isChecked);
                    SetCheckboxIfWritable("colESub", isChecked);
                }
                finally
                {
                    _suppressCascade = false;
                }
                return;
            }

            // -------------------------------
            // If any child changed
            // -------------------------------
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
