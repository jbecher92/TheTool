using System;
using System.Drawing;
using System.Windows.Forms;

namespace TheTool
{
    partial class ConfirmationForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.DataGridView dgvSummary;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnUnlock;
        private System.Windows.Forms.Button btnAbort;
        private Label label1;
        private CheckBox tailProd;
        private CheckBox tailExt;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

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
            label1 = new Label();
            tailProd = new CheckBox();
            tailExt = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)dgvSummary).BeginInit();
            SuspendLayout();
            // 
            // dgvSummary
            // 
            dgvSummary.AllowUserToAddRows = false;
            dgvSummary.AllowUserToDeleteRows = false;
            dgvSummary.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvSummary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSummary.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvSummary.Columns.AddRange(new DataGridViewColumn[] { colSiteName, colProd, colEAP, colESub });
            dgvSummary.Location = new Point(11, 12);
            dgvSummary.Name = "dgvSummary";
            dgvSummary.ReadOnly = true;
            dgvSummary.RowHeadersVisible = false;
            dgvSummary.RowHeadersWidth = 51;
            dgvSummary.Size = new Size(1266, 565);
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
            btnConfirm.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnConfirm.Location = new Point(1023, 587);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new Size(80, 29);
            btnConfirm.TabIndex = 1;
            btnConfirm.Text = "Confirm";
            btnConfirm.UseVisualStyleBackColor = true;
            btnConfirm.Click += btnConfirm_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(1199, 587);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(80, 29);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // btnUnlock
            // 
            btnUnlock.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnUnlock.Location = new Point(1111, 587);
            btnUnlock.Name = "btnUnlock";
            btnUnlock.Size = new Size(80, 29);
            btnUnlock.TabIndex = 3;
            btnUnlock.Text = "Edit";
            btnUnlock.UseVisualStyleBackColor = true;
            btnUnlock.Click += btnUnlock_Click;
            // 
            // btnAbort
            // 
            btnAbort.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAbort.Location = new Point(1199, 587);
            btnAbort.Name = "btnAbort";
            btnAbort.Size = new Size(80, 29);
            btnAbort.TabIndex = 4;
            btnAbort.Text = "Abort";
            btnAbort.UseVisualStyleBackColor = true;
            btnAbort.Visible = false;
            btnAbort.Click += btnAbort_Click;
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            label1.AutoSize = true;
            label1.Location = new Point(11, 591);
            label1.Name = "label1";
            label1.Size = new Size(0, 20);
            label1.TabIndex = 5;
            label1.Click += label1_Click;
            // 
            // tailProd
            // 
            tailProd.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            tailProd.AutoSize = true;
            tailProd.Checked = true;
            tailProd.Location = new Point(11, 589);
            tailProd.Name = "tailProd";
            tailProd.Size = new Size(292, 24);
            tailProd.TabIndex = 8;
            tailProd.Text = "Production web.config tail replacement";
            tailProd.UseVisualStyleBackColor = true;
            // 
            // tailExt
            // 
            tailExt.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            tailExt.AutoSize = true;
            tailExt.Checked = true;
            tailExt.CheckState = CheckState.Checked;
            tailExt.Location = new Point(319, 589);
            tailExt.Name = "tailExt";
            tailExt.Size = new Size(179, 24);
            tailExt.TabIndex = 9;
            tailExt.Text = "NEW External changes";
            tailExt.UseVisualStyleBackColor = true;
            // 
            // ConfirmationForm
            // 
            AcceptButton = btnConfirm;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new Size(1290, 627);
            Controls.Add(tailExt);
            Controls.Add(tailProd);
            Controls.Add(label1);
            Controls.Add(dgvSummary);
            Controls.Add(btnUnlock);
            Controls.Add(btnConfirm);
            Controls.Add(btnCancel);
            Controls.Add(btnAbort);
            MinimumSize = new Size(600, 398);
            Name = "ConfirmationForm";
            Text = "Confirm Selected Sites";
            ((System.ComponentModel.ISupportInitialize)dgvSummary).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
        private DataGridViewTextBoxColumn colSiteName;
        //private DataGridViewCheckBoxColumn coleApi;
        private DataGridViewCheckBoxColumn colProd;
        private DataGridViewCheckBoxColumn colEAP;
        private DataGridViewCheckBoxColumn colESub;
    }
}
