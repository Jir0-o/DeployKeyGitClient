// Replace your MainForm.cs with this file
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeployKeyGitClient
{
    public class MainForm : Form
    {
        // UI controls (initialized in InitUi)
        private TextBox txtInstallFolder = null!;
        private Button btnBrowse = null!;
        private TextBox txtGitUrl = null!;
        private Button btnGenerate = null!;
        private TextBox txtPublicKey = null!;
        private Button btnCopy = null!;
        private Button btnSaveKeys = null!;
        private Button btnClone = null!;
        private Button btnPull = null!;
        private TextBox txtLog = null!;
        private ProgressBar progressBar = null!;

        private Button btnRemoveVHost = null!;

        // New: key path selector separate from project folder
        private TextBox txtKeyFolder = null!;
        private Button btnBrowseKeyFolder = null!;
        private Button btnLoadKey = null!;
        private Button btnSaveKeyFromText = null!;

        // DB / SQL fields
        private Button btnSelectSql = null!;
        private TextBox txtSqlPath = null!;
        private Button btnExecSql = null!;
        private TextBox txtDbHost = null!;
        private TextBox txtDbPort = null!;
        private TextBox txtDbName = null!;
        private TextBox txtDbUser = null!;
        private TextBox txtDbPass = null!;

        private Button btnComposerInstall = null!;
        private Button btnMigrate = null!;

        private TextBox txtVHostDomain = null!;
        private Button btnCreateVHost = null!;

        private Button btnEnablePhpZip = null!;
        private Button btnCancelOp = null!;

        private Button btnRegisterDevice = null!;
        private TextBox txtApiUrl = null!;
        private Button btnApplyApiUrl = null!;
        private TextBox txtSkipPath = null!;
        private Button btnToggleSkipWorktree = null!;
        private Button btnBackupDb = null!;
        private Button btnGenerateEnv = null!;

        // Protect single function UI
        private TextBox txtControllerRelPath = null!;
        private TextBox txtFunctionName = null!;
        private Button btnProtectFunction = null!;
        private Button btnReapplyProtected = null!;
        private Button btnMarkFileSkipWorktree = null!;

        // Generated key content
        private string? _privatePem;
        private string? _publicSsh;
        private string? _privateKeyPath; // file where private key saved

        // process cancellation support
        private Process? _currentProcess;
        private readonly object _procLock = new object();

        // layout containers
        private SplitContainer splitMain = null!;
        private Panel leftPanel = null!;
        private Panel rightPanel = null!;
        private TableLayoutPanel leftTable = null!;
        private FlowLayoutPanel rightFlow = null!;

        // Log group panel reference (so we can resize correctly)
        private Panel grpLogPanel = null!;

        public MainForm()
        {
            Text = "Deploy-Key Git Client";
            Width = 1200;
            Height = 920;
            StartPosition = FormStartPosition.CenterScreen;
            InitUi();

            // Load saved inputs (SettingsManager)
            try
            {
                var s = SettingsManager.Load();
                if (s.TryGetValue("InstallFolder", out var v)) txtInstallFolder.Text = v;
                if (s.TryGetValue("GitUrl", out v)) txtGitUrl.Text = v;
                if (s.TryGetValue("KeyFolder", out v)) txtKeyFolder.Text = v;
                if (s.TryGetValue("DbHost", out v)) txtDbHost.Text = v;
                if (s.TryGetValue("DbPort", out v)) txtDbPort.Text = v;
                if (s.TryGetValue("DbName", out v)) txtDbName.Text = v;
                if (s.TryGetValue("DbUser", out v)) txtDbUser.Text = v;
                if (s.TryGetValue("DbPass", out v)) txtDbPass.Text = v;
                if (s.TryGetValue("SqlPath", out v)) txtSqlPath.Text = v;
                if (s.TryGetValue("ApiUrl", out v)) txtApiUrl.Text = v;
                if (s.TryGetValue("SkipPath", out v)) txtSkipPath.Text = v;
                if (s.TryGetValue("VHostDomain", out v)) txtVHostDomain.Text = v;
                if (s.TryGetValue("ControllerRelPath", out v)) txtControllerRelPath.Text = v;
                if (s.TryGetValue("FunctionName", out v)) txtFunctionName.Text = v;
                if (s.TryGetValue("PublicKey", out v)) { txtPublicKey.Text = v; _publicSsh = v; }
                if (s.TryGetValue("PrivateKey", out v)) { _privatePem = v; }
            }
            catch { /* ignore */ }
        }

        private void InitUi()
        {
            // top-level split: left = controls, right = workspace (public key + log)
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                // left slightly larger than previously
                SplitterDistance = 760,
                IsSplitterFixed = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(splitMain);

            // Left panel: scrollable controls column
            leftPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            splitMain.Panel1.Controls.Add(leftPanel);

            // Right panel: public key editor + logs
            rightPanel = new Panel { Dock = DockStyle.Fill };
            splitMain.Panel2.Controls.Add(rightPanel);

            // Build left column as a TableLayout to group controls
            leftTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                Padding = new Padding(8),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            leftPanel.Controls.Add(leftTable);

            // Group 1: Repo & Git
            var grpRepo = CreateGroupPanel("Repository");
            var repoTbl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            repoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
            repoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            repoTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            repoTbl.RowCount = 3;

            // Install/Repo folder row (with Browse)
            var lblInstall = new Label { Text = "Install/Repo folder:", Anchor = AnchorStyles.Left, AutoSize = true };
            txtInstallFolder = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 360 };
            btnBrowse = new Button { Text = "Browse...", Width = 72 };
            btnBrowse.Click += BtnBrowse_Click;

            repoTbl.Controls.Add(lblInstall, 0, 0);
            repoTbl.Controls.Add(txtInstallFolder, 0, 1);
            repoTbl.SetColumnSpan(txtInstallFolder, 2);
            repoTbl.Controls.Add(btnBrowse, 2, 1);

            // Git URL
            var lblGit = new Label { Text = "Git repo URL (HTTPS or SSH):", Anchor = AnchorStyles.Left, AutoSize = true };
            txtGitUrl = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 460 };
            repoTbl.Controls.Add(lblGit, 0, 2);
            repoTbl.Controls.Add(txtGitUrl, 0, 3);
            repoTbl.SetColumnSpan(txtGitUrl, 3);

            // Key generation + buttons
            var keyRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            btnGenerate = new Button { Text = "Generate Deploy Key", AutoSize = true };
            btnGenerate.Click += BtnGenerate_Click;
            btnSaveKeys = new Button { Text = "Save Keys (to key folder)", AutoSize = true };
            btnSaveKeys.Click += BtnSaveKeys_Click;
            btnCopy = new Button { Text = "Copy Public Key", AutoSize = true };
            btnCopy.Click += BtnCopy_Click;
            btnCancelOp = new Button { Text = "Cancel Operation", AutoSize = true };
            btnCancelOp.Click += BtnCancelOp_Click;
            keyRow.Controls.Add(btnGenerate);
            keyRow.Controls.Add(btnSaveKeys);
            keyRow.Controls.Add(btnCopy);
            keyRow.Controls.Add(btnCancelOp);

            // Key folder row and load/save buttons
            var keyFolderRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            keyFolderRow.Controls.Add(new Label { Text = "Key folder (separate):", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            txtKeyFolder = new TextBox { Width = 300 };
            btnBrowseKeyFolder = new Button { Text = "Browse", AutoSize = true };
            btnBrowseKeyFolder.Click += BtnBrowseKeyFolder_Click;
            btnLoadKey = new Button { Text = "Load Key from key folder", AutoSize = true };
            btnLoadKey.Click += BtnLoadKey_Click;
            btnSaveKeyFromText = new Button { Text = "Save Key Text to key folder", AutoSize = true };
            btnSaveKeyFromText.Click += BtnSaveKeyFromText_Click;
            keyFolderRow.Controls.Add(txtKeyFolder);
            keyFolderRow.Controls.Add(btnBrowseKeyFolder);
            keyFolderRow.Controls.Add(btnLoadKey);
            keyFolderRow.Controls.Add(btnSaveKeyFromText);

            // Add repo group items
            grpRepo.Controls.Add(repoTbl);
            grpRepo.Controls.Add(keyRow);
            grpRepo.Controls.Add(keyFolderRow);
            leftTable.Controls.Add(grpRepo);

            // More groups (Git ops, DB, Composer, VHost, etc.) - keep layout compact
            var grpGitOps = CreateGroupPanel("Git operations");
            var gitOpsFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            btnClone = new Button { Text = "Clone to folder (temp -> move)", AutoSize = true };
            btnClone.Click += BtnClone_Click;
            btnPull = new Button { Text = "Pull / Update", AutoSize = true };
            btnPull.Click += BtnPull_Click;
            progressBar = new ProgressBar { Width = 200, Height = 22, Anchor = AnchorStyles.Left };
            gitOpsFlow.Controls.Add(btnClone);
            gitOpsFlow.Controls.Add(btnPull);
            gitOpsFlow.Controls.Add(progressBar);
            grpGitOps.Controls.Add(gitOpsFlow);
            leftTable.Controls.Add(grpGitOps);

            var grpDb = CreateGroupPanel("Local DB / SQL (XAMPP)");
            var dbTbl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 6 };
            dbTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            dbTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            dbTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            dbTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            dbTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            dbTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dbTbl.RowCount = 2;
            dbTbl.Controls.Add(new Label { Text = "Host:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            txtDbHost = new TextBox { Text = "127.0.0.1", Width = 110 };
            dbTbl.Controls.Add(txtDbHost, 1, 0);
            dbTbl.Controls.Add(new Label { Text = "Port:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
            txtDbPort = new TextBox { Text = "3306", Width = 50 };
            dbTbl.Controls.Add(txtDbPort, 3, 0);
            dbTbl.Controls.Add(new Label { Text = "DB:", AutoSize = true, Anchor = AnchorStyles.Left }, 4, 0);
            txtDbName = new TextBox { Text = "laravel", Width = 120 };
            dbTbl.Controls.Add(txtDbName, 5, 0);
            dbTbl.Controls.Add(new Label { Text = "User:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            txtDbUser = new TextBox { Text = "root", Width = 110 };
            dbTbl.Controls.Add(txtDbUser, 1, 1);
            dbTbl.Controls.Add(new Label { Text = "Pass:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
            txtDbPass = new TextBox { Width = 160, UseSystemPasswordChar = true };
            dbTbl.Controls.Add(txtDbPass, 3, 1);
            dbTbl.SetColumnSpan(txtDbPass, 3);

            var sqlRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            btnSelectSql = new Button { Text = "Select SQL File", AutoSize = true };
            btnSelectSql.Click += BtnSelectSql_Click;
            txtSqlPath = new TextBox { Width = 460, ReadOnly = true };
            btnExecSql = new Button { Text = "Execute SQL", AutoSize = true };
            btnExecSql.Click += BtnExecSql_Click;
            sqlRow.Controls.Add(btnSelectSql);
            sqlRow.Controls.Add(txtSqlPath);
            sqlRow.Controls.Add(btnExecSql);

            grpDb.Controls.Add(dbTbl);
            grpDb.Controls.Add(sqlRow);
            leftTable.Controls.Add(grpDb);

            var grpComposer = CreateGroupPanel("Composer & Artisan");
            var composerFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            btnComposerInstall = new Button { Text = "Composer Install (auto-update)", AutoSize = true };
            btnComposerInstall.Click += BtnComposerInstall_Click;
            btnMigrate = new Button { Text = "Run php artisan migrate", AutoSize = true };
            btnMigrate.Click += BtnMigrate_Click;
            btnEnablePhpZip = new Button { Text = "Enable PHP zip extension", AutoSize = true };
            btnEnablePhpZip.Click += BtnEnablePhpZip_Click;
            composerFlow.Controls.Add(btnComposerInstall);
            composerFlow.Controls.Add(btnMigrate);
            composerFlow.Controls.Add(btnEnablePhpZip);
            grpComposer.Controls.Add(composerFlow);
            leftTable.Controls.Add(grpComposer);

            var grpVhost = CreateGroupPanel("Virtual Host");
            var vhostRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            vhostRow.Controls.Add(new Label { Text = "Domain:", AutoSize = true, Padding = new Padding(6, 8, 0, 0) });
            txtVHostDomain = new TextBox { Width = 220, Text = "myproject.local" };
            btnCreateVHost = new Button { Text = "Create Virtual Host", AutoSize = true };
            btnCreateVHost.Click += BtnCreateVHost_Click;
            btnRemoveVHost = new Button { Text = "Remove vHost (undo)", AutoSize = true };
            btnRemoveVHost.Click += BtnRemoveVHost_Click;
            vhostRow.Controls.Add(txtVHostDomain);
            vhostRow.Controls.Add(btnCreateVHost);
            vhostRow.Controls.Add(btnRemoveVHost);
            grpVhost.Controls.Add(vhostRow);
            leftTable.Controls.Add(grpVhost);

            var grpApi = CreateGroupPanel("Backoffice / API / Skip-worktree");
            var apiRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            btnRegisterDevice = new Button { Text = "Register Device (Backoffice)", AutoSize = true };
            btnRegisterDevice.Click += BtnRegisterDevice_Click;
            apiRow.Controls.Add(btnRegisterDevice);
            apiRow.Controls.Add(new Label { Text = "API URL to apply (OrderController):", AutoSize = true, Padding = new Padding(10, 8, 0, 0) });
            txtApiUrl = new TextBox { Width = 340, Text = "https://api.example.com/sync" };
            btnApplyApiUrl = new Button { Text = "Apply API URL", AutoSize = true };
            btnApplyApiUrl.Click += BtnApplyApiUrl_Click;
            apiRow.Controls.Add(txtApiUrl);
            apiRow.Controls.Add(btnApplyApiUrl);
            grpApi.Controls.Add(apiRow);

            var skipRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            skipRow.Controls.Add(new Label { Text = "Path to protect (git skip-worktree):", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            txtSkipPath = new TextBox { Width = 420, Text = "app/Http/Controllers/Sales/OrderController.php" };
            btnToggleSkipWorktree = new Button { Text = "Toggle Skip-Worktree", AutoSize = true };
            btnToggleSkipWorktree.Click += BtnToggleSkipWorktree_Click;
            skipRow.Controls.Add(txtSkipPath);
            skipRow.Controls.Add(btnToggleSkipWorktree);
            grpApi.Controls.Add(skipRow);

            leftTable.Controls.Add(grpApi);

            var grpDbOps = CreateGroupPanel("DB Tools");
            var dbOpsFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            btnBackupDb = new Button { Text = "Backup Database", AutoSize = true };
            btnBackupDb.Click += BtnBackupDb_Click;
            btnGenerateEnv = new Button { Text = "Generate .env File", AutoSize = true };
            btnGenerateEnv.Click += BtnGenerateEnv_Click;
            dbOpsFlow.Controls.Add(btnBackupDb);
            dbOpsFlow.Controls.Add(btnGenerateEnv);
            grpDbOps.Controls.Add(dbOpsFlow);
            leftTable.Controls.Add(grpDbOps);

            var grpProtect = CreateGroupPanel("Protect single function workflow (function-level)");
            var protectTbl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            protectTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            protectTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            protectTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            protectTbl.RowCount = 2;
            protectTbl.Controls.Add(new Label { Text = "Controller relative path:", AutoSize = true }, 0, 0);
            txtControllerRelPath = new TextBox { Width = 360, Text = "app/Http/Controllers/BackofficeLoginController.php" };
            protectTbl.Controls.Add(txtControllerRelPath, 1, 0);
            protectTbl.Controls.Add(new Label { Text = "Function name:", AutoSize = true }, 0, 1);
            txtFunctionName = new TextBox { Width = 160, Text = "check" };
            protectTbl.Controls.Add(txtFunctionName, 1, 1);

            var protectFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            btnProtectFunction = new Button { Text = "Save/Protect Function (store local body)", AutoSize = true };
            btnProtectFunction.Click += BtnProtectFunction_Click;
            btnReapplyProtected = new Button { Text = "Reapply All Protected Functions", AutoSize = true };
            btnReapplyProtected.Click += BtnReapplyProtected_Click;
            btnMarkFileSkipWorktree = new Button { Text = "Mark file skip-worktree (file-level)", AutoSize = true };
            btnMarkFileSkipWorktree.Click += BtnMarkFileSkipWorktree_Click;
            protectFlow.Controls.Add(btnProtectFunction);
            protectFlow.Controls.Add(btnReapplyProtected);
            protectFlow.Controls.Add(btnMarkFileSkipWorktree);

            grpProtect.Controls.Add(protectTbl);
            grpProtect.Controls.Add(protectFlow);
            leftTable.Controls.Add(grpProtect);

            // Right side: public key editor and logs
            rightFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false, Padding = new Padding(8) };
            rightPanel.Controls.Add(rightFlow);

            var lblPublicKey = new Label { Text = "Public deploy key (editable) - edit then Save to write files", AutoSize = true };
            txtPublicKey = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Width = Math.Max(400, splitMain.Panel2.ClientSize.Width - 40), Height = 280, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var pkButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            var btnLoad = new Button { Text = "Load Key from key folder", AutoSize = true };
            btnLoad.Click += BtnLoadKey_Click;
            var btnSaveText = new Button { Text = "Save Key Text to key folder", AutoSize = true };
            btnSaveText.Click += BtnSaveKeyFromText_Click;
            pkButtons.Controls.Add(btnLoad);
            pkButtons.Controls.Add(btnSaveText);
            rightFlow.Controls.Add(lblPublicKey);
            rightFlow.Controls.Add(txtPublicKey);
            rightFlow.Controls.Add(pkButtons);

            // Log area - dock and visible
            grpLogPanel = CreateGroupPanel("Log");
            // make log group fixed-size so FlowLayout will show it properly
            grpLogPanel.Height = 320;
            grpLogPanel.AutoSize = false;
            // set initial width to match right panel
            grpLogPanel.Width = Math.Max(360, splitMain.Panel2.ClientSize.Width - 40);

            txtLog = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Dock = DockStyle.Fill, Height = 300 };
            grpLogPanel.Controls.Add(txtLog);
            rightFlow.Controls.Add(grpLogPanel);

            // Persist inputs as user types
            txtInstallFolder.TextChanged += (s, e) => SettingsManager.Save("InstallFolder", txtInstallFolder.Text);
            txtGitUrl.TextChanged += (s, e) => SettingsManager.Save("GitUrl", txtGitUrl.Text);
            txtKeyFolder.TextChanged += (s, e) => SettingsManager.Save("KeyFolder", txtKeyFolder.Text);
            txtDbHost.TextChanged += (s, e) => SettingsManager.Save("DbHost", txtDbHost.Text);
            txtDbPort.TextChanged += (s, e) => SettingsManager.Save("DbPort", txtDbPort.Text);
            txtDbName.TextChanged += (s, e) => SettingsManager.Save("DbName", txtDbName.Text);
            txtDbUser.TextChanged += (s, e) => SettingsManager.Save("DbUser", txtDbUser.Text);
            txtDbPass.TextChanged += (s, e) => SettingsManager.Save("DbPass", txtDbPass.Text);
            txtSqlPath.TextChanged += (s, e) => SettingsManager.Save("SqlPath", txtSqlPath.Text);
            txtApiUrl.TextChanged += (s, e) => SettingsManager.Save("ApiUrl", txtApiUrl.Text);
            txtSkipPath.TextChanged += (s, e) => SettingsManager.Save("SkipPath", txtSkipPath.Text);
            txtVHostDomain.TextChanged += (s, e) => SettingsManager.Save("VHostDomain", txtVHostDomain.Text);
            txtControllerRelPath.TextChanged += (s, e) => SettingsManager.Save("ControllerRelPath", txtControllerRelPath.Text);
            txtFunctionName.TextChanged += (s, e) => SettingsManager.Save("FunctionName", txtFunctionName.Text);

            // handle resizing to keep right textbox and log width responsive
            splitMain.Panel2.Resize += (s, e) =>
            {
                // update public key width
                txtPublicKey.Width = Math.Max(400, splitMain.Panel2.ClientSize.Width - 40);

                // update log group width and inner textbox width
                if (grpLogPanel != null)
                {
                    grpLogPanel.Width = Math.Max(360, splitMain.Panel2.ClientSize.Width - 40);
                    // ensure the internal txtLog receives proper width as it's Dock=Fill inside grpLogPanel
                    txtLog.Width = Math.Max(200, grpLogPanel.ClientSize.Width - 16);
                }
            };
        }

        private Panel CreateGroupPanel(string title)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(6),
                BorderStyle = BorderStyle.FixedSingle
            };
            var lbl = new Label { Text = title, Dock = DockStyle.Top, Font = new System.Drawing.Font(Font.FontFamily, 9.0f, System.Drawing.FontStyle.Bold), Height = 20 };
            pnl.Controls.Add(lbl);
            return pnl;
        }

        private void Log(string line)
        {
            if (txtLog == null) return;
            if (txtLog.InvokeRequired) txtLog.Invoke(new Action(() => { txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}"); }));
            else txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        }

        // ---------------- Key UI handlers ----------------

        private void BtnBrowseKeyFolder_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select folder to save the deploy key (outside repo recommended)" };
            if (dlg.ShowDialog() == DialogResult.OK) txtKeyFolder.Text = dlg.SelectedPath;
        }

        // Install/Repo browse fixes: opens folder browser and writes into txtInstallFolder
        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select folder to hold the repository" };
            if (dlg.ShowDialog() == DialogResult.OK) txtInstallFolder.Text = dlg.SelectedPath;
        }

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            try
            {
                var pair = GenerateRsaOpenSshKeyPair(4096, comment: $"deploy@{Dns.GetHostName()}");
                _privatePem = pair.privatePem;
                _publicSsh = pair.publicSsh;
                txtPublicKey.Text = _publicSsh; // now shows in editable box
                SettingsManager.Save("PrivateKey", _privatePem);
                SettingsManager.Save("PublicKey", _publicSsh);

                _privateKeyPath = null;
                Log("Keypair generated (in-memory). Save to folder to use with Git.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Key generation failed: " + ex.Message);
            }
        }

        private void BtnLoadKey_Click(object? sender, EventArgs e)
        {
            try
            {
                var folder = txtKeyFolder.Text?.Trim();
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    MessageBox.Show("Select key folder first.");
                    return;
                }
                var priv = Path.Combine(folder, ".ssh", "deploy_key");
                var pub = Path.Combine(folder, ".ssh", "deploy_key.pub");
                if (!File.Exists(pub) && File.Exists(Path.Combine(folder, "deploy_key.pub")))
                    pub = Path.Combine(folder, "deploy_key.pub");

                if (File.Exists(pub))
                {
                    txtPublicKey.Text = File.ReadAllText(pub, Encoding.UTF8);
                    Log("Loaded public key into editor.");
                }
                else
                {
                    MessageBox.Show("Public key not found in selected folder (expected .ssh/deploy_key.pub or deploy_key.pub).");
                }

                if (File.Exists(priv))
                {
                    _privateKeyPath = priv;
                    Log("Private key path set to: " + priv);
                }
                else
                {
                    Log("Private key not found in key folder (it's fine if you only have public key).");
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Load key error: " + ex.Message);
            }
        }

        private void BtnSaveKeyFromText_Click(object? sender, EventArgs e)
        {
            try
            {
                var folder = txtKeyFolder.Text?.Trim();
                if (string.IsNullOrEmpty(folder))
                {
                    using var dlg = new FolderBrowserDialog { Description = "Select folder to save keys (separate from repo)" };
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    folder = dlg.SelectedPath;
                    txtKeyFolder.Text = folder;
                }

                var sshDir = Path.Combine(folder, ".ssh");
                Directory.CreateDirectory(sshDir);
                var pubPath = Path.Combine(sshDir, "deploy_key.pub");
                var privPath = Path.Combine(sshDir, "deploy_key");

                // Write public key from editor
                File.WriteAllText(pubPath, txtPublicKey.Text ?? "", Encoding.UTF8);
                Log("Saved deploy public key to: " + pubPath);

                // If private PEM exists in memory, save it; otherwise prompt user if they want to paste private PEM in a dialog
                if (!string.IsNullOrEmpty(_privatePem))
                {
                    File.WriteAllText(privPath, _privatePem, Encoding.UTF8);
                    _privateKeyPath = privPath;
                    Log("Saved private key to: " + privPath);
                }
                else if (!File.Exists(privPath))
                {
                    var res = MessageBox.Show("No private key in memory. Do you want to paste private PEM now (recommended) ?", "Private key", MessageBoxButtons.YesNo);
                    if (res == DialogResult.Yes)
                    {
                        using var dlg = new Form { Width = 800, Height = 480, Text = "Paste Private PEM (deploy_key)" };
                        var txt = new TextBox { Left = 8, Top = 8, Width = 760, Height = 380, Multiline = true, ScrollBars = ScrollBars.Both };
                        var btnOk = new Button { Text = "Save", Left = 580, Top = 400, Width = 80, DialogResult = DialogResult.OK };
                        var btnCancel = new Button { Text = "Cancel", Left = 680, Top = 400, Width = 80, DialogResult = DialogResult.Cancel };
                        dlg.Controls.AddRange(new Control[] { txt, btnOk, btnCancel });
                        dlg.AcceptButton = btnOk;
                        dlg.CancelButton = btnCancel;
                        if (dlg.ShowDialog() == DialogResult.OK)
                        {
                            File.WriteAllText(privPath, txt.Text, Encoding.UTF8);
                            _privateKeyPath = privPath;
                            Log("Saved private key to: " + privPath);
                        }
                    }
                }

                MessageBox.Show("Keys saved to key folder.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Save key error: " + ex.Message);
            }
        }

        private void BtnCopy_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtPublicKey.Text))
            {
                Clipboard.SetText(txtPublicKey.Text);
                Log("Public key copied to clipboard.");
            }
        }

        private void BtnSaveKeys_Click(object? sender, EventArgs e)
        {
            // Shortcut to save current key pair to KeyFolder (same behavior as SaveKeyFromText but ensures privatePem written)
            BtnSaveKeyFromText_Click(sender, e);
        }

        // ---------------- Process cancel ----------------

        private void BtnCancelOp_Click(object? sender, EventArgs e)
        {
            lock (_procLock)
            {
                AppLogic.KillCurrentProcess();
                Log("Requested cancel of running child process.");
            }
        }

        // ---------------- Clone / Pull ----------------

        private async void BtnClone_Click(object? sender, EventArgs e)
        {
            try
            {
                var gitUrl = txtGitUrl.Text?.Trim();
                if (string.IsNullOrEmpty(gitUrl)) { MessageBox.Show("Enter the repository URL."); return; }

                if (string.IsNullOrEmpty(txtKeyFolder.Text?.Trim()) && string.IsNullOrEmpty(_privateKeyPath))
                {
                    MessageBox.Show("Either save a private key to Key folder or set private key path.");
                }

                // Choose privateKeyPath from key folder if available
                if (string.IsNullOrEmpty(_privateKeyPath) && !string.IsNullOrEmpty(txtKeyFolder.Text) && Directory.Exists(txtKeyFolder.Text))
                {
                    var candidate = Path.Combine(txtKeyFolder.Text, ".ssh", "deploy_key");
                    if (File.Exists(candidate)) _privateKeyPath = candidate;
                }

                if (string.IsNullOrEmpty(_privateKeyPath))
                {
                    var res = MessageBox.Show("No private key path set. Continue without SSH override (likely fail)?", "No key", MessageBoxButtons.YesNo);
                    if (res != DialogResult.Yes) return;
                }

                var targetFolder = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(targetFolder)) { MessageBox.Show("Select target folder."); return; }

                if (Directory.Exists(targetFolder) && Directory.EnumerateFileSystemEntries(targetFolder).Any())
                {
                    var confirm = MessageBox.Show(
                        $"Folder '{targetFolder}' is not empty.\n\nThis application will DELETE ALL CONTENTS of the folder before moving the cloned project into it.\n\nProceed?",
                        "Confirm overwrite and clone",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes) { Log("User cancelled clone into non-empty folder."); return; }
                }
                else Directory.CreateDirectory(targetFolder);

                progressBar.Value = 0;
                await AppLogic.CloneIntoTempThenMoveAsync(gitUrl, targetFolder, v => progressBar.Value = v, Log, _privateKeyPath);
                MessageBox.Show("Clone succeeded and files moved to target folder.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Clone error: " + ex.Message);
            }
        }

        private async void BtnPull_Click(object? sender, EventArgs e)
        {
            try
            {
                var targetFolder = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
                {
                    MessageBox.Show("Select existing repo folder.");
                    return;
                }

                // choose key from key folder if not set
                if (string.IsNullOrEmpty(_privateKeyPath) && !string.IsNullOrEmpty(txtKeyFolder.Text))
                {
                    var candidate = Path.Combine(txtKeyFolder.Text, ".ssh", "deploy_key");
                    if (File.Exists(candidate)) _privateKeyPath = candidate;
                }

                progressBar.Value = 0;
                await AppLogic.PullUpdateAsync(targetFolder, v => progressBar.Value = v, Log, _privateKeyPath);
                MessageBox.Show("Update finished. Check log for details.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Pull error: " + ex.Message);
            }
        }

        // ---------------- SQL / DB ----------------

        private void BtnSelectSql_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "SQL files (*.sql)|*.sql|All files|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtSqlPath.Text = dlg.FileName;
                SettingsManager.Save("SqlPath", dlg.FileName);
                Log($"Selected SQL file: {dlg.FileName}");
            }
        }

        private async void BtnExecSql_Click(object? sender, EventArgs e)
        {
            try
            {
                var sqlPath = txtSqlPath.Text?.Trim();
                if (string.IsNullOrEmpty(sqlPath) || !File.Exists(sqlPath))
                {
                    MessageBox.Show("Select a SQL file first.");
                    return;
                }
                var db = new AppLogic.DbInfo
                {
                    Host = txtDbHost.Text.Trim(),
                    Port = txtDbPort.Text.Trim(),
                    Database = txtDbName.Text.Trim(),
                    User = txtDbUser.Text.Trim(),
                    Password = txtDbPass.Text
                };

                progressBar.Value = 0;
                Log($"Importing SQL: {sqlPath}");
                var ok = await AppLogic.ImportSqlFileAsync(sqlPath, db, Log);
                MessageBox.Show(ok ? "SQL executed." : "SQL execution failed. See log.");
                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("SQL exec error: " + ex.Message);
            }
        }

        private async void BtnBackupDb_Click(object? sender, EventArgs e)
        {
            try
            {
                var db = new AppLogic.DbInfo
                {
                    Host = txtDbHost.Text.Trim(),
                    Port = txtDbPort.Text.Trim(),
                    Database = txtDbName.Text.Trim(),
                    User = txtDbUser.Text.Trim(),
                    Password = txtDbPass.Text
                };
                var outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{db.Database}_backup_{DateTime.Now:yyyyMMddHHmmss}.sql");
                var ok = await AppLogic.BackupDatabaseAsync(db, outPath, Log);
                MessageBox.Show(ok ? $"Backup saved: {outPath}" : "Backup failed. See log.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Backup error: " + ex.Message);
            }
        }

        private async void BtnGenerateEnv_Click(object? sender, EventArgs e)
        {
            try
            {
                var db = new AppLogic.DbInfo
                {
                    Host = txtDbHost.Text.Trim(),
                    Port = txtDbPort.Text.Trim(),
                    Database = txtDbName.Text.Trim(),
                    User = txtDbUser.Text.Trim(),
                    Password = txtDbPass.Text
                };
                var ok = await AppLogic.CreateEnvFromExampleAsync(txtInstallFolder.Text.Trim(), db, Log);
                MessageBox.Show(ok ? ".env created" : ".env creation failed or .env.example missing");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show(".env generation failed: " + ex.Message);
            }
        }

        private async void BtnComposerInstall_Click(object? sender, EventArgs e)
        {
            try
            {
                await AppLogic.RunComposerInstallWithFallbackAsync(txtInstallFolder.Text.Trim(), Log);
                MessageBox.Show("Composer install/update finished.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Composer error: " + ex.Message);
            }
        }

        private async void BtnMigrate_Click(object? sender, EventArgs e)
        {
            try
            {
                await AppLogic.RunPhpArtisanMigrateAsync(txtInstallFolder.Text.Trim(), Log);
                MessageBox.Show("Migrate finished.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Migrate error: " + ex.Message);
            }
        }

        private void BtnEnablePhpZip_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Use AppLogic.EnablePhpZip (admin required). This UI placeholder avoids duplicate code. See logs.");
        }

        private async void BtnCreateVHost_Click(object? sender, EventArgs e)
        {
            try
            {
                var domain = txtVHostDomain.Text?.Trim();
                if (string.IsNullOrEmpty(domain)) { MessageBox.Show("Enter domain (e.g. shoe.com)"); return; }
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot)) { MessageBox.Show("Select project folder."); return; }
                var publicPath = Path.Combine(projectRoot, "public");
                var ok = await AppLogic.CreateVirtualHostAsync(domain, publicPath, Log);
                MessageBox.Show(ok ? "Virtual host created (check logs). Make sure app is run as Administrator." : "Failed. See log.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Create VHost error: " + ex.Message);
            }
        }

        private async void BtnRemoveVHost_Click(object? sender, EventArgs e)
        {
            try
            {
                var domain = txtVHostDomain.Text?.Trim();
                if (string.IsNullOrEmpty(domain)) { MessageBox.Show("Enter domain to remove (e.g. myproject.local)"); return; }

                var confirm = MessageBox.Show($"This will attempt to remove vhost, hosts entry and certs for '{domain}'. Continue?", "Confirm remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                var ok = await AppLogic.RemoveVirtualHostAsync(domain, Log);
                MessageBox.Show(ok ? "Remove process finished (check logs)." : "Remove failed (check logs).");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Remove vHost error: " + ex.Message);
            }
        }

        private async void BtnRegisterDevice_Click(object? sender, EventArgs e)
        {
            try
            {
                var ok = await AppLogic.ApplyProcessorIdToBackofficeControllerAsync(txtInstallFolder.Text.Trim(), Log);
                MessageBox.Show(ok ? "ProcessorId applied and file protected." : "ProcessorId update failed or pattern not found.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Register device error: " + ex.Message);
            }
        }

        private async void BtnApplyApiUrl_Click(object? sender, EventArgs e)
        {
            try
            {
                var url = txtApiUrl.Text?.Trim();
                if (string.IsNullOrEmpty(url)) { MessageBox.Show("Enter API URL."); return; }
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot)) { MessageBox.Show("Select project folder."); return; }

                var rel = "app/Http/Controllers/Sales/OrderController.php";
                var file = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file)) { MessageBox.Show($"Controller not found at {file}"); return; }

                var bak = file + ".deploybak." + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(file, bak);
                Log($"Backup created: {bak}");

                var text = File.ReadAllText(file, Encoding.UTF8);
                var replaced = Regex.Replace(text, @"Http::post\(\s*(['""]?)#\1\s*,", $"Http::post('{url}',", RegexOptions.IgnoreCase);
                if (replaced == text) replaced = text.Replace("Http::post('#'", $"Http::post('{url}'");
                File.WriteAllText(file, replaced, Encoding.UTF8);
                Log("OrderController.php patched with API URL.");
                await AppLogic.ToggleSkipWorktreeAsync(projectRoot, rel, Log);
                MessageBox.Show("API URL applied and file marked skip-worktree (protected).");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Apply API URL error: " + ex.Message);
            }
        }

        private async void BtnToggleSkipWorktree_Click(object? sender, EventArgs e)
        {
            try
            {
                await AppLogic.ToggleSkipWorktreeAsync(txtInstallFolder.Text.Trim(), txtSkipPath.Text.Trim(), Log);
                MessageBox.Show("Skip-worktree toggle attempted. Check log.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Toggle skip-worktree error: " + ex.Message);
            }
        }

        // ---------------- Protect single-function workflow ----------------

        private async void BtnProtectFunction_Click(object? sender, EventArgs e)
        {
            try
            {
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder.");
                    return;
                }
                var rel = txtControllerRelPath.Text?.Trim();
                var fn = txtFunctionName.Text?.Trim();
                if (string.IsNullOrEmpty(rel) || string.IsNullOrEmpty(fn)) { MessageBox.Show("Set controller path and function name."); return; }

                var ok = await AppLogic.ProtectFunctionAsync(projectRoot, rel, fn, Log);
                MessageBox.Show(ok ? "Function body saved to protected store." : "Protect failed (pattern not found). See log.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Protect function error: " + ex.Message);
            }
        }

        private async void BtnReapplyProtected_Click(object? sender, EventArgs e)
        {
            try
            {
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder.");
                    return;
                }
                var ok = await AppLogic.ReapplyProtectedFunctionsAsync(projectRoot, Log);
                MessageBox.Show(ok ? "Reapplied protected functions where matches found." : "No protected functions reapplied (check logs).");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Reapply protected error: " + ex.Message);
            }
        }

        private async void BtnMarkFileSkipWorktree_Click(object? sender, EventArgs e)
        {
            try
            {
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder.");
                    return;
                }
                var rel = txtControllerRelPath.Text?.Trim();
                if (string.IsNullOrEmpty(rel)) { MessageBox.Show("Set controller relative path."); return; }
                await AppLogic.ToggleSkipWorktreeAsync(projectRoot, rel, Log);
                MessageBox.Show("Toggled skip-worktree for file (file-level protection).");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Mark skip-worktree error: " + ex.Message);
            }
        }

        // ---------------- RSA helpers (same as before) ----------------

        public (string privatePem, string publicSsh) GenerateRsaOpenSshKeyPair(int bits = 4096, string comment = "deploy@client")
        {
            using var rsa = RSA.Create();
            rsa.KeySize = bits;
            var pkcs1 = rsa.ExportRSAPrivateKey();
            string privPem = PemEncode("RSA PRIVATE KEY", pkcs1);

            var rsaParams = rsa.ExportParameters(false);
            var pubKey = BuildSshRsaPublicKey(rsaParams.Exponent!, rsaParams.Modulus!);
            string pubBase64 = Convert.ToBase64String(pubKey);
            string pubSsh = $"ssh-rsa {pubBase64} {comment}";
            return (privPem, pubSsh);
        }

        private static string PemEncode(string label, byte[] derBytes)
        {
            const int lineLen = 64;
            var b64 = Convert.ToBase64String(derBytes);
            var sb = new StringBuilder();
            sb.AppendLine($"-----BEGIN {label}-----");
            for (int i = 0; i < b64.Length; i += lineLen) sb.AppendLine(b64.Substring(i, Math.Min(lineLen, b64.Length - i)));
            sb.AppendLine($"-----END {label}-----");
            return sb.ToString();
        }

        private static byte[] MpIntWithZeroPrefixIfNeeded(byte[] value)
        {
            if (value == null || value.Length == 0) return new byte[] { 0x00 };
            int i = 0;
            while (i < value.Length && value[i] == 0) i++;
            var trimmed = value.Skip(i).ToArray();
            if (trimmed.Length == 0) trimmed = new byte[] { 0x00 };
            if ((trimmed[0] & 0x80) != 0)
            {
                var withPrefix = new byte[trimmed.Length + 1];
                withPrefix[0] = 0x00;
                Buffer.BlockCopy(trimmed, 0, withPrefix, 1, trimmed.Length);
                return withPrefix;
            }
            return trimmed;
        }

        private static byte[] BuildSshRsaPublicKey(byte[] exponent, byte[] modulus)
        {
            byte[] MakeString(byte[] data)
            {
                var len = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(data.Length));
                using var ms = new MemoryStream();
                ms.Write(len, 0, len.Length);
                ms.Write(data, 0, data.Length);
                return ms.ToArray();
            }

            byte[] MakeMpInt(byte[] data)
            {
                var mp = MpIntWithZeroPrefixIfNeeded(data);
                var len = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(mp.Length));
                using var ms = new MemoryStream();
                ms.Write(len, 0, len.Length);
                ms.Write(mp, 0, mp.Length);
                return ms.ToArray();
            }

            var alg = Encoding.ASCII.GetBytes("ssh-rsa");
            using var outMs = new MemoryStream();
            outMs.Write(MakeString(alg), 0, MakeString(alg).Length);
            outMs.Write(MakeMpInt(exponent), 0, MakeMpInt(exponent).Length);
            outMs.Write(MakeMpInt(modulus), 0, MakeMpInt(modulus).Length);
            return outMs.ToArray();
        }
    }
}
