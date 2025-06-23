namespace TheTool
{
    partial class SiteSelectorPanel
    {
        private System.Windows.Forms.TextBox txtFilter;
        private System.Windows.Forms.DataGridView dgvSites;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colSelected;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSiteName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colProd;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colExt;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colOther;

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            txtFilter = new TextBox();
            dgvSites = new DataGridView();
            colSelected = new DataGridViewCheckBoxColumn();
            colSiteName = new DataGridViewTextBoxColumn();
            colProd = new DataGridViewCheckBoxColumn();
            colExt = new DataGridViewCheckBoxColumn();
            colOther = new DataGridViewCheckBoxColumn();
            ((System.ComponentModel.ISupportInitialize)dgvSites).BeginInit();
            SuspendLayout();
            // 
            // txtFilter
            // 
            txtFilter.Font = new Font("Segoe UI", 10F);
            txtFilter.Location = new Point(3, 3);
            txtFilter.Name = "txtFilter";
            txtFilter.PlaceholderText = "Enter letter number";
            txtFilter.Size = new Size(491, 34);
            txtFilter.TabIndex = 1;
            txtFilter.TextChanged += TxtFilter_TextChanged;
            // 
            // dgvSites
            // 
            dgvSites.AllowUserToAddRows = false;
            dgvSites.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(245, 245, 245);
            dgvSites.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvSites.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSites.BorderStyle = BorderStyle.None;
            dgvSites.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.LightGray;
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dataGridViewCellStyle2.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.True;
            dgvSites.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvSites.ColumnHeadersHeight = 34;
            dgvSites.Columns.AddRange(new DataGridViewColumn[] { colSelected, colSiteName, colProd, colExt, colOther });
            dgvSites.EnableHeadersVisualStyles = false;
            dgvSites.Font = new Font("Segoe UI", 10F);
            dgvSites.GridColor = Color.LightGray;
            dgvSites.Location = new Point(3, 43);
            dgvSites.MultiSelect = false;
            dgvSites.Name = "dgvSites";
            dgvSites.RowHeadersVisible = false;
            dgvSites.RowHeadersWidth = 62;
            dgvSites.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSites.Size = new Size(806, 656);
            dgvSites.TabIndex = 0;
            dgvSites.CellValueChanged += dgvSites_CellValueChanged;
            dgvSites.CurrentCellDirtyStateChanged += dgvSites_CurrentCellDirtyStateChanged;
            // 
            // colSelected
            // 
            colSelected.FillWeight = 10F;
            colSelected.HeaderText = "";
            colSelected.MinimumWidth = 8;
            colSelected.Name = "colSelected";
            colSelected.ReadOnly = true;
            // 
            // colSiteName
            // 
            colSiteName.FillWeight = 60F;
            colSiteName.HeaderText = "Site Name";
            colSiteName.MinimumWidth = 8;
            colSiteName.Name = "colSiteName";
            colSiteName.ReadOnly = true;
            // 
            // colProd
            // 
            colProd.FillWeight = 10F;
            colProd.HeaderText = "Prod";
            colProd.MinimumWidth = 8;
            colProd.Name = "colProd";
            // 
            // colExt
            // 
            colExt.FillWeight = 10F;
            colExt.HeaderText = "EAP";
            colExt.MinimumWidth = 8;
            colExt.Name = "colExt";
            // 
            // colOther
            // 
            colOther.FillWeight = 10F;
            colOther.HeaderText = "eSub";
            colOther.MinimumWidth = 8;
            colOther.Name = "colOther";
            // 
            // SiteSelectorPanel
            // 
            Controls.Add(txtFilter);
            Controls.Add(dgvSites);
            MinimumSize = new Size(600, 400);
            Name = "SiteSelectorPanel";
            Size = new Size(827, 738);
            ((System.ComponentModel.ISupportInitialize)dgvSites).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
