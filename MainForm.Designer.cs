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
            siteSelectorPanel.Location = new Point(10, 156);
            siteSelectorPanel.Margin = new Padding(2, 2, 2, 2);
            siteSelectorPanel.MinimumSize = new Size(600, 400);
            siteSelectorPanel.Name = "siteSelectorPanel";
            siteSelectorPanel.Size = new Size(647, 552);
            siteSelectorPanel.TabIndex = 0;
            siteSelectorPanel.ZipValidationFunc = null;
            // 
            // lblProdPath
            // 
            lblProdPath.Location = new Point(10, 16);
            lblProdPath.Margin = new Padding(2, 0, 2, 0);
            lblProdPath.Name = "lblProdPath";
            lblProdPath.Size = new Size(160, 24);
            lblProdPath.TabIndex = 1;
            lblProdPath.Text = "Production:";
            // 
            // txtProdPath
            // 
            txtProdPath.Location = new Point(176, 16);
            txtProdPath.Margin = new Padding(2, 2, 2, 2);
            txtProdPath.Name = "txtProdPath";
            txtProdPath.ReadOnly = true;
            txtProdPath.Size = new Size(401, 27);
            txtProdPath.TabIndex = 2;
            // 
            // btnBrowseProd
            // 
            btnBrowseProd.Location = new Point(584, 16);
            btnBrowseProd.Margin = new Padding(2, 2, 2, 2);
            btnBrowseProd.Name = "btnBrowseProd";
            btnBrowseProd.Size = new Size(24, 24);
            btnBrowseProd.TabIndex = 3;
            btnBrowseProd.Text = "...";
            btnBrowseProd.Click += BtnBrowseProd_Click;
            // 
            // lblEapPath
            // 
            lblEapPath.Location = new Point(10, 48);
            lblEapPath.Margin = new Padding(2, 0, 2, 0);
            lblEapPath.Name = "lblEapPath";
            lblEapPath.Size = new Size(160, 24);
            lblEapPath.TabIndex = 4;
            lblEapPath.Text = "CaseInfoSearch:";
            // 
            // txtEapPath
            // 
            txtEapPath.Location = new Point(176, 48);
            txtEapPath.Margin = new Padding(2, 2, 2, 2);
            txtEapPath.Name = "txtEapPath";
            txtEapPath.ReadOnly = true;
            txtEapPath.Size = new Size(401, 27);
            txtEapPath.TabIndex = 5;
            // 
            // btnBrowseEap
            // 
            btnBrowseEap.Location = new Point(584, 48);
            btnBrowseEap.Margin = new Padding(2, 2, 2, 2);
            btnBrowseEap.Name = "btnBrowseEap";
            btnBrowseEap.Size = new Size(24, 24);
            btnBrowseEap.TabIndex = 6;
            btnBrowseEap.Text = "...";
            btnBrowseEap.Click += BtnBrowseEap_Click;
            // 
            // lblDataAccessPath
            // 
            lblDataAccessPath.Location = new Point(10, 80);
            lblDataAccessPath.Margin = new Padding(2, 0, 2, 0);
            lblDataAccessPath.Name = "lblDataAccessPath";
            lblDataAccessPath.Size = new Size(160, 24);
            lblDataAccessPath.TabIndex = 7;
            lblDataAccessPath.Text = "DataAccess:";
            // 
            // txtDataAccessPath
            // 
            txtDataAccessPath.Location = new Point(176, 80);
            txtDataAccessPath.Margin = new Padding(2, 2, 2, 2);
            txtDataAccessPath.Name = "txtDataAccessPath";
            txtDataAccessPath.ReadOnly = true;
            txtDataAccessPath.Size = new Size(401, 27);
            txtDataAccessPath.TabIndex = 8;
            // 
            // btnBrowseDataAccess
            // 
            btnBrowseDataAccess.Location = new Point(584, 80);
            btnBrowseDataAccess.Margin = new Padding(2, 2, 2, 2);
            btnBrowseDataAccess.Name = "btnBrowseDataAccess";
            btnBrowseDataAccess.Size = new Size(24, 24);
            btnBrowseDataAccess.TabIndex = 9;
            btnBrowseDataAccess.Text = "...";
            btnBrowseDataAccess.Click += BtnBrowseDataAccess_Click;
            // 
            // lblESubPath
            // 
            lblESubPath.Location = new Point(10, 112);
            lblESubPath.Margin = new Padding(2, 0, 2, 0);
            lblESubPath.Name = "lblESubPath";
            lblESubPath.Size = new Size(160, 24);
            lblESubPath.TabIndex = 10;
            lblESubPath.Text = "eSubpoena:";
            // 
            // txtESubPath
            // 
            txtESubPath.Location = new Point(176, 112);
            txtESubPath.Margin = new Padding(2, 2, 2, 2);
            txtESubPath.Name = "txtESubPath";
            txtESubPath.ReadOnly = true;
            txtESubPath.Size = new Size(401, 27);
            txtESubPath.TabIndex = 11;
            // 
            // btnBrowseESub
            // 
            btnBrowseESub.Location = new Point(584, 112);
            btnBrowseESub.Margin = new Padding(2, 2, 2, 2);
            btnBrowseESub.Name = "btnBrowseESub";
            btnBrowseESub.Size = new Size(24, 24);
            btnBrowseESub.TabIndex = 12;
            btnBrowseESub.Text = "...";
            btnBrowseESub.Click += BtnBrowseESub_Click;
            // 
            // btnExecute
            // 
            btnExecute.Location = new Point(584, 728);
            btnExecute.Margin = new Padding(2, 2, 2, 2);
            btnExecute.Name = "btnExecute";
            btnExecute.Size = new Size(73, 27);
            btnExecute.TabIndex = 13;
            btnExecute.Text = "Update";
            btnExecute.Click += BtnExecute_Click;
            // 
            // btnSiteCreator
            // 
            btnSiteCreator.Location = new Point(10, 728);
            btnSiteCreator.Margin = new Padding(2, 2, 2, 2);
            btnSiteCreator.Name = "btnSiteCreator";
            btnSiteCreator.Size = new Size(73, 27);
            btnSiteCreator.TabIndex = 13;
            btnSiteCreator.Text = "Create";
            btnSiteCreator.Click += btnSiteCreator_Click_1;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(678, 769);
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
            Margin = new Padding(2, 2, 2, 2);
            Name = "MainForm";
            Text = "LL TOOL J";
            ResumeLayout(false);
            PerformLayout();
        }


    }
}
