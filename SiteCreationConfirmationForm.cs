using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TheTool
{
    public partial class SiteCreationConfirmationForm : Form
    {
        public bool Confirmed { get; private set; } = false;

    
        public SiteCreationConfirmationForm(List<(string Label, string Path)> sitesToCreate)
        {
            InitializeComponent();
            PopulateGrid(sitesToCreate);
        }

        private void PopulateGrid(List<(string Label, string Path)> entries)
        {
            dgvPreview.Rows.Clear();
            foreach (var (label, path) in entries)
                dgvPreview.Rows.Add(label, path);
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            Confirmed = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Confirmed = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
