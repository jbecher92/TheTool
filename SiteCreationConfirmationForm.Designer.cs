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

            SuspendLayout();

            // lblPreview
            lblPreview.AutoSize = true;
            lblPreview.Location = new Point(12, 9);
            lblPreview.Name = "lblPreview";
            lblPreview.Size = new Size(126, 25);
            lblPreview.TabIndex = 0;
            lblPreview.Text = "Sites to Create";

            // dgvPreview
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.AllowUserToResizeRows = false;
            dgvPreview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPreview.Columns.AddRange(new DataGridViewColumn[] {
                colSiteName,
                colPath
            });
            dgvPreview.Location = new Point(12, 37);
            dgvPreview.Name = "dgvPreview";
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.RowHeadersWidth = 62;
            dgvPreview.RowTemplate.Height = 33;
            dgvPreview.Size = new Size(1264, 175);
            dgvPreview.TabIndex = 1;

            // colSiteName
            colSiteName.HeaderText = "Site Name";
            colSiteName.Name = "colSiteName";
            colSiteName.Width = 300;

            // colPath
            colPath.HeaderText = "Path";
            colPath.Name = "colPath";
            colPath.Width = 900;

            // btnConfirm
            btnConfirm.Location = new Point(1054, 375);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new Size(90, 34);
            btnConfirm.TabIndex = 2;
            btnConfirm.Text = "Confirm";
            btnConfirm.Click += btnConfirm_Click;

            // btnCancel
            btnCancel.Location = new Point(1150, 375);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(90, 34);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "Cancel";
            btnCancel.Click += btnCancel_Click;

            // SiteCreationConfirmationForm
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1288, 421);
            Controls.Add(lblPreview);
            Controls.Add(dgvPreview);
            Controls.Add(btnConfirm);
            Controls.Add(btnCancel);
            Name = "SiteCreationConfirmationForm";
            Text = "Confirm Site Creation";
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
