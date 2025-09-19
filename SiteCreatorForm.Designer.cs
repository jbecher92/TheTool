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
        private System.Windows.Forms.TextBox txtPreview;
        private System.Windows.Forms.Label lblPreview;
        private System.Windows.Forms.PictureBox picStateValidation;

        private void InitializeComponent()
        {
            this.lblState = new System.Windows.Forms.Label();
            this.txtState = new System.Windows.Forms.TextBox();
            this.lblClient = new System.Windows.Forms.Label();
            this.txtClient = new System.Windows.Forms.TextBox();
            this.chkProduction = new System.Windows.Forms.CheckBox();
            this.chkCaseInfoSearch = new System.Windows.Forms.CheckBox();
            this.chkESubpoena = new System.Windows.Forms.CheckBox();
            //this.chkDataAccess = new System.Windows.Forms.CheckBox();
            this.btnCreate = new System.Windows.Forms.Button();
            this.txtPreview = new System.Windows.Forms.TextBox();
            this.lblPreview = new System.Windows.Forms.Label();
            this.picStateValidation = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.picStateValidation)).BeginInit();
            this.SuspendLayout();

            // lblState
            this.lblState.AutoSize = true;
            this.lblState.Location = new System.Drawing.Point(12, 15);
            this.lblState.Name = "lblState";
            this.lblState.Size = new System.Drawing.Size(48, 25);
            this.lblState.Text = "State";

            // txtState
            this.txtState.Location = new System.Drawing.Point(70, 12);
            this.txtState.Name = "txtState";
            this.txtState.Size = new System.Drawing.Size(50, 31);
            this.txtState.TextChanged += (_, __) => ValidateInputs(updateUiOnly: true);

            // picStateValidation
            this.picStateValidation.Location = new System.Drawing.Point(130, 15);
            this.picStateValidation.Size = new System.Drawing.Size(24, 24);
            this.picStateValidation.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picStateValidation.TabStop = false;
            this.Controls.Add(this.picStateValidation);

            // lblClient
            this.lblClient.AutoSize = true;
            this.lblClient.Location = new System.Drawing.Point(170, 15);
            this.lblClient.Name = "lblClient";
            this.lblClient.Size = new System.Drawing.Size(56, 25);
            this.lblClient.Text = "Client";

            // txtClient
            this.txtClient.Location = new System.Drawing.Point(230, 12);
            this.txtClient.Name = "txtClient";
            this.txtClient.Size = new System.Drawing.Size(332, 31);
            this.txtClient.TextChanged += (_, __) => ValidateInputs(updateUiOnly: true);

            // chkProduction
            this.chkProduction.Location = new System.Drawing.Point(12, 72);
            this.chkProduction.Name = "chkProduction";
            this.chkProduction.Size = new System.Drawing.Size(126, 30);
            this.chkProduction.Text = "Default Site";
            this.chkProduction.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);

            // chkCaseInfoSearch
            this.chkCaseInfoSearch.Location = new System.Drawing.Point(144, 72);
            this.chkCaseInfoSearch.Name = "chkCaseInfoSearch";
            this.chkCaseInfoSearch.Size = new System.Drawing.Size(170, 30);
            this.chkCaseInfoSearch.Text = "CaseInfoSearch";
            this.chkCaseInfoSearch.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);

            // chkESubpoena
            this.chkESubpoena.Location = new System.Drawing.Point(320, 72);
            this.chkESubpoena.Name = "chkESubpoena";
            this.chkESubpoena.Size = new System.Drawing.Size(160, 30);
            this.chkESubpoena.Text = "eSubpoena";
            this.chkESubpoena.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);

            // chkDataAccess
            //this.chkDataAccess.Location = new System.Drawing.Point(446, 72);
            //this.chkDataAccess.Name = "chkDataAccess";
            //this.chkDataAccess.Size = new System.Drawing.Size(140, 30);
            //this.chkDataAccess.Text = "DataAccess";
            //this.chkDataAccess.CheckedChanged += (_, __) => ValidateInputs(updateUiOnly: true);


            // btnCreate
            this.btnCreate.Location = new System.Drawing.Point(733, 412);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(100, 40);
            this.btnCreate.Text = "Create";
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);

            // lblPreview
            this.lblPreview.AutoSize = true;
            this.lblPreview.Location = new System.Drawing.Point(12, 105);
            this.lblPreview.Name = "lblPreview";
            this.lblPreview.Size = new System.Drawing.Size(70, 25);
            this.lblPreview.Text = "Preview";

            // txtPreview
            this.txtPreview.Location = new System.Drawing.Point(12, 135);
            this.txtPreview.Multiline = true;
            this.txtPreview.Name = "txtPreview";
            this.txtPreview.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtPreview.ReadOnly = true;
            this.txtPreview.Size = new System.Drawing.Size(821, 260);

            // SiteCreatorForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(845, 464);
            this.Controls.Add(this.lblState);
            this.Controls.Add(this.txtState);
            this.Controls.Add(this.lblClient);
            this.Controls.Add(this.txtClient);
            this.Controls.Add(this.chkProduction);
            this.Controls.Add(this.chkCaseInfoSearch);
            this.Controls.Add(this.chkESubpoena);
            //this.Controls.Add(this.chkDataAccess);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.lblPreview);
            this.Controls.Add(this.txtPreview);
            this.Name = "SiteCreatorForm";
            this.Text = "Site Creator";
            ((System.ComponentModel.ISupportInitialize)(this.picStateValidation)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
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
