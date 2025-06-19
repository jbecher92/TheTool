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

        /// <summary>
        /// Loads a list of site names into the grid.
        /// </summary>
        public void LoadSites(IEnumerable<string> siteNames)
        {
            dgvSites.Rows.Clear();

            foreach (string site in siteNames)
            {
                dgvSites.Rows.Add(false, site, false, false, false);
            }
        }

        /// <summary>
        /// Returns selected sites and their corresponding update flags.
        /// </summary>
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

        /// <summary>
        /// Filters the grid rows based on the text input.
        /// </summary>
        private void TxtFilter_TextChanged(object sender, EventArgs e)
        {
            string filter = txtFilter.Text.Trim().ToLower();

            foreach (DataGridViewRow row in dgvSites.Rows)
            {
                string siteName = row.Cells["colSiteName"].Value?.ToString()?.ToLower() ?? "";
                row.Visible = siteName.Contains(filter);
            }
        }
    }
}