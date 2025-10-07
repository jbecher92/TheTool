namespace TheTool
{
    partial class ConfirmationForm
    {
       // private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.DataGridView dgvSummary;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnUnlock;

        private void InitializeComponent()
        {
            dgvSummary = new DataGridView();
            colSiteName = new DataGridViewTextBoxColumn();
            colProd = new DataGridViewCheckBoxColumn();
            colEAP = new DataGridViewCheckBoxColumn();
            colESub = new DataGridViewCheckBoxColumn();
            btnConfirm = new Button();
            btnCancel = new Button();
            btnUnlock = new Button();
            btnAbort = new Button();
            ((System.ComponentModel.ISupportInitialize)dgvSummary).BeginInit();
            SuspendLayout();
            // 
            // dgvSummary
            // 
            dgvSummary.AllowUserToAddRows = false;
            dgvSummary.AllowUserToDeleteRows = false;
            dgvSummary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSummary.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvSummary.Columns.AddRange(new DataGridViewColumn[] { colSiteName, colProd, colEAP, colESub });
            dgvSummary.Location = new Point(12, 12);
            dgvSummary.Name = "dgvSummary";
            dgvSummary.ReadOnly = true;
            dgvSummary.RowHeadersVisible = false;
            dgvSummary.RowHeadersWidth = 51;
            dgvSummary.Size = new Size(560, 300);
            dgvSummary.TabIndex = 0;
            // 
            // colSiteName
            // 
            colSiteName.HeaderText = "Site Name";
            colSiteName.MinimumWidth = 6;
            colSiteName.Name = "colSiteName";
            colSiteName.ReadOnly = true;
            // 
            // colProd
            // 
            colProd.HeaderText = "Prod";
            colProd.MinimumWidth = 6;
            colProd.Name = "colProd";
            colProd.ReadOnly = true;
            // 
            // colEAP
            // 
            colEAP.HeaderText = "EAP";
            colEAP.MinimumWidth = 6;
            colEAP.Name = "colEAP";
            colEAP.ReadOnly = true;
            // 
            // colESub
            // 
            colESub.HeaderText = "eSub";
            colESub.MinimumWidth = 6;
            colESub.Name = "colESub";
            colESub.ReadOnly = true;
            // 
            // btnConfirm
            // 
            btnConfirm.Location = new Point(392, 320);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new Size(80, 30);
            btnConfirm.TabIndex = 1;
            btnConfirm.Text = "Confirm";
            btnConfirm.UseVisualStyleBackColor = true;
            btnConfirm.Click += btnConfirm_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(492, 320);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(80, 30);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // btnUnlock
            // 
            btnUnlock.Location = new Point(292, 320);
            btnUnlock.Name = "btnUnlock";
            btnUnlock.Size = new Size(80, 30);
            btnUnlock.TabIndex = 3;
            btnUnlock.Text = "Edit";
            btnUnlock.UseVisualStyleBackColor = true;
            btnUnlock.Click += btnUnlock_Click;
            // 
            // btnAbort
            // 
            btnAbort.Anchor = AnchorStyles.Bottom;
            btnAbort.Location = new Point(12, 318);
            btnAbort.Name = "btnAbort";
            btnAbort.Size = new Size(80, 30);
            btnAbort.TabIndex = 4;
            btnAbort.Text = "Abort";
            btnAbort.UseVisualStyleBackColor = true;
            btnAbort.Visible = false;
            btnAbort.Click += btnAbort_Click;
            // 
            // ConfirmationForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(584, 361);
            Controls.Add(btnAbort);
            Controls.Add(dgvSummary);
            Controls.Add(btnUnlock);
            Controls.Add(btnConfirm);
            Controls.Add(btnCancel);
            MinimumSize = new Size(600, 400);
            Name = "ConfirmationForm";
            Text = "Confirm Selected Sites";
            ((System.ComponentModel.ISupportInitialize)dgvSummary).EndInit();
            ResumeLayout(false);
        }

        private System.Windows.Forms.DataGridViewTextBoxColumn colSiteName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colProd;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colEAP;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colESub;
        private Button btnAbort;
    }
}
