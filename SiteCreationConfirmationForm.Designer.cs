namespace TheTool
{
    partial class SiteCreationConfirmationForm
    {
        private System.ComponentModel.IContainer components = null;
        private Label lblPreview;
        private DataGridView dgvPreview;
        private Button btnConfirm;
        private Button btnCancel;
        private DataGridViewTextBoxColumn colSiteName;
        private DataGridViewTextBoxColumn colPath;

        private void InitializeComponent()
        {
            lblPreview = new Label();
            dgvPreview = new DataGridView();
            colSiteName = new DataGridViewTextBoxColumn();
            colPath = new DataGridViewTextBoxColumn();
            btnConfirm = new Button();
            btnCancel = new Button();
            ((System.ComponentModel.ISupportInitialize)dgvPreview).BeginInit();
            SuspendLayout();
            // 
            // lblPreview
            // 
            lblPreview.AutoSize = true;
            lblPreview.Location = new Point(10, 7);
            lblPreview.Margin = new Padding(2, 0, 2, 0);
            lblPreview.Name = "lblPreview";
            lblPreview.Size = new Size(105, 20);
            lblPreview.TabIndex = 0;
            lblPreview.Text = "Sites to Create";
            // 
            // dgvPreview
            // 
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.AllowUserToResizeRows = false;
            dgvPreview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPreview.Columns.AddRange(new DataGridViewColumn[] { colSiteName, colPath });
            dgvPreview.Location = new Point(10, 30);
            dgvPreview.Margin = new Padding(2, 2, 2, 2);
            dgvPreview.Name = "dgvPreview";
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.RowHeadersWidth = 62;
            dgvPreview.RowTemplate.Height = 33;
            dgvPreview.Size = new Size(1011, 266);
            dgvPreview.TabIndex = 1;
            // 
            // colSiteName
            // 
            colSiteName.HeaderText = "Site Name";
            colSiteName.MinimumWidth = 6;
            colSiteName.Name = "colSiteName";
            colSiteName.Width = 300;
            // 
            // colPath
            // 
            colPath.HeaderText = "Path";
            colPath.MinimumWidth = 6;
            colPath.Name = "colPath";
            colPath.Width = 900;
            // 
            // btnConfirm
            // 
            btnConfirm.Location = new Point(843, 300);
            btnConfirm.Margin = new Padding(2, 2, 2, 2);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new Size(72, 27);
            btnConfirm.TabIndex = 2;
            btnConfirm.Text = "Confirm";
            btnConfirm.Click += btnConfirm_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(920, 300);
            btnCancel.Margin = new Padding(2, 2, 2, 2);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(72, 27);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "Cancel";
            btnCancel.Click += btnCancel_Click;
            // 
            // SiteCreationConfirmationForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1030, 337);
            Controls.Add(lblPreview);
            Controls.Add(dgvPreview);
            Controls.Add(btnConfirm);
            Controls.Add(btnCancel);
            Margin = new Padding(2, 2, 2, 2);
            Name = "SiteCreationConfirmationForm";
            Text = "Confirm Site Creation";
            ((System.ComponentModel.ISupportInitialize)dgvPreview).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }
    }
}
