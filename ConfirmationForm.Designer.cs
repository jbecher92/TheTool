namespace TheTool
{
    partial class ConfirmationForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.DataGridView dgvSummary;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnUnlock;

        private void InitializeComponent()
        {
            this.dgvSummary = new System.Windows.Forms.DataGridView();
            this.btnConfirm = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnUnlock = new System.Windows.Forms.Button();

            this.colSiteName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProd = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colEAP = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colESub = new System.Windows.Forms.DataGridViewCheckBoxColumn();

            ((System.ComponentModel.ISupportInitialize)(this.dgvSummary)).BeginInit();
            this.SuspendLayout();

            // dgvSummary
            this.dgvSummary.AllowUserToAddRows = false;
            this.dgvSummary.AllowUserToDeleteRows = false;
            this.dgvSummary.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvSummary.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSummary.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colSiteName,
                this.colProd,
                this.colEAP,
                this.colESub});
            this.dgvSummary.Location = new System.Drawing.Point(12, 12);
            this.dgvSummary.Name = "dgvSummary";
            this.dgvSummary.ReadOnly = true;
            this.dgvSummary.RowHeadersVisible = false;
            this.dgvSummary.Size = new System.Drawing.Size(560, 300);
            this.dgvSummary.TabIndex = 0;

            // btnUnlock
            this.btnUnlock.Location = new System.Drawing.Point(292, 320);
            this.btnUnlock.Name = "btnUnlock";
            this.btnUnlock.Size = new System.Drawing.Size(80, 30);
            this.btnUnlock.TabIndex = 3;
            this.btnUnlock.Text = "Edit";
            this.btnUnlock.UseVisualStyleBackColor = true;
            this.btnUnlock.Click += new System.EventHandler(this.btnUnlock_Click);

            // btnConfirm
            this.btnConfirm.Location = new System.Drawing.Point(392, 320);
            this.btnConfirm.Name = "btnConfirm";
            this.btnConfirm.Size = new System.Drawing.Size(80, 30);
            this.btnConfirm.TabIndex = 1;
            this.btnConfirm.Text = "Confirm";
            this.btnConfirm.UseVisualStyleBackColor = true;
            this.btnConfirm.Click += new System.EventHandler(this.btnConfirm_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(492, 320);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(80, 30);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // colSiteName
            this.colSiteName.HeaderText = "Site Name";
            this.colSiteName.Name = "colSiteName";
            this.colSiteName.ReadOnly = true;

            // colProd
            this.colProd.HeaderText = "Prod";
            this.colProd.Name = "colProd";
            this.colProd.ReadOnly = true;

            // colEAP
            this.colEAP.HeaderText = "EAP";
            this.colEAP.Name = "colEAP";
            this.colEAP.ReadOnly = true;

            // colESub
            this.colESub.HeaderText = "eSub";
            this.colESub.Name = "colESub";
            this.colESub.ReadOnly = true;

            // ConfirmationForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 361);
            this.Controls.Add(this.dgvSummary);
            this.Controls.Add(this.btnUnlock);
            this.Controls.Add(this.btnConfirm);
            this.Controls.Add(this.btnCancel);
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Name = "ConfirmationForm";
            this.Text = "Confirm Selected Sites";
            this.colProd.DefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
            this.colEAP.DefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
            this.colESub.DefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
            ((System.ComponentModel.ISupportInitialize)(this.dgvSummary)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.DataGridViewTextBoxColumn colSiteName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colProd;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colEAP;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colESub;
    }
}
