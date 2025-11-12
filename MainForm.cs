using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
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

        public MainForm()
        {
            Text = "Deploy-Key Git Client";
            Width = 1140;
            Height = 1020;
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
            var pad = 8;
            var left = pad;
            var y = pad;

            // Repo folder, git url
            Controls.Add(new Label { Text = "Install/Repo folder:", Left = left, Top = y + 6, Width = 140 });
            txtInstallFolder = new TextBox { Left = 160, Top = y, Width = 640 };
            Controls.Add(txtInstallFolder);
            btnBrowse = new Button { Text = "Browse...", Left = 810, Top = y, Width = 120 };
            btnBrowse.Click += BtnBrowse_Click;
            Controls.Add(btnBrowse);

            y += 36;
            Controls.Add(new Label { Text = "Git repo URL (HTTPS or SSH):", Left = left, Top = y + 6, Width = 200 });
            txtGitUrl = new TextBox { Left = 210, Top = y, Width = 740 };
            Controls.Add(txtGitUrl);

            y += 36;
            // Key generation + separate key folder controls
            btnGenerate = new Button { Text = "Generate Deploy Key", Left = left, Top = y, Width = 180 };
            btnGenerate.Click += BtnGenerate_Click;
            Controls.Add(btnGenerate);

            btnSaveKeys = new Button { Text = "Save Keys (to key folder)", Left = left + 190, Top = y, Width = 160 };
            btnSaveKeys.Click += BtnSaveKeys_Click;
            Controls.Add(btnSaveKeys);

            btnCopy = new Button { Text = "Copy Public Key", Left = left + 360, Top = y, Width = 120 };
            btnCopy.Click += BtnCopy_Click;
            Controls.Add(btnCopy);

            btnCancelOp = new Button { Text = "Cancel Operation", Left = left + 490, Top = y, Width = 140 };
            btnCancelOp.Click += BtnCancelOp_Click;
            Controls.Add(btnCancelOp);

            // Key folder selector (new)
            Controls.Add(new Label { Text = "Key folder (separate):", Left = left + 650, Top = y + 6, Width = 120 });
            txtKeyFolder = new TextBox { Left = left + 770, Top = y, Width = 240 };
            Controls.Add(txtKeyFolder);
            btnBrowseKeyFolder = new Button { Text = "Browse", Left = left + 1018, Top = y, Width = 80 };
            btnBrowseKeyFolder.Click += BtnBrowseKeyFolder_Click;
            Controls.Add(btnBrowseKeyFolder);

            y += 40;
            Controls.Add(new Label { Text = "Public deploy key (editable) - edit then Save to write files:", Left = left, Top = y + 6, Width = 420 });
            y += 20;
            // Public key is editable now so user can tweak before saving
            txtPublicKey = new TextBox { Left = left, Top = y, Width = 1080, Height = 110, Multiline = true, ScrollBars = ScrollBars.Vertical };
            Controls.Add(txtPublicKey);

            // load existing key button + save text to files
            y += 116;
            btnLoadKey = new Button { Text = "Load Key from key folder", Left = left, Top = y, Width = 200 };
            btnLoadKey.Click += BtnLoadKey_Click;
            Controls.Add(btnLoadKey);

            btnSaveKeyFromText = new Button { Text = "Save Key Text to key folder", Left = left + 210, Top = y, Width = 220 };
            btnSaveKeyFromText.Click += BtnSaveKeyFromText_Click;
            Controls.Add(btnSaveKeyFromText);

            // Clone/Pull controls
            y += 44;
            btnClone = new Button { Text = "Clone to folder (temp -> move)", Left = left, Top = y, Width = 260 };
            btnClone.Click += BtnClone_Click;
            Controls.Add(btnClone);

            btnPull = new Button { Text = "Pull / Update", Left = left + 270, Top = y, Width = 160 };
            btnPull.Click += BtnPull_Click;
            Controls.Add(btnPull);

            progressBar = new ProgressBar { Left = left + 440, Top = y + 6, Width = 720, Height = 24 };
            Controls.Add(progressBar);

            // --- Database controls ---
            y += 44;
            Controls.Add(new Label { Text = "Local DB / SQL (XAMPP)", Left = left, Top = y + 6, Width = 200 });

            y += 28;
            Controls.Add(new Label { Text = "Host:", Left = left, Top = y + 6, Width = 40 });
            txtDbHost = new TextBox { Left = left + 40, Top = y, Width = 120, Text = "127.0.0.1" };
            Controls.Add(txtDbHost);

            Controls.Add(new Label { Text = "Port:", Left = left + 180, Top = y + 6, Width = 40 });
            txtDbPort = new TextBox { Left = left + 220, Top = y, Width = 60, Text = "3306" };
            Controls.Add(txtDbPort);

            Controls.Add(new Label { Text = "DB:", Left = left + 300, Top = y + 6, Width = 30 });
            txtDbName = new TextBox { Left = left + 330, Top = y, Width = 160, Text = "laravel" };
            Controls.Add(txtDbName);

            Controls.Add(new Label { Text = "User:", Left = left + 500, Top = y + 6, Width = 40 });
            txtDbUser = new TextBox { Left = left + 540, Top = y, Width = 120, Text = "root" };
            Controls.Add(txtDbUser);

            Controls.Add(new Label { Text = "Pass:", Left = left + 680, Top = y + 6, Width = 40 });
            txtDbPass = new TextBox { Left = left + 720, Top = y, Width = 160, Text = "", UseSystemPasswordChar = true };
            Controls.Add(txtDbPass);

            y += 36;
            btnSelectSql = new Button { Text = "Select SQL File", Left = left, Top = y, Width = 140 };
            btnSelectSql.Click += BtnSelectSql_Click;
            Controls.Add(btnSelectSql);

            txtSqlPath = new TextBox { Left = left + 150, Top = y, Width = 700, ReadOnly = true };
            Controls.Add(txtSqlPath);

            btnExecSql = new Button { Text = "Execute SQL", Left = left + 860, Top = y, Width = 120 };
            btnExecSql.Click += BtnExecSql_Click;
            Controls.Add(btnExecSql);

            // Composer and migrate
            y += 44;
            btnComposerInstall = new Button { Text = "Composer Install (auto-update)", Left = left, Top = y, Width = 260 };
            btnComposerInstall.Click += BtnComposerInstall_Click;
            Controls.Add(btnComposerInstall);

            btnMigrate = new Button { Text = "Run php artisan migrate", Left = left + 290, Top = y, Width = 240 };
            btnMigrate.Click += BtnMigrate_Click;
            Controls.Add(btnMigrate);

            btnEnablePhpZip = new Button { Text = "Enable PHP zip extension", Left = left + 550, Top = y, Width = 220 };
            btnEnablePhpZip.Click += BtnEnablePhpZip_Click;
            Controls.Add(btnEnablePhpZip);

            // Virtual host
            y += 44;
            Controls.Add(new Label { Text = "Create local virtual host (requires admin)", Left = left, Top = y + 6, Width = 360 });

            txtVHostDomain = new TextBox { Left = left + 360, Top = y, Width = 320, Text = "myproject.local" };
            Controls.Add(txtVHostDomain);

            btnCreateVHost = new Button { Text = "Create Virtual Host", Left = left + 690, Top = y, Width = 200 };
            btnCreateVHost.Click += BtnCreateVHost_Click;
            Controls.Add(btnCreateVHost);

            // Register / API / Skip-worktree
            y += 44;
            btnRegisterDevice = new Button { Text = "Register Device (Backoffice)", Left = left, Top = y, Width = 260 };
            btnRegisterDevice.Click += BtnRegisterDevice_Click;
            Controls.Add(btnRegisterDevice);

            Controls.Add(new Label { Text = "API URL to apply (OrderController):", Left = left + 280, Top = y + 6, Width = 220 });
            txtApiUrl = new TextBox { Left = left + 510, Top = y, Width = 420, Text = "https://api.example.com/sync" };
            Controls.Add(txtApiUrl);

            btnApplyApiUrl = new Button { Text = "Apply API URL", Left = left + 940, Top = y, Width = 120 };
            btnApplyApiUrl.Click += BtnApplyApiUrl_Click;
            Controls.Add(btnApplyApiUrl);

            y += 36;
            Controls.Add(new Label { Text = "Path to protect (git skip-worktree):", Left = left, Top = y + 6, Width = 220 });
            txtSkipPath = new TextBox { Left = left + 240, Top = y, Width = 640, Text = "app/Http/Controllers/Sales/OrderController.php" };
            Controls.Add(txtSkipPath);
            btnToggleSkipWorktree = new Button { Text = "Toggle Skip-Worktree", Left = left + 900, Top = y, Width = 160 };
            btnToggleSkipWorktree.Click += BtnToggleSkipWorktree_Click;
            Controls.Add(btnToggleSkipWorktree);

            // DB backup + env
            y += 44;
            btnBackupDb = new Button { Text = "Backup Database", Left = left, Top = y, Width = 260 };
            btnBackupDb.Click += BtnBackupDb_Click;
            Controls.Add(btnBackupDb);

            btnGenerateEnv = new Button { Text = "Generate .env File", Left = left + 270, Top = y, Width = 240 };
            btnGenerateEnv.Click += BtnGenerateEnv_Click;
            Controls.Add(btnGenerateEnv);

            // --- Protect single function UI ---
            y += 44;
            Controls.Add(new Label { Text = "Protect single function workflow (function-level):", Left = left, Top = y + 6, Width = 380 });

            y += 28;
            Controls.Add(new Label { Text = "Controller relative path:", Left = left, Top = y + 6, Width = 140 });
            txtControllerRelPath = new TextBox { Left = left + 150, Top = y, Width = 560, Text = "app/Http/Controllers/BackofficeLoginController.php" };
            Controls.Add(txtControllerRelPath);

            Controls.Add(new Label { Text = "Function name:", Left = left + 720, Top = y + 6, Width = 90 });
            txtFunctionName = new TextBox { Left = left + 810, Top = y, Width = 160, Text = "check" };
            Controls.Add(txtFunctionName);

            y += 36;
            btnProtectFunction = new Button { Text = "Save/Protect Function (store local body)", Left = left, Top = y, Width = 300 };
            btnProtectFunction.Click += BtnProtectFunction_Click;
            Controls.Add(btnProtectFunction);

            btnReapplyProtected = new Button { Text = "Reapply All Protected Functions", Left = left + 310, Top = y, Width = 260 };
            btnReapplyProtected.Click += BtnReapplyProtected_Click;
            Controls.Add(btnReapplyProtected);

            btnMarkFileSkipWorktree = new Button { Text = "Mark file skip-worktree (file-level)", Left = left + 580, Top = y, Width = 240 };
            btnMarkFileSkipWorktree.Click += BtnMarkFileSkipWorktree_Click;
            Controls.Add(btnMarkFileSkipWorktree);

            // --- Log area ---
            y += 60;
            txtLog = new TextBox { Left = left, Top = y, Width = 1080, Height = 260, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true };
            Controls.Add(txtLog);

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
        }

        private void Log(string line)
        {
            if (txtLog.InvokeRequired) txtLog.Invoke(new Action(() => { txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}"); }));
            else txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
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
                txtPublicKey.Text = _publicSsh; // now shows in editable box
                SettingsManager.Save("PrivateKey", _privatePem);
                SettingsManager.Save("PublicKey", _publicSsh);

                _privateKeyPath = null;
                Log("Keypair generated (in-memory). Save to folder to use with Git.");
            }
            catch (Exception ex)
            {
                Log("Key generation failed: " + ex.Message);
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
                Log("Load key error: " + ex.Message);
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
                Log("Save key error: " + ex.Message);
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
                Log("Clone error: " + ex.Message);
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
                Log("Pull error: " + ex.Message);
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

            try
            {
                progressBar.Value = 0;
                Log($"Importing SQL: {sqlPath}");
                var ok = await AppLogic.ImportSqlFileAsync(sqlPath, db, Log);
                MessageBox.Show(ok ? "SQL executed." : "SQL execution failed. See log.");
                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Log("SQL exec error: " + ex.Message);
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
                Log("Backup error: " + ex.Message);
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
                Log(".env generation failed: " + ex.Message);
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
                Log("Composer error: " + ex.Message);
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
                Log("Migrate error: " + ex.Message);
            }
        }

        // Enable PHP zip - delegate to AppLogic (not shown here)
        private void BtnEnablePhpZip_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Use AppLogic.EnablePhpZip (admin required). This UI placeholder avoids duplicate code. See logs.");
        }

        private void BtnCreateVHost_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Use AppLogic.CreateVirtualHost (admin required). This UI placeholder avoids duplicate code. See logs.");
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
                Log("Register device error: " + ex.Message);
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
                Log("Apply API URL error: " + ex.Message);
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
                Log("Toggle skip-worktree error: " + ex.Message);
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
                Log("Protect function error: " + ex.Message);
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
                Log("Reapply protected error: " + ex.Message);
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
                Log("Mark skip-worktree error: " + ex.Message);
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
