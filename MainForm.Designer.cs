namespace TheTool
{
    partial class MainForm
    {
        private SiteSelectorPanel siteSelectorPanel;
        private Label lblProdPath;
        private TextBox txtProdPath;
        private Button btnBrowseProd;
        private Label lblEapPath;
        private TextBox txtEapPath;
        private Button btnBrowseEap;
        private Label lblDataAccessPath;
        private TextBox txtDataAccessPath;
        private Button btnBrowseDataAccess;
        private Label lblESubPath;
        private TextBox txtESubPath;
        private Button btnBrowseESub;
        private Button btnExecute;
        private Button btnSiteCreator;

        //private Button btnDummy;

        private void InitializeComponent()
        {
            siteSelectorPanel = new SiteSelectorPanel();
            lblProdPath = new Label();
            txtProdPath = new TextBox();
            btnBrowseProd = new Button();
            lblEapPath = new Label();
            txtEapPath = new TextBox();
            btnBrowseEap = new Button();
            lblDataAccessPath = new Label();
            txtDataAccessPath = new TextBox();
            btnBrowseDataAccess = new Button();
            lblESubPath = new Label();
            txtESubPath = new TextBox();
            btnBrowseESub = new Button();
            btnExecute = new Button();
            btnSiteCreator = new Button();

            SuspendLayout();
            // 
            // siteSelectorPanel
            // 
            siteSelectorPanel.Location = new Point(12, 195);
            siteSelectorPanel.MinimumSize = new Size(600, 400);
            siteSelectorPanel.Name = "siteSelectorPanel";
            siteSelectorPanel.Size = new Size(809, 690);
            siteSelectorPanel.TabIndex = 0;
            siteSelectorPanel.ZipValidationFunc = null;
           
            // 
            // lblProdPath
            // 
            lblProdPath.Location = new Point(12, 20);
            lblProdPath.Name = "lblProdPath";
            lblProdPath.Size = new Size(200, 30);
            lblProdPath.TabIndex = 1;
            lblProdPath.Text = "Production (.zip):";
            // 
            // txtProdPath
            // 
            txtProdPath.Location = new Point(220, 20);
            txtProdPath.Name = "txtProdPath";
            txtProdPath.ReadOnly = true;
            txtProdPath.Size = new Size(500, 31);
            txtProdPath.TabIndex = 2;
            // 
            // btnBrowseProd
            // 
            btnBrowseProd.Location = new Point(730, 20);
            btnBrowseProd.Name = "btnBrowseProd";
            btnBrowseProd.Size = new Size(91, 30);
            btnBrowseProd.TabIndex = 3;
            btnBrowseProd.Text = "Browse...";
            btnBrowseProd.Click += BtnBrowseProd_Click;
            // 
            // lblEapPath
            // 
            lblEapPath.Location = new Point(12, 60);
            lblEapPath.Name = "lblEapPath";
            lblEapPath.Size = new Size(200, 30);
            lblEapPath.TabIndex = 4;
            lblEapPath.Text = "CaseInfoSearch (.zip):";
            // 
            // txtEapPath
            // 
            txtEapPath.Location = new Point(220, 60);
            txtEapPath.Name = "txtEapPath";
            txtEapPath.ReadOnly = true;
            txtEapPath.Size = new Size(500, 31);
            txtEapPath.TabIndex = 5;
            // 
            // btnBrowseEap
            // 
            btnBrowseEap.Location = new Point(730, 60);
            btnBrowseEap.Name = "btnBrowseEap";
            btnBrowseEap.Size = new Size(91, 30);
            btnBrowseEap.TabIndex = 6;
            btnBrowseEap.Text = "Browse...";
            btnBrowseEap.Click += BtnBrowseEap_Click;
            // 
            // lblDataAccessPath
            // 
            lblDataAccessPath.Location = new Point(12, 100);
            lblDataAccessPath.Name = "lblDataAccessPath";
            lblDataAccessPath.Size = new Size(200, 30);
            lblDataAccessPath.TabIndex = 7;
            lblDataAccessPath.Text = "DataAccess (.zip):";
            // 
            // txtDataAccessPath
            // 
            txtDataAccessPath.Location = new Point(220, 100);
            txtDataAccessPath.Name = "txtDataAccessPath";
            txtDataAccessPath.ReadOnly = true;
            txtDataAccessPath.Size = new Size(500, 31);
            txtDataAccessPath.TabIndex = 8;
            // 
            // btnBrowseDataAccess
            // 
            btnBrowseDataAccess.Location = new Point(730, 100);
            btnBrowseDataAccess.Name = "btnBrowseDataAccess";
            btnBrowseDataAccess.Size = new Size(91, 30);
            btnBrowseDataAccess.TabIndex = 9;
            btnBrowseDataAccess.Text = "Browse...";
            btnBrowseDataAccess.Click += BtnBrowseDataAccess_Click;
            // 
            // lblESubPath
            // 
            lblESubPath.Location = new Point(12, 140);
            lblESubPath.Name = "lblESubPath";
            lblESubPath.Size = new Size(200, 30);
            lblESubPath.TabIndex = 10;
            lblESubPath.Text = "eSubpoena (.zip):";
            // 
            // txtESubPath
            // 
            txtESubPath.Location = new Point(220, 140);
            txtESubPath.Name = "txtESubPath";
            txtESubPath.ReadOnly = true;
            txtESubPath.Size = new Size(500, 31);
            txtESubPath.TabIndex = 11;
            // 
            // btnBrowseESub
            // 
            btnBrowseESub.Location = new Point(730, 140);
            btnBrowseESub.Name = "btnBrowseESub";
            btnBrowseESub.Size = new Size(91, 30);
            btnBrowseESub.TabIndex = 12;
            btnBrowseESub.Text = "Browse...";
            btnBrowseESub.Click += BtnBrowseESub_Click;
            // 
            // btnExecute
            // 
            btnExecute.Location = new Point(730, 910);
            btnExecute.Name = "btnExecute";
            btnExecute.Size = new Size(91, 34);
            btnExecute.TabIndex = 13;
            btnExecute.Text = "Update";
            btnExecute.Click += BtnExecute_Click;
            // 
            // btnSiteCreator
            // 
            btnSiteCreator.Location = new Point(12, 910);
            btnSiteCreator.Name = "btnSiteCreator";
            btnSiteCreator.Size = new Size(91, 34);
            btnSiteCreator.TabIndex = 13;
            btnSiteCreator.Text = "Create";
            btnSiteCreator.Click += btnSiteCreator_Click_1;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(847, 961);
            Controls.Add(siteSelectorPanel);
            Controls.Add(lblProdPath);
            Controls.Add(txtProdPath);
            Controls.Add(btnBrowseProd);
            Controls.Add(lblEapPath);
            Controls.Add(txtEapPath);
            Controls.Add(btnBrowseEap);
            Controls.Add(lblDataAccessPath);
            Controls.Add(txtDataAccessPath);
            Controls.Add(btnBrowseDataAccess);
            Controls.Add(lblESubPath);
            Controls.Add(txtESubPath);
            Controls.Add(btnBrowseESub);
            Controls.Add(btnExecute);
            Controls.Add(btnSiteCreator);
            Name = "MainForm";
            Text = "LL TOOL J";
            ResumeLayout(false);
            PerformLayout();
        }


    }
}
