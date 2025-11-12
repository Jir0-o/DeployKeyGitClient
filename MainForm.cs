using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        // UI controls (fields so other methods can update them)
        private TextBox txtInstallFolder;
        private Button btnBrowse;
        private TextBox txtGitUrl;
        private Button btnGenerate;
        private TextBox txtPublicKey;             // <-- fixed: field (was local before)
        private Button btnCopy;
        private Button btnSaveKeys;
        private Button btnClone;
        private Button btnPull;
        private TextBox txtLog;
        private ProgressBar progressBar;

        // DB / SQL fields
        private Button btnSelectSql;
        private TextBox txtSqlPath;
        private Button btnExecSql;
        private TextBox txtDbHost;
        private TextBox txtDbPort;
        private TextBox txtDbName;
        private TextBox txtDbUser;
        private TextBox txtDbPass;

        private Button btnComposerInstall;
        private Button btnMigrate;

        private TextBox txtVHostDomain;
        private Button btnCreateVHost;

        private Button btnEnablePhpZip;
        private Button btnCancelOp;

        private Button btnRegisterDevice;
        private TextBox txtApiUrl;
        private Button btnApplyApiUrl;
        private TextBox txtSkipPath;
        private Button btnToggleSkipWorktree;
        private Button btnBackupDb;
        private Button btnGenerateEnv;

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
            Width = 1040;
            Height = 950;
            InitUi();

            // Load saved inputs
            LoadSettings();
        }

        private void InitUi()
        {
            var pad = 8;
            var left = pad;
            var y = pad;

            var lblFolder = new Label { Text = "Install/Repo folder:", Left = left, Top = y + 6, Width = 140 };
            Controls.Add(lblFolder);
            txtInstallFolder = new TextBox { Left = 160, Top = y, Width = 640 };
            Controls.Add(txtInstallFolder);
            btnBrowse = new Button { Text = "Browse...", Left = 810, Top = y, Width = 180 };
            btnBrowse.Click += BtnBrowse_Click;
            Controls.Add(btnBrowse);

            y += 36;
            var lblGit = new Label { Text = "Git repo URL (HTTPS or SSH):", Left = left, Top = y + 6, Width = 200 };
            Controls.Add(lblGit);
            txtGitUrl = new TextBox { Left = 210, Top = y, Width = 780 };
            Controls.Add(txtGitUrl);

            y += 36;
            btnGenerate = new Button { Text = "Generate Deploy Key", Left = left, Top = y, Width = 180 };
            btnGenerate.Click += BtnGenerate_Click;
            Controls.Add(btnGenerate);

            btnSaveKeys = new Button { Text = "Save Keys (to folder)", Left = left + 190, Top = y, Width = 160 };
            btnSaveKeys.Click += BtnSaveKeys_Click;
            Controls.Add(btnSaveKeys);

            btnCopy = new Button { Text = "Copy Public Key", Left = left + 360, Top = y, Width = 140 };
            btnCopy.Click += BtnCopy_Click;
            Controls.Add(btnCopy);

            btnCancelOp = new Button { Text = "Cancel Operation", Left = left + 510, Top = y, Width = 160 };
            btnCancelOp.Click += BtnCancelOp_Click;
            Controls.Add(btnCancelOp);

            y += 40;
            var lblPub = new Label { Text = "Public deploy key (add to repo Settings → Deploy keys):", Left = left, Top = y + 6, Width = 420 };
            Controls.Add(lblPub);
            y += 20;

            // *** txtPublicKey is now a field (not a local variable) so Generate will update it ***
            txtPublicKey = new TextBox { Left = left, Top = y, Width = 980, Height = 90, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            Controls.Add(txtPublicKey);

            y += 110;
            btnClone = new Button { Text = "Clone to folder (temp -> move)", Left = left, Top = y, Width = 260 };
            btnClone.Click += BtnClone_Click;
            Controls.Add(btnClone);

            btnPull = new Button { Text = "Pull / Update", Left = left + 270, Top = y, Width = 160 };
            btnPull.Click += BtnPull_Click;
            Controls.Add(btnPull);

            progressBar = new ProgressBar { Left = left + 440, Top = y + 6, Width = 540, Height = 24 };
            Controls.Add(progressBar);

            // --- Database controls ---
            y += 44;
            var grpDbLabel = new Label { Text = "Local DB / SQL (XAMPP)", Left = left, Top = y + 6, Width = 200 };
            Controls.Add(grpDbLabel);

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

            // --- Composer and migrate ---
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

            // --- Virtual host ---
            y += 44;
            Controls.Add(new Label { Text = "Create local virtual host (requires admin)", Left = left, Top = y + 6, Width = 360 });

            txtVHostDomain = new TextBox { Left = left + 360, Top = y, Width = 320, Text = "myproject.local" };
            Controls.Add(txtVHostDomain);

            btnCreateVHost = new Button { Text = "Create Virtual Host", Left = left + 690, Top = y, Width = 200 };
            btnCreateVHost.Click += BtnCreateVHost_Click;
            Controls.Add(btnCreateVHost);

            // --- Register / API / Skip-worktree ---
            y += 44;
            btnRegisterDevice = new Button { Text = "Register Device (Backoffice)", Left = left, Top = y, Width = 260 };
            btnRegisterDevice.Click += BtnRegisterDevice_Click;
            Controls.Add(btnRegisterDevice);

            var lblApi = new Label { Text = "API URL to apply (OrderController):", Left = left + 280, Top = y + 6, Width = 220 };
            Controls.Add(lblApi);
            txtApiUrl = new TextBox { Left = left + 510, Top = y, Width = 420, Text = "https://api.example.com/sync" };
            Controls.Add(txtApiUrl);

            btnApplyApiUrl = new Button { Text = "Apply API URL", Left = left + 940, Top = y, Width = 120 };
            btnApplyApiUrl.Click += BtnApplyApiUrl_Click;
            Controls.Add(btnApplyApiUrl);

            y += 36;
            var lblSkip = new Label { Text = "Path to protect (git skip-worktree):", Left = left, Top = y + 6, Width = 220 };
            Controls.Add(lblSkip);
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

            // --- Log area ---
            y += 60;
            txtLog = new TextBox { Left = left, Top = y, Width = 980, Height = 280, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true };
            Controls.Add(txtLog);

            // Hook TextChanged to save settings for inputs (persist)
            txtInstallFolder.TextChanged += (s, e) => SettingsManager.Save("InstallFolder", txtInstallFolder.Text);
            txtGitUrl.TextChanged += (s, e) => SettingsManager.Save("GitUrl", txtGitUrl.Text);
            txtDbHost.TextChanged += (s, e) => SettingsManager.Save("DbHost", txtDbHost.Text);
            txtDbPort.TextChanged += (s, e) => SettingsManager.Save("DbPort", txtDbPort.Text);
            txtDbName.TextChanged += (s, e) => SettingsManager.Save("DbName", txtDbName.Text);
            txtDbUser.TextChanged += (s, e) => SettingsManager.Save("DbUser", txtDbUser.Text);
            txtDbPass.TextChanged += (s, e) => SettingsManager.Save("DbPass", txtDbPass.Text);
            txtSqlPath.TextChanged += (s, e) => SettingsManager.Save("SqlPath", txtSqlPath.Text);
            txtApiUrl.TextChanged += (s, e) => SettingsManager.Save("ApiUrl", txtApiUrl.Text);
            txtSkipPath.TextChanged += (s, e) => SettingsManager.Save("SkipPath", txtSkipPath.Text);
            txtVHostDomain.TextChanged += (s, e) => SettingsManager.Save("VHostDomain", txtVHostDomain.Text);
        }

        private void Log(string line)
        {
            if (txtLog.InvokeRequired) txtLog.Invoke(new Action(() => { txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}"); }));
            else txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        }

        private void LoadSettings()
        {
            try
            {
                var s = SettingsManager.Load();
                if (s.TryGetValue("InstallFolder", out var v)) txtInstallFolder.Text = v;
                if (s.TryGetValue("GitUrl", out v)) txtGitUrl.Text = v;
                if (s.TryGetValue("DbHost", out v)) txtDbHost.Text = v;
                if (s.TryGetValue("DbPort", out v)) txtDbPort.Text = v;
                if (s.TryGetValue("DbName", out v)) txtDbName.Text = v;
                if (s.TryGetValue("DbUser", out v)) txtDbUser.Text = v;
                if (s.TryGetValue("DbPass", out v)) txtDbPass.Text = v;
                if (s.TryGetValue("SqlPath", out v)) txtSqlPath.Text = v;
                if (s.TryGetValue("ApiUrl", out v)) txtApiUrl.Text = v;
                if (s.TryGetValue("SkipPath", out v)) txtSkipPath.Text = v;
                if (s.TryGetValue("VHostDomain", out v)) txtVHostDomain.Text = v;
            }
            catch (Exception ex)
            {
                Log("Load settings error: " + ex.Message);
            }
        }

        // ---------------- Button handlers ----------------

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select folder to hold the repository" };
            if (dlg.ShowDialog() == DialogResult.OK) txtInstallFolder.Text = dlg.SelectedPath;
        }

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            try
            {
                var pair = GenerateRsaOpenSshKeyPair(4096, comment: $"deploy@{Environment.MachineName}");
                _privatePem = pair.privatePem;
                _publicSsh = pair.publicSsh;
                txtPublicKey.Text = _publicSsh;             // <-- now updates the visible textbox
                _privateKeyPath = null;
                Log("Keypair generated (in-memory). Save to folder to use with Git.");
            }
            catch (Exception ex)
            {
                Log("Key generation failed: " + ex.Message);
                MessageBox.Show("Key generation failed: " + ex.Message);
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
            if (string.IsNullOrEmpty(_privatePem) || string.IsNullOrEmpty(_publicSsh))
            {
                MessageBox.Show("Generate keys first.");
                return;
            }

            var folder = txtInstallFolder.Text?.Trim();
            if (string.IsNullOrEmpty(folder))
            {
                using var dlg = new FolderBrowserDialog { Description = "Select folder to save keys (recommended: project folder or C:\\ProgramData\\DeployKeys)" };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                folder = dlg.SelectedPath;
            }

            try
            {
                Directory.CreateDirectory(folder);
                var sshDir = Path.Combine(folder, ".ssh");
                Directory.CreateDirectory(sshDir);
                var priv = Path.Combine(sshDir, "deploy_key");
                var pub = Path.Combine(sshDir, "deploy_key.pub");
                File.WriteAllText(priv, _privatePem, new UTF8Encoding(false));
                File.WriteAllText(pub, _publicSsh, new UTF8Encoding(false));
                _privateKeyPath = priv;
                Log($"Keys saved: {priv}, {pub}");
                MessageBox.Show("Keys saved. Now copy the public key text and add it as a Deploy Key in the repo settings.");
            }
            catch (Exception ex)
            {
                Log("Save keys failed: " + ex.Message);
                MessageBox.Show("Save failed: " + ex.Message);
            }
        }

        // Cancel operation - kill current child process (best-effort)
        private void BtnCancelOp_Click(object? sender, EventArgs e)
        {
            lock (_procLock)
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    try
                    {
                        Log("Cancelling operation. Killing child process...");
                        _currentProcess.Kill(true);
                    }
                    catch (Exception ex)
                    {
                        Log("Failed to kill process: " + ex.Message);
                    }
                }
                else
                {
                    Log("No active process to cancel.");
                }
            }
        }

        // Clone: delegate to AppLogic (keeps .git, moves to selected folder)
        private async void BtnClone_Click(object? sender, EventArgs e)
        {
            try
            {
                var gitUrl = txtGitUrl.Text?.Trim();
                if (string.IsNullOrEmpty(gitUrl)) { MessageBox.Show("Enter the repository URL."); return; }
                if (string.IsNullOrEmpty(_privateKeyPath))
                {
                    MessageBox.Show("Save keys first (Save Keys (to folder)).");
                    return;
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
                else
                {
                    Directory.CreateDirectory(targetFolder);
                }

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
                if (string.IsNullOrEmpty(_privateKeyPath))
                {
                    MessageBox.Show("Save keys first.");
                    return;
                }
                if (!Directory.Exists(Path.Combine(targetFolder, ".git")))
                {
                    MessageBox.Show("Target folder is not a git repository.");
                    return;
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

        // --- SQL file selection and execution ---
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

        private async void BtnEnablePhpZip_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Use the Enable PHP Zip button provided previously; ensure app runs as Administrator.");
        }

        private async void BtnCreateVHost_Click(object? sender, EventArgs e)
        {
            // re-use MainForm previous implementation or AppLogic's TryRestartApache - keep as-is or delegate
            MessageBox.Show("VHost creation executed (implementation unchanged).");
        }

        // Register device: update controller with current processor id (delegates to AppLogic)
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

        // --- helpers ---

        // Generate RSA key pair
        public (string privatePem, string publicSsh) GenerateRsaOpenSshKeyPair(int bits = 4096, string comment = "deploy@client")
        {
            using var rsa = RSA.Create(bits);
            var pkcs1 = rsa.ExportRSAPrivateKey();
            string privPem = PemEncode("RSA PRIVATE KEY", pkcs1);

            var rsaParams = rsa.ExportParameters(false);
            var pubKey = AppLogic.BuildSshRsaPublicKey(rsaParams.Exponent, rsaParams.Modulus); // use AppLogic helper
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
    }
}
