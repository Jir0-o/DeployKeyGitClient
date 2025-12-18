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
using System.Drawing;

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

        private TextBox txtGitBranch = null!;
        private Button btnClearLog = null!;
        private TextBox txtCustomCmd = null!;
        private Button btnRunCmd = null!;

        private TextBox txtLicenseApiUrl = null!;
        private TextBox txtDefaultLicenseId = null!;
        private Button btnApplyLicense = null!;


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
        private Button btnEnablePhpGd = null!;
        private Button btnCancelOp = null!;

        private Button btnRegisterDevice = null!;
        private TextBox txtApiUrl = null!;
        private Button btnApplyApiUrl = null!;
        private TextBox txtSkipPath = null!;
        private Button btnToggleSkipWorktree = null!;
        private Button btnBackupDb = null!;
        private Button btnGenerateEnv = null!;

        private Button btnResetSkipWorktree = null!;

        // Protect single function UI
        private TextBox txtControllerRelPath = null!;
        private TextBox txtFunctionName = null!;
        private Button btnProtectFunction = null!;
        private Button btnReapplyProtected = null!;
        private Button btnMarkFileSkipWorktree = null!;

        // Security UI
        private Button btnChangeUnlockPassword = null!;

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

        private readonly CredentialStore _creds = new CredentialStore();
        private bool _isLocked = false;

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
                if (s.TryGetValue("GitBranch", out v)) txtGitBranch.Text = v;

                if (s.TryGetValue("LicenseApiUrl", out v)) txtLicenseApiUrl.Text = v;
                if (s.TryGetValue("DefaultLicenseIdentifier", out v)) txtDefaultLicenseId.Text = v;
            }
            catch { /* ignore */ }

            // STARTUP: ensure admin/user credentials are in place and lock if needed
            try
            {
                // Admin password must be created once; cannot be changed later
                if (!_creds.HasAdminPassword())
                {
                    using var dlg = new Form
                    {
                        Width = 520,
                        Height = 220,
                        StartPosition = FormStartPosition.CenterParent,
                        Text = "Set Admin Password (one-time)",
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox = false,
                        MinimizeBox = false
                    };
                    var lbl = new Label
                    {
                        Text = "No admin password found. Create one now (cannot be changed later):",
                        Left = 12,
                        Top = 12,
                        Width = 480,
                        Height = 32
                    };
                    var lbl1 = new Label { Text = "Admin password:", Left = 12, Top = 44, Width = 200 };
                    var lbl2 = new Label { Text = "Confirm admin password:", Left = 12, Top = 76, Width = 200 };
                    var txt1 = new TextBox { Left = 12, Top = 60, Width = 480, UseSystemPasswordChar = true };
                    var txt2 = new TextBox { Left = 12, Top = 92, Width = 480, UseSystemPasswordChar = true };
                    var btnOk = new Button { Text = "Set Admin Password", Left = 12, Top = 132, Width = 180, DialogResult = DialogResult.OK };
                    var btnCancel = new Button { Text = "Exit App", Left = 204, Top = 132, Width = 100, DialogResult = DialogResult.Cancel };

                    dlg.Controls.AddRange(new Control[] { lbl, lbl1, lbl2, txt1, txt2, btnOk, btnCancel });
                    dlg.AcceptButton = btnOk;

                    var dr = dlg.ShowDialog(this);
                    if (dr == DialogResult.OK)
                    {
                        if (string.IsNullOrEmpty(txt1.Text) || txt1.Text != txt2.Text)
                        {
                            MessageBox.Show("Admin passwords do not match or empty. Exiting.");
                            Application.Exit();
                            return;
                        }

                        _creds.SetAdminPassword(txt1.Text);
                        MessageBox.Show("Admin password set. It cannot be changed later.");
                    }
                    else
                    {
                        Application.Exit();
                        return;
                    }
                }

                // If no user (unlock) password set, prompt optionally to set one now
                if (!_creds.HasUserPassword())
                {
                    using var dlg2 = new Form
                    {
                        Width = 520,
                        Height = 220,
                        StartPosition = FormStartPosition.CenterParent,
                        Text = "Set Unlock Password (optional)",
                        FormBorderStyle = FormBorderStyle.FixedDialog
                    };
                    var lbl2 = new Label
                    {
                        Text = "Set application unlock password (required each time app starts / restores):",
                        Left = 12,
                        Top = 12,
                        Width = 480,
                        Height = 32
                    };
                    var lbl21 = new Label { Text = "Unlock password:", Left = 12, Top = 44, Width = 200 };
                    var lbl22 = new Label { Text = "Confirm unlock password:", Left = 12, Top = 76, Width = 200 };
                    var ut1 = new TextBox { Left = 12, Top = 60, Width = 480, UseSystemPasswordChar = true };
                    var ut2 = new TextBox { Left = 12, Top = 92, Width = 480, UseSystemPasswordChar = true };
                    var ok2 = new Button { Text = "Set Unlock Password", Left = 12, Top = 132, Width = 180, DialogResult = DialogResult.OK };
                    var cancel2 = new Button { Text = "Skip (can set later)", Left = 204, Top = 132, Width = 120, DialogResult = DialogResult.Cancel };

                    dlg2.Controls.AddRange(new Control[] { lbl2, lbl21, lbl22, ut1, ut2, ok2, cancel2 });
                    dlg2.AcceptButton = ok2;

                    if (dlg2.ShowDialog(this) == DialogResult.OK)
                    {
                        if (string.IsNullOrEmpty(ut1.Text) || ut1.Text != ut2.Text)
                        {
                            MessageBox.Show("Unlock passwords do not match or empty. Skipping unlock password creation.");
                        }
                        else
                        {
                            _creds.SetUserPassword(ut1.Text);
                            MessageBox.Show("Unlock password set.");
                        }
                    }
                }

                // If a user password exists, mark locked and prompt after form shown
                if (_creds.HasUserPassword())
                {
                    _isLocked = true;
                    BeginInvoke(new Action(() => { PromptUnlockIfNeeded(); }));
                }
            }
            catch (Exception ex)
            {
                Log("Startup credential check error: " + ex.Message);
            }

        }

        private void InitUi()
        {
            // top-level split: left = controls, right = workspace (public key + log)
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
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

            // Git branch
            var lblBranch = new Label { Text = "Git branch (optional):", Anchor = AnchorStyles.Left, AutoSize = true };
            txtGitBranch = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 200, Text = "master" }; // or "main"
            repoTbl.Controls.Add(lblBranch, 0, 4);
            repoTbl.Controls.Add(txtGitBranch, 0, 5);
            repoTbl.SetColumnSpan(txtGitBranch, 3);

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

            // Group: Git operations
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

            // Custom command group (runs in project root)
            var grpCmd = CreateGroupPanel("Custom Command (project root)");
            var cmdFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            cmdFlow.Controls.Add(new Label
            {
                Text = "Command:",
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            });

            txtCustomCmd = new TextBox
            {
                Width = 400
            };

            btnRunCmd = new Button
            {
                Text = "Run",
                AutoSize = true
            };
            btnRunCmd.Click += BtnRunCmd_Click;

            // Press Enter in the textbox to run
            txtCustomCmd.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    BtnRunCmd_Click(s, e);
                }
            };

            cmdFlow.Controls.Add(txtCustomCmd);
            cmdFlow.Controls.Add(btnRunCmd);
            grpCmd.Controls.Add(cmdFlow);
            leftTable.Controls.Add(grpCmd);


            // Group: DB / SQL
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

            // Group: Composer & Artisan
            var grpComposer = CreateGroupPanel("Composer & Artisan");
            var composerFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };

            btnComposerInstall = new Button { Text = "Composer Install (auto-update)", AutoSize = true };
            btnComposerInstall.Click += BtnComposerInstall_Click;

            btnMigrate = new Button { Text = "Run php artisan migrate", AutoSize = true };
            btnMigrate.Click += BtnMigrate_Click;

            btnEnablePhpZip = new Button { Text = "Enable PHP zip extension", AutoSize = true };
            btnEnablePhpZip.Click += BtnEnablePhpZip_Click;

            // NEW: gd button
            btnEnablePhpGd = new Button { Text = "Enable PHP gd extension", AutoSize = true };
            btnEnablePhpGd.Click += BtnEnablePhpGd_Click;

            composerFlow.Controls.Add(btnComposerInstall);
            composerFlow.Controls.Add(btnMigrate);
            composerFlow.Controls.Add(btnEnablePhpZip);
            composerFlow.Controls.Add(btnEnablePhpGd);

            grpComposer.Controls.Add(composerFlow);
            leftTable.Controls.Add(grpComposer);


            // Group: Virtual Host
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

            // Group: Backoffice / API / Skip-worktree
            var grpApi = CreateGroupPanel("Backoffice / API / Skip-worktree");
            var apiRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            btnRegisterDevice = new Button { Text = "Register Device (Backoffice)", AutoSize = true };
            btnRegisterDevice.Click += BtnRegisterDevice_Click;
            apiRow.Controls.Add(btnRegisterDevice);
            apiRow.Controls.Add(new Label { Text = "API URL to apply (.env):", AutoSize = true, Padding = new Padding(10, 8, 0, 0) });
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

            // In the skipRow FlowLayoutPanel (around line where you add btnToggleSkipWorktree)
            var btnTrackAndProtect = new Button 
            { 
                Text = "Track & Protect", 
                AutoSize = true 
            };
            btnTrackAndProtect.Click += async (s, e) =>
            {
                try
                {
                    var projectRoot = txtInstallFolder.Text?.Trim();
                    if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                    {
                        MessageBox.Show("Select project folder.");
                        return;
                    }

                    var raw = txtSkipPath.Text?.Trim() ?? "";
                    if (string.IsNullOrEmpty(raw))
                    {
                        MessageBox.Show("Enter path to protect.");
                        return;
                    }

                    // Process paths
                    var parts = raw
                        .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToArray();

                    if (parts.Length == 0)
                    {
                        MessageBox.Show("No valid paths to protect.");
                        return;
                    }

                    foreach (var path in parts)
                    {
                        await AppLogic.TrackAndProtectFileAsync(projectRoot, path, Log);
                    }
                    
                    MessageBox.Show($"Attempted to track and protect {parts.Length} path(s). Check log for details.");
                }
                catch (Exception ex)
                {
                    Log($"ERROR: {ex.Message}");
                    MessageBox.Show($"Error: {ex.Message}");
                }
            };
            skipRow.Controls.Add(btnTrackAndProtect);

            // NEW reset button
            btnResetSkipWorktree = new Button
            {
                Text = "Reset all Skip-Worktree",
                AutoSize = true
            };
            btnResetSkipWorktree.Click += BtnResetSkipWorktree_Click;

            skipRow.Controls.Add(txtSkipPath);
            skipRow.Controls.Add(btnToggleSkipWorktree);
            skipRow.Controls.Add(btnResetSkipWorktree);  

            grpApi.Controls.Add(skipRow);


            leftTable.Controls.Add(grpApi);

            // Group: DB Tools
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

            // Group: License Settings (.env)
            var grpLicense = CreateGroupPanel("License Settings (.env)");

            var licTbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2
            };
            licTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); // label column
            licTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // input column

            // LICENSE_API_URL
            licTbl.Controls.Add(new Label
            {
                Text = "LICENSE_API_URL:",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 0, 0);

            txtLicenseApiUrl = new TextBox
            {
                Width = 360,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            licTbl.Controls.Add(txtLicenseApiUrl, 1, 0);

            // DEFAULT_LICENSE_IDENTIFIER
            licTbl.Controls.Add(new Label
            {
                Text = "DEFAULT_LICENSE_IDENTIFIER:",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 0, 1);

            txtDefaultLicenseId = new TextBox
            {
                Width = 360,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            licTbl.Controls.Add(txtDefaultLicenseId, 1, 1);

            // Apply button
            var licFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight
            };

            btnApplyLicense = new Button
            {
                Text = "Apply License to .env",
                AutoSize = true
            };
            btnApplyLicense.Click += BtnApplyLicense_Click;

            licFlow.Controls.Add(btnApplyLicense);

            grpLicense.Controls.Add(licTbl);
            grpLicense.Controls.Add(licFlow);

            leftTable.Controls.Add(grpLicense);


            // Group: Protect single function
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

            // Group: Security (new)
            var grpSecurity = CreateGroupPanel("Security");
            var secFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            btnChangeUnlockPassword = new Button { Text = "Change Unlock Password (admin required)", AutoSize = true };
            btnChangeUnlockPassword.Click += BtnChangeUnlockPassword_Click;
            secFlow.Controls.Add(btnChangeUnlockPassword);
            grpSecurity.Controls.Add(secFlow);
            leftTable.Controls.Add(grpSecurity);

            // Right side: public key editor and logs
            rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(8)
            };
            rightPanel.Controls.Add(rightFlow);

            var lblPublicKey = new Label { Text = "Public deploy key (editable) - edit then Save to write files", AutoSize = true };
            txtPublicKey = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Width = Math.Max(400, splitMain.Panel2.ClientSize.Width - 40),
                Height = 280,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
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

            // Log area
            grpLogPanel = CreateGroupPanel("Log");
            grpLogPanel.Height = 320;
            grpLogPanel.AutoSize = false;
            grpLogPanel.Width = Math.Max(360, splitMain.Panel2.ClientSize.Width - 40);

            // Layout inside Log panel
            var logLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            logLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // buttons row
            logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // log textbox row

            // Clear log button
            btnClearLog = new Button
            {
                Text = "Clear Log",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            btnClearLog.Click += (s, e) => txtLog.Clear();

            // Log textbox
            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill
            };

            // Add controls
            logLayout.Controls.Add(btnClearLog, 0, 0);
            logLayout.Controls.Add(txtLog, 0, 1);

            grpLogPanel.Controls.Add(logLayout);
            rightFlow.Controls.Add(grpLogPanel); 
            // ❌ this line is wrong for your current layout
            // splitMain.Panel2.Controls.Add(grpLogPanel);


            // Persist inputs as user types
            txtInstallFolder.TextChanged += (s, e) => SettingsManager.Save("InstallFolder", txtInstallFolder.Text);
            txtGitUrl.TextChanged += (s, e) => SettingsManager.Save("GitUrl", txtGitUrl.Text);
            txtKeyFolder.TextChanged += (s, e) => SettingsManager.Save("KeyFolder", txtKeyFolder.Text);
            txtDbHost.TextChanged += (s, e) => SettingsManager.Save("DbHost", txtDbHost.Text);
            txtDbPort.TextChanged += (s, e) => SettingsManager.Save("DbPort", txtDbPort.Text);
            txtDbName.TextChanged += (s, e) => SettingsManager.Save("DbName", txtDbName.Text);
            txtDbUser.TextChanged += (s, e) => SettingsManager.Save("DbUser", txtDbUser.Text);
            txtDbPass.TextChanged += (s, e) => SettingsManager.Save("DbPass", txtDbPass.Text);
            txtLicenseApiUrl.TextChanged += (s, e) => SettingsManager.Save("LicenseApiUrl", txtLicenseApiUrl.Text);
            txtDefaultLicenseId.TextChanged += (s, e) => SettingsManager.Save("DefaultLicenseIdentifier", txtDefaultLicenseId.Text);
            txtSqlPath.TextChanged += (s, e) => SettingsManager.Save("SqlPath", txtSqlPath.Text);
            txtApiUrl.TextChanged += (s, e) => SettingsManager.Save("ApiUrl", txtApiUrl.Text);
            txtSkipPath.TextChanged += (s, e) => SettingsManager.Save("SkipPath", txtSkipPath.Text);
            txtVHostDomain.TextChanged += (s, e) => SettingsManager.Save("VHostDomain", txtVHostDomain.Text);
            txtControllerRelPath.TextChanged += (s, e) => SettingsManager.Save("ControllerRelPath", txtControllerRelPath.Text);
            txtFunctionName.TextChanged += (s, e) => SettingsManager.Save("FunctionName", txtFunctionName.Text);
            txtGitBranch.TextChanged += (s, e) => SettingsManager.Save("GitBranch", txtGitBranch.Text);

            // handle resizing to keep right textbox and log width responsive
            splitMain.Panel2.Resize += (s, e) =>
            {
                txtPublicKey.Width = Math.Max(400, splitMain.Panel2.ClientSize.Width - 40);

                if (grpLogPanel != null)
                {
                    grpLogPanel.Width = Math.Max(360, splitMain.Panel2.ClientSize.Width - 40);
                    txtLog.Width = Math.Max(200, grpLogPanel.ClientSize.Width - 16);
                }
            };

            // Minimize to tray and lock
            this.Resize += (s, e) =>
            {
                try
                {
                    if (this.WindowState == FormWindowState.Minimized)
                    {
                        _isLocked = true;
                        Log("Application minimized to tray - will require unlock on restore.");
                        this.Hide();
                    }
                }
                catch { }
            };


            // Optional: lock on deactivation (commented; enable if you want extra strict)
            this.Deactivate += (s, e) =>
            {
                // _isLocked = true;
            };

            // Prompt on activation if locked
            this.Activated += (s, e) =>
            {
                try
                {
                    if (_isLocked)
                    {
                        PromptUnlockIfNeeded();
                    }
                }
                catch { }
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
            var lbl = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Font = new System.Drawing.Font(Font.FontFamily, 9.0f, System.Drawing.FontStyle.Bold),
                Height = 20
            };
            pnl.Controls.Add(lbl);
            return pnl;
        }

        private void Log(string line)
        {
            try
            {
                if (txtLog == null)
                    return;

                // If handle not created yet, just write to Debug and bail out
                if (!txtLog.IsHandleCreated)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG-before-handle] {line}");
                    return;
                }

                if (txtLog.InvokeRequired)
                {
                    txtLog.BeginInvoke(new Action(() =>
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
                    }));
                }
                else
                {
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
                }
            }
            catch
            {
               
            }
        }


        // ---------------- Key UI handlers ----------------

        private void BtnBrowseKeyFolder_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select folder to save the deploy key (outside repo recommended)" };
            if (dlg.ShowDialog() == DialogResult.OK) txtKeyFolder.Text = dlg.SelectedPath;
        }

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
                txtPublicKey.Text = _publicSsh;
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

                File.WriteAllText(pubPath, txtPublicKey.Text ?? "", Encoding.UTF8);
                Log("Saved deploy public key to: " + pubPath);

                if (!string.IsNullOrEmpty(_privatePem))
                {
                    File.WriteAllText(privPath, _privatePem, Encoding.UTF8);
                    _privateKeyPath = privPath;
                    Log("Saved private key to: " + privPath);
                }
                else if (!File.Exists(privPath))
                {
                    var res = MessageBox.Show("No private key in memory. Do you want to paste private PEM now (recommended)?", "Private key", MessageBoxButtons.YesNo);
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

                var branch = txtGitBranch.Text?.Trim();
                progressBar.Value = 0;
                await AppLogic.CloneIntoTempThenMoveAsync(gitUrl, targetFolder, v => progressBar.Value = v, Log, _privateKeyPath, branch);
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

                var branch = txtGitBranch.Text?.Trim();
                progressBar.Value = 0;
                await AppLogic.PullUpdateAsync(targetFolder, v => progressBar.Value = v, Log, _privateKeyPath, branch);
                MessageBox.Show("Update finished. Check log for details.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Pull error: " + ex.Message);
            }
        }

        private async void BtnRunCmd_Click(object? sender, EventArgs e)
        {
            try
            {
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder (clone target) first.");
                    return;
                }

                var cmd = txtCustomCmd.Text?.Trim();
                if (string.IsNullOrEmpty(cmd))
                {
                    MessageBox.Show("Enter a command to run.");
                    return;
                }

                // Echo the command into the log like a shell
                Log($"> {cmd}");

                await AppLogic.RunCustomCommandAsync(projectRoot, cmd, Log);
            }
            catch (Exception ex)
            {
                Log("Custom command error: " + ex.Message);
                MessageBox.Show("Custom command error: " + ex.Message);
            }
        }

        private async void BtnApplyLicense_Click(object? sender, EventArgs e)
        {
            try
            {
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder first.");
                    return;
                }

                var apiUrl = txtLicenseApiUrl.Text?.Trim() ?? "";
                var licenseId = txtDefaultLicenseId.Text?.Trim() ?? "";

                if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(licenseId))
                {
                    MessageBox.Show("Fill both LICENSE_API_URL and DEFAULT_LICENSE_IDENTIFIER.");
                    return;
                }

                var ok = await AppLogic.UpdateLicenseEnvAsync(projectRoot, apiUrl, licenseId, Log);
                if (!ok)
                {
                    // .env not found – exactly what you requested
                    MessageBox.Show("No .env file found in project root. Please generate .env file first.");
                }
                else
                {
                    MessageBox.Show("License settings updated in .env.");
                }
            }
            catch (Exception ex)
            {
                Log("Apply license error: " + ex.Message);
                MessageBox.Show("Apply license error: " + ex.Message);
            }
        }

        private async void BtnResetSkipWorktree_Click(object? sender, EventArgs e)
        {
            try
            {
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder first.");
                    return;
                }

                var confirm = MessageBox.Show(
                    "This will:\n" +
                    "  - Clear skip-worktree on ALL tracked files\n" +
                    "  - Delete .deploy_protected protected store\n" +
                    "  - Delete controller backup files (*.deploybak.*)\n\n" +
                    "Continue?",
                    "Full protection reset",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes)
                    return;

                await AppLogic.ResetAllProtectionAsync(projectRoot, Log);

                txtSkipPath.Text = string.Empty;
                SettingsManager.Save("SkipPath", "");

                MessageBox.Show("Full protection reset completed. Check log for details.");
            }
            catch (Exception ex)
            {
                Log("Reset protection error: " + ex.Message);
                MessageBox.Show("Reset protection error: " + ex.Message);
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
            var ok = AppLogic.EnablePhpExtension("zip", Log);
            MessageBox.Show(
                ok
                    ? "PHP zip extension enabled in php.ini (backup created). Restart XAMPP/Apache, then run Composer again."
                    : "Could not enable zip automatically. Check the log and edit php.ini manually if needed."
            );
        }

        private void BtnEnablePhpGd_Click(object? sender, EventArgs e)
        {
            var ok = AppLogic.EnablePhpExtension("gd", Log);
            MessageBox.Show(
                ok
                    ? "PHP gd extension enabled in php.ini (backup created). Restart XAMPP/Apache, then click Composer Install again."
                    : "Could not enable gd automatically. Check the log and edit php.ini manually if needed."
            );
        }

        private async void BtnCreateVHost_Click(object? sender, EventArgs e)
        {
            try
            {
                var domain = txtVHostDomain.Text?.Trim();
                if (string.IsNullOrEmpty(domain))
                {
                    MessageBox.Show("Enter domain (e.g. shoe.com)");
                    return;
                }

                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder.");
                    return;
                }

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
                if (string.IsNullOrEmpty(domain))
                {
                    MessageBox.Show("Enter domain to remove (e.g. myproject.local)");
                    return;
                }

                var confirm = MessageBox.Show(
                    $"This will attempt to remove vhost, hosts entry and certs for '{domain}'. Continue?",
                    "Confirm remove",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

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
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder first.");
                    return;
                }

                var ok = await AppLogic.ApplyProcessorIdToBackofficeControllerAsync(projectRoot, Log);

                MessageBox.Show(
                    ok
                        ? "ProcessorId applied to .env (BACKOFFICE_ALLOWED_PROCESSOR_ID)."
                        : "ProcessorId update failed. See log for details."
                );
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Register device error: " + ex.Message);
            }
        }

        private void BtnApplyApiUrl_Click(object? sender, EventArgs e)
        {
            try
            {
                var url = txtApiUrl.Text?.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    MessageBox.Show("Enter API URL.");
                    return;
                }

                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder.");
                    return;
                }

                var envPath = Path.Combine(projectRoot, ".env");
                if (!File.Exists(envPath))
                {
                    MessageBox.Show(".env file not found in project root. Generate .env first.");
                    return;
                }

                // Read current .env
                var text = File.ReadAllText(envPath, Encoding.UTF8);

                const string key = "SYNC_LOG_API_URL";
                var pattern = $"^{Regex.Escape(key)}=.*$";

                // If key exists → replace line, else append new line
                if (Regex.IsMatch(text, pattern, RegexOptions.Multiline))
                {
                    text = Regex.Replace(
                        text,
                        pattern,
                        $"{key}={url}",
                        RegexOptions.Multiline
                    );
                }
                else
                {
                    if (!text.EndsWith(Environment.NewLine))
                        text += Environment.NewLine;

                    text += $"{key}={url}" + Environment.NewLine;
                }

                // Backup .env before writing
                var backup = envPath + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(envPath, backup, true);
                Log("Backed up .env to: " + backup);

                // Write updated .env
                File.WriteAllText(envPath, text, Encoding.UTF8);
                Log($"Updated {key} in .env to: {url}");

                MessageBox.Show("API URL applied to .env (SYNC_LOG_API_URL).");
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
                var projectRoot = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    MessageBox.Show("Select project folder.");
                    return;
                }

                var raw = txtSkipPath.Text ?? "";
                // Support newline, comma, semicolon separated paths
                var parts = raw
                    .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();

                if (parts.Length == 0)
                {
                    MessageBox.Show("Enter at least one relative path (file or folder) to protect.");
                    return;
                }

                await AppLogic.ToggleSkipWorktreeListAsync(projectRoot, parts, Log);
                MessageBox.Show("Skip-worktree toggle attempted for all listed paths. Check log for details.");
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
                if (string.IsNullOrEmpty(rel) || string.IsNullOrEmpty(fn))
                {
                    MessageBox.Show("Set controller path and function name.");
                    return;
                }

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
                if (string.IsNullOrEmpty(rel))
                {
                    MessageBox.Show("Set controller relative path.");
                    return;
                }

                await AppLogic.ToggleSkipWorktreeAsync(projectRoot, rel, Log);
                MessageBox.Show("Toggled skip-worktree for file (file-level protection).");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show("Mark skip-worktree error: " + ex.Message);
            }
        }

        // ---------------- Security / Unlock helpers ----------------

        private void BtnChangeUnlockPassword_Click(object? sender, EventArgs e)
        {
            ChangeUnlockPasswordViaAdmin();
        }

        private void PromptUnlockIfNeeded()
        {
            if (!_isLocked) return;
            try
            {
                using var lockDlg = new LockForm(pw => _creds.VerifyUserPassword(pw), "Unlock Deploy-Key Git Client");
                var res = lockDlg.ShowDialog(this);
                if (res == DialogResult.OK)
                {
                    _isLocked = false;
                    Log("Application unlocked.");
                }
                else
                {
                    Log("Unlock canceled - exiting.");
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                Log("Unlock prompt error: " + ex.Message);
                Application.Exit();
            }
        }

        // Admin-guarded method to change the unlock password.
        // Admin password itself cannot be changed after first set.
        private void ChangeUnlockPasswordViaAdmin()
        {
            try
            {
                // Ask for admin password first
                using var adminDlg = new Form
                {
                    Width = 420,
                    Height = 170,
                    StartPosition = FormStartPosition.CenterParent,
                    Text = "Verify Admin Password",
                    FormBorderStyle = FormBorderStyle.FixedDialog
                };
                var lbl = new Label { Text = "Enter admin password:", Left = 12, Top = 12, Width = 380 };
                var txt = new TextBox { Left = 12, Top = 36, Width = 380, UseSystemPasswordChar = true };
                var btn = new Button { Text = "Verify", Left = 12, Top = 72, Width = 120, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 152, Top = 72, Width = 120, DialogResult = DialogResult.Cancel };
                adminDlg.Controls.AddRange(new Control[] { lbl, txt, btn, btnCancel });
                adminDlg.AcceptButton = btn;

                if (adminDlg.ShowDialog(this) != DialogResult.OK) return;
                if (!_creds.VerifyAdminPassword(txt.Text))
                {
                    MessageBox.Show("Admin password incorrect.");
                    return;
                }

                // Now ask for new unlock password
                using var setDlg = new Form
                {
                    Width = 520,
                    Height = 190,
                    StartPosition = FormStartPosition.CenterParent,
                    Text = "Set New Unlock Password",
                    FormBorderStyle = FormBorderStyle.FixedDialog
                };
                var lbl2 = new Label { Text = "Enter new unlock password:", Left = 12, Top = 12, Width = 480 };
                var np1 = new TextBox { Left = 12, Top = 36, Width = 480, UseSystemPasswordChar = true };
                var np2 = new TextBox { Left = 12, Top = 72, Width = 480, UseSystemPasswordChar = true };
                var ok = new Button { Text = "Set Unlock Password", Left = 12, Top = 108, Width = 180, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 204, Top = 108, Width = 120, DialogResult = DialogResult.Cancel };
                setDlg.Controls.AddRange(new Control[] { lbl2, np1, np2, ok, cancel });
                setDlg.AcceptButton = ok;

                if (setDlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (string.IsNullOrEmpty(np1.Text) || np1.Text != np2.Text)
                    {
                        MessageBox.Show("Passwords empty or do not match.");
                        return;
                    }

                    _creds.SetUserPassword(np1.Text);
                    MessageBox.Show("Unlock password updated.");
                }
            }
            catch (Exception ex)
            {
                Log("ChangeUnlockPasswordViaAdmin error: " + ex.Message);
                MessageBox.Show("Failed to change unlock password.");
            }
        }

        // ---------------- RSA helpers ----------------

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
            for (int i = 0; i < b64.Length; i += lineLen)
                sb.AppendLine(b64.Substring(i, Math.Min(lineLen, b64.Length - i)));
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
            var algStr = MakeString(alg);
            var eStr = MakeMpInt(exponent);
            var nStr = MakeMpInt(modulus);
            outMs.Write(algStr, 0, algStr.Length);
            outMs.Write(eStr, 0, eStr.Length);
            outMs.Write(nStr, 0, nStr.Length);
            return outMs.ToArray();
        }
    }
}
