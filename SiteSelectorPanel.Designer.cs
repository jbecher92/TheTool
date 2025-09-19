namespace TheTool
{
    partial class SiteSelectorPanel
    {
        private System.Windows.Forms.TextBox txtFilter;
        private System.Windows.Forms.DataGridView dgvSites;

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
            colESub = new DataGridViewCheckBoxColumn();
            ((System.ComponentModel.ISupportInitialize)dgvSites).BeginInit();
            SuspendLayout();
            // 
            // txtFilter
            // 
            txtFilter.Font = new Font("Segoe UI", 10F);
            txtFilter.Location = new Point(3, 3);
            txtFilter.Name = "txtFilter";
            txtFilter.PlaceholderText = "Enter letter number";
            txtFilter.Size = new Size(491, 30);
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
            dgvSites.Columns.AddRange(new DataGridViewColumn[] { colSelected, colSiteName, colProd, colExt, colESub });
            dgvSites.EnableHeadersVisualStyles = false;
            dgvSites.Font = new Font("Segoe UI", 10F);
            dgvSites.GridColor = Color.LightGray;
            dgvSites.Location = new Point(3, 43);
            dgvSites.MultiSelect = false;
            dgvSites.Name = "dgvSites";
            dgvSites.RowHeadersVisible = false;
            dgvSites.RowHeadersWidth = 62;
            dgvSites.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSites.Size = new Size(641, 506);
            dgvSites.TabIndex = 0;
            dgvSites.CellValueChanged += dgvSites_CellValueChanged;
            dgvSites.CurrentCellDirtyStateChanged += dgvSites_CurrentCellDirtyStateChanged;
            // 
            // colSelected
            // 
            colSelected.FillWeight = 20F;
            colSelected.HeaderText = "";
            colSelected.MinimumWidth = 8;
            colSelected.Name = "colSelected";
            // 
            // colSiteName
            // 
            colSiteName.FillWeight = 150F;
            colSiteName.HeaderText = "Site Name";
            colSiteName.MinimumWidth = 8;
            colSiteName.Name = "colSiteName";
            // 
            // colProd
            // 
            colProd.FillWeight = 30F;
            colProd.HeaderText = "Prod";
            colProd.MinimumWidth = 8;
            colProd.Name = "colProd";
            colProd.Resizable = DataGridViewTriState.False;
            // 
            // colExt
            // 
            colExt.FillWeight = 30F;
            colExt.HeaderText = "EAP";
            colExt.MinimumWidth = 8;
            colExt.Name = "colExt";
            colExt.Resizable = DataGridViewTriState.False;
            // 
            // colESub
            // 
            colESub.FillWeight = 30F;
            colESub.HeaderText = "eSub";
            colESub.MinimumWidth = 8;
            colESub.Name = "colESub";
            colESub.Resizable = DataGridViewTriState.False;
            // 
            // SiteSelectorPanel
            // 
            Controls.Add(txtFilter);
            Controls.Add(dgvSites);
            MinimumSize = new Size(600, 400);
            Name = "SiteSelectorPanel";
            Size = new Size(647, 552);
            ((System.ComponentModel.ISupportInitialize)dgvSites).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
        private DataGridViewCheckBoxColumn colSelected;
        private DataGridViewTextBoxColumn colSiteName;
        private DataGridViewCheckBoxColumn colProd;
        private DataGridViewCheckBoxColumn colExt;
        private DataGridViewCheckBoxColumn colESub;
    }
}
