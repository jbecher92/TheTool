namespace TheTool
{
    partial class SiteCreatorForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblState;
        private System.Windows.Forms.TextBox txtState;
        private System.Windows.Forms.Label lblClient;
        private System.Windows.Forms.TextBox txtClient;
        private System.Windows.Forms.CheckBox chkProduction;
        private System.Windows.Forms.CheckBox chkCaseInfoSearch;
        private System.Windows.Forms.CheckBox chkESubpoena;
        //private System.Windows.Forms.CheckBox chkDataAccess;
        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.PictureBox picStateValidation;

        private void InitializeComponent()
        {
            lblState = new Label();
            txtState = new TextBox();
            lblClient = new Label();
            txtClient = new TextBox();
            chkProduction = new CheckBox();
            chkCaseInfoSearch = new CheckBox();
            chkESubpoena = new CheckBox();
            btnCreate = new Button();
            picStateValidation = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)picStateValidation).BeginInit();
            SuspendLayout();
            // 
            // lblState
            // 
            lblState.AutoSize = true;
            lblState.Location = new Point(10, 12);
            lblState.Margin = new Padding(2, 0, 2, 0);
            lblState.Name = "lblState";
            lblState.Size = new Size(43, 20);
            lblState.TabIndex = 1;
            lblState.Text = "State";
            // 
            // txtState
            // 
            txtState.Location = new Point(56, 10);
            txtState.Margin = new Padding(2, 2, 2, 2);
            txtState.Name = "txtState";
            txtState.Size = new Size(41, 27);
            txtState.TabIndex = 2;
            // 
            // lblClient
            // 
            lblClient.AutoSize = true;
            lblClient.Location = new Point(136, 12);
            lblClient.Margin = new Padding(2, 0, 2, 0);
            lblClient.Name = "lblClient";
            lblClient.Size = new Size(47, 20);
            lblClient.TabIndex = 3;
            lblClient.Text = "Client";
            // 
            // txtClient
            // 
            txtClient.Location = new Point(184, 10);
            txtClient.Margin = new Padding(2, 2, 2, 2);
            txtClient.Name = "txtClient";
            txtClient.Size = new Size(266, 27);
            txtClient.TabIndex = 4;
            // 
            // chkProduction
            // 
            chkProduction.Location = new Point(22, 76);
            chkProduction.Margin = new Padding(2, 2, 2, 2);
            chkProduction.Name = "chkProduction";
            chkProduction.Size = new Size(101, 24);
            chkProduction.TabIndex = 5;
            chkProduction.Text = "Default Site";
            // 
            // chkCaseInfoSearch
            // 
            chkCaseInfoSearch.Location = new Point(184, 76);
            chkCaseInfoSearch.Margin = new Padding(2, 2, 2, 2);
            chkCaseInfoSearch.Name = "chkCaseInfoSearch";
            chkCaseInfoSearch.Size = new Size(136, 24);
            chkCaseInfoSearch.TabIndex = 6;
            chkCaseInfoSearch.Text = "CaseInfoSearch";
            // 
            // chkESubpoena
            // 
            chkESubpoena.Location = new Point(391, 76);
            chkESubpoena.Margin = new Padding(2, 2, 2, 2);
            chkESubpoena.Name = "chkESubpoena";
            chkESubpoena.Size = new Size(128, 24);
            chkESubpoena.TabIndex = 7;
            chkESubpoena.Text = "eSubpoena";
            // 
            // btnCreate
            // 
            btnCreate.Location = new Point(586, 330);
            btnCreate.Margin = new Padding(2, 2, 2, 2);
            btnCreate.Name = "btnCreate";
            btnCreate.Size = new Size(80, 32);
            btnCreate.TabIndex = 8;
            btnCreate.Text = "Create";
            btnCreate.Click += btnCreate_Click;
            // 
            // picStateValidation
            // 
            picStateValidation.Location = new Point(104, 12);
            picStateValidation.Margin = new Padding(2, 2, 2, 2);
            picStateValidation.Name = "picStateValidation";
            picStateValidation.Size = new Size(19, 19);
            picStateValidation.SizeMode = PictureBoxSizeMode.StretchImage;
            picStateValidation.TabIndex = 0;
            picStateValidation.TabStop = false;
            // 
            // SiteCreatorForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(676, 371);
            Controls.Add(picStateValidation);
            Controls.Add(lblState);
            Controls.Add(txtState);
            Controls.Add(lblClient);
            Controls.Add(txtClient);
            Controls.Add(chkProduction);
            Controls.Add(chkCaseInfoSearch);
            Controls.Add(chkESubpoena);
            Controls.Add(btnCreate);
            Margin = new Padding(2, 2, 2, 2);
            Name = "SiteCreatorForm";
            Text = "Site Creator";
            ((System.ComponentModel.ISupportInitialize)picStateValidation).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
