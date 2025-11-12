// MainForm.cs - full file. Replace your existing file with this.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        // UI controls
        private TextBox txtInstallFolder;
        private Button btnBrowse;
        private TextBox txtGitUrl;
        private Button btnGenerate;
        private TextBox txtPublicKey;
        private Button btnCopy;
        private Button btnSaveKeys;
        private Button btnClone;
        private Button btnPull;
        private TextBox txtLog;
        private ProgressBar progressBar;

        // New UI for DB/Composer/Migrate/VHost/Cancel/Register/API/Skip
        private Button btnCancelOp;
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

        private Button btnRegisterDevice;
        private TextBox txtApiUrl;
        private Button btnApplyApiUrl;
        private TextBox txtSkipPath;
        private Button btnToggleSkipWorktree;

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
            Width = 1024;
            Height = 900;
            InitUi();
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

            btnCancelOp = new Button { Text = "Cancel Operation", Left = left + 510, Top = y, Width = 140 };
            btnCancelOp.Click += BtnCancelOp_Click;
            Controls.Add(btnCancelOp);

            y += 40;
            var lblPub = new Label { Text = "Public deploy key (add to repo Settings → Deploy keys):", Left = left, Top = y + 6, Width = 420 };
            Controls.Add(lblPub);
            y += 20;
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

            // DB controls
            y += 44;
            var grpDbLabel = new Label { Text = "Local DB / SQL (XAMPP)", Left = left, Top = y + 6, Width = 200 };
            Controls.Add(grpDbLabel);

            y += 28;
            var lblDbHost = new Label { Text = "Host:", Left = left, Top = y + 6, Width = 40 };
            Controls.Add(lblDbHost);
            txtDbHost = new TextBox { Left = left + 40, Top = y, Width = 120, Text = "127.0.0.1" };
            Controls.Add(txtDbHost);

            var lblDbPort = new Label { Text = "Port:", Left = left + 180, Top = y + 6, Width = 40 };
            Controls.Add(lblDbPort);
            txtDbPort = new TextBox { Left = left + 220, Top = y, Width = 60, Text = "3306" };
            Controls.Add(txtDbPort);

            var lblDbName = new Label { Text = "DB:", Left = left + 300, Top = y + 6, Width = 30 };
            Controls.Add(lblDbName);
            txtDbName = new TextBox { Left = left + 330, Top = y, Width = 160, Text = "laravel" };
            Controls.Add(txtDbName);

            var lblDbUser = new Label { Text = "User:", Left = left + 500, Top = y + 6, Width = 40 };
            Controls.Add(lblDbUser);
            txtDbUser = new TextBox { Left = left + 540, Top = y, Width = 120, Text = "root" };
            Controls.Add(txtDbUser);

            var lblDbPass = new Label { Text = "Pass:", Left = left + 680, Top = y + 6, Width = 40 };
            Controls.Add(lblDbPass);
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

            btnMigrate = new Button { Text = "Run php artisan migrate", Left = left + 270, Top = y, Width = 240 };
            btnMigrate.Click += BtnMigrate_Click;
            Controls.Add(btnMigrate);

            btnEnablePhpZip = new Button { Text = "Enable PHP zip extension", Left = left + 540, Top = y, Width = 220 };
            btnEnablePhpZip.Click += BtnEnablePhpZip_Click;
            Controls.Add(btnEnablePhpZip);

            // Virtual host
            y += 44;
            var lblVHost = new Label { Text = "Create local virtual host (requires admin)", Left = left, Top = y + 6, Width = 360 };
            Controls.Add(lblVHost);

            txtVHostDomain = new TextBox { Left = left + 360, Top = y, Width = 320, Text = "myproject.local" };
            Controls.Add(txtVHostDomain);

            btnCreateVHost = new Button { Text = "Create Virtual Host", Left = left + 690, Top = y, Width = 220 };
            btnCreateVHost.Click += BtnCreateVHost_Click;
            Controls.Add(btnCreateVHost);

            // Register device & API patch
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

            // Log
            y += 44;
            txtLog = new TextBox { Left = left, Top = y, Width = 980, Height = 300, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true };
            Controls.Add(txtLog);
        }

        private void Log(string line)
        {
            if (txtLog.InvokeRequired) txtLog.Invoke(new Action(() => { txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}"); }));
            else txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
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
                _privateKeyPath = null;
                Log("Keypair generated (in-memory). Save to folder to use with Git.");
                Log("Note: GitHub recommends RSA >= 2048. Using 4096 by default.");
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
                try
                {
                    var fi = new FileInfo(priv);
                    var acl = fi.GetAccessControl();
                    acl.SetAccessRuleProtection(true, false);
                    var sid = WindowsIdentity.GetCurrent().User;
                    if (sid != null)
                    {
                        var rule = new System.Security.AccessControl.FileSystemAccessRule(sid, System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow);
                        acl.ResetAccessRule(rule);
                        fi.SetAccessControl(acl);
                    }
                }
                catch { /* non-fatal */ }

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

        // Clone into temp then move (preserves .git) and .env creation
        private async void BtnClone_Click(object? sender, EventArgs e)
        {
            try
            {
                var gitUrl = txtGitUrl.Text?.Trim();
                if (string.IsNullOrEmpty(gitUrl)) { MessageBox.Show("Enter the repository URL."); return; }
                if (string.IsNullOrEmpty(_privateKeyPath)) { MessageBox.Show("Save keys first (Save Keys (to folder))."); return; }
                var targetFolder = txtInstallFolder.Text?.Trim();
                if (string.IsNullOrEmpty(targetFolder)) { MessageBox.Show("Select target folder."); return; }

                // confirm overwrite if target not empty
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
                    Directory.CreateDirectory(targetFolder); // ensure exists
                }

                // Make a temp folder for cloning
                var tempRoot = Path.Combine(Path.GetTempPath(), "deploygit_clone_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                Log($"Cloning into temp folder: {tempRoot}");

                progressBar.Value = 0;
                var sshUrl = ConvertToSshIfHttps(gitUrl);
                var env = CreateGitSshEnv(_privateKeyPath);

                // Run git clone into tempRoot\repo
                var repoDirName = DeriveRepoName(gitUrl) ?? "repo";
                var tempRepoPath = Path.Combine(tempRoot, repoDirName);

                var r = await RunProcessCaptureWithProcess("git", $"clone \"{sshUrl}\" \"{tempRepoPath}\"", env, null);
                Log($"git clone exit {r.code}");
                if (!string.IsNullOrWhiteSpace(r.stdout)) Log(r.stdout);
                if (!string.IsNullOrWhiteSpace(r.stderr)) Log("ERR: " + r.stderr);

                if (r.code != 0)
                {
                    // cleanup temp
                    TryDeleteDirectory(tempRoot);
                    MessageBox.Show($"Clone failed (code {r.code}). See log.");
                    return;
                }

                // Now move/copy contents from tempRepoPath -> targetFolder
                Log($"Copying files from temp to target: {tempRepoPath} -> {targetFolder}");
                // Ensure target is empty (delete if user confirmed earlier)
                if (Directory.Exists(targetFolder) && Directory.EnumerateFileSystemEntries(targetFolder).Any())
                {
                    TryDeleteDirectory(targetFolder);
                    Directory.CreateDirectory(targetFolder);
                }

                // Try fast move first (Directory.Move). If it fails (cross-volume) fall back to recursive copy.
                try
                {
                    Directory.Move(tempRepoPath, targetFolder);
                    Log("Moved temp repo into target using Directory.Move.");
                }
                catch (Exception ex)
                {
                    Log("Directory.Move failed or cross-volume. Falling back to recursive copy. " + ex.Message);
                    CopyDirectoryRecursive(tempRepoPath, targetFolder);
                    TryDeleteDirectory(tempRepoPath);
                    Log("Copied files from temp into target.");
                }

                // final cleanup of temp root if exists
                try { if (Directory.Exists(tempRoot)) TryDeleteDirectory(tempRoot); } catch { /* non-fatal */ }

                // After clone, create .env from .env.example if missing and fill DB values
                try
                {
                    var envExample = Path.Combine(targetFolder, ".env.example");
                    var envFile = Path.Combine(targetFolder, ".env");
                    if (!File.Exists(envFile) && File.Exists(envExample))
                    {
                        var text = File.ReadAllText(envExample, Encoding.UTF8);
                        // replace common DB keys if present; fallback to append
                        text = ReplaceOrAppendEnvKey(text, "DB_HOST", txtDbHost.Text.Trim());
                        text = ReplaceOrAppendEnvKey(text, "DB_PORT", txtDbPort.Text.Trim());
                        text = ReplaceOrAppendEnvKey(text, "DB_DATABASE", txtDbName.Text.Trim());
                        text = ReplaceOrAppendEnvKey(text, "DB_USERNAME", txtDbUser.Text.Trim());
                        text = ReplaceOrAppendEnvKey(text, "DB_PASSWORD", txtDbPass.Text);
                        File.WriteAllText(envFile, text, Encoding.UTF8);
                        Log("Created .env from .env.example and filled DB credentials from UI.");
                    }
                    else
                    {
                        Log(".env already exists or .env.example missing. Skipping .env creation.");
                    }
                }
                catch (Exception ex)
                {
                    Log("Failed to create .env: " + ex.Message);
                }

                progressBar.Value = 100;
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
                Log("Fetching updates...");
                var env = CreateGitSshEnv(_privateKeyPath);

                // stash local changes if any
                var status = await RunProcessCaptureWithProcess("git", "status --porcelain", env, targetFolder);
                bool hasLocalChanges = !string.IsNullOrWhiteSpace(status.stdout);
                if (hasLocalChanges)
                {
                    Log("Local changes detected. Stashing before pull.");
                    var stash = await RunProcessCaptureWithProcess("git", "stash --include-untracked", env, targetFolder);
                    Log($"stash exit {stash.code}");
                }

                var fetch = await RunProcessCaptureWithProcess("git", "fetch --all --prune", env, targetFolder);
                Log($"fetch exit {fetch.code}");
                var merge = await RunProcessCaptureWithProcess("git", "merge --ff-only @{u}", env, targetFolder);
                if (merge.code != 0)
                {
                    // try pull --rebase fallback
                    Log("Fast-forward failed. Trying pull --rebase.");
                    var pull = await RunProcessCaptureWithProcess("git", "pull --rebase", env, targetFolder);
                    Log($"pull exit {pull.code}");
                    if (pull.code != 0) Log("Pull failed or conflicts occurred. Check repository manually.");
                }
                else
                {
                    Log("Fast-forward merge succeeded.");
                }

                // try pop stash if any
                if (hasLocalChanges)
                {
                    var pop = await RunProcessCaptureWithProcess("git", "stash pop", env, targetFolder);
                    Log($"stash pop exit {pop.code}");
                }
                progressBar.Value = 100;
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
            var host = txtDbHost.Text.Trim();
            var port = txtDbPort.Text.Trim();
            var db = txtDbName.Text.Trim();
            var user = txtDbUser.Text.Trim();
            var pass = txtDbPass.Text;

            // try locate mysql.exe in XAMPP
            var mysqlExe = Path.Combine("C:\\", "xampp", "mysql", "bin", "mysql.exe");
            if (!File.Exists(mysqlExe)) mysqlExe = "mysql"; // fallback to PATH

            try
            {
                progressBar.Value = 0;
                Log($"Executing SQL file '{sqlPath}' on {user}@{host}:{port}/{db} using {mysqlExe}");

                // read file content
                var sqlText = File.ReadAllText(sqlPath, Encoding.UTF8);

                // prepare arguments. we will feed SQL via stdin to avoid shell redirection issues.
                var args = $"-h {host} -P {port} -u {user} " + (string.IsNullOrEmpty(pass) ? "" : $"-p{pass} ") + $"{db}";
                var r = await RunProcessWithStdinCaptureWithProcess(mysqlExe, args, sqlText, null);
                Log($"mysql exit {r.code}");
                if (!string.IsNullOrWhiteSpace(r.stdout)) Log(r.stdout);
                if (!string.IsNullOrWhiteSpace(r.stderr)) Log("ERR: " + r.stderr);
                MessageBox.Show(r.code == 0 ? "SQL executed." : $"SQL execution failed (code {r.code}). See log.");
                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Log("SQL exec error: " + ex.Message);
                MessageBox.Show("SQL exec error: " + ex.Message);
            }
        }

        // Composer install with auto-update fallback
        private async void BtnComposerInstall_Click(object? sender, EventArgs e)
        {
            var targetFolder = txtInstallFolder.Text?.Trim();
            if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
            {
                MessageBox.Show("Select project folder.");
                return;
            }
            try
            {
                progressBar.Value = 0;
                Log("Running composer install...");
                var composer = "composer";
                var composerPhar = Path.Combine(targetFolder, "composer.phar");
                var usePhp = false;
                string phpExe = "php";
                var xamppPhp = Path.Combine("C:\\", "xampp", "php", "php.exe");
                if (File.Exists(xamppPhp)) phpExe = xamppPhp;
                if (File.Exists(composerPhar))
                {
                    usePhp = true;
                }

                Task<(int code, string stdout, string stderr)> task;
                if (usePhp)
                {
                    task = RunProcessCaptureWithProcess(phpExe, $"\"{composerPhar}\" install --no-interaction --prefer-dist", null, targetFolder);
                }
                else
                {
                    task = RunProcessCaptureWithProcess(composer, "install --no-interaction --prefer-dist", null, targetFolder);
                }
                var r = await task;
                Log($"composer install exit {r.code}");
                if (!string.IsNullOrWhiteSpace(r.stdout)) Log(r.stdout);
                if (!string.IsNullOrWhiteSpace(r.stderr)) Log("ERR: " + r.stderr);

                // Detect failure or lock incompatibility
                if (r.code != 0 || (r.stderr != null && r.stderr.Contains("Your lock file does not contain a compatible set of packages", StringComparison.OrdinalIgnoreCase)))
                {
                    Log("Composer install failed or lock incompatible. Running composer update...");
                    Task<(int code, string stdout, string stderr)> updateTask;
                    if (usePhp)
                    {
                        updateTask = RunProcessCaptureWithProcess(phpExe, $"\"{composerPhar}\" update --no-interaction --prefer-dist", null, targetFolder);
                    }
                    else
                    {
                        updateTask = RunProcessCaptureWithProcess(composer, "update --no-interaction --prefer-dist", null, targetFolder);
                    }
                    var r2 = await updateTask;
                    Log($"composer update exit {r2.code}");
                    if (!string.IsNullOrWhiteSpace(r2.stdout)) Log(r2.stdout);
                    if (!string.IsNullOrWhiteSpace(r2.stderr)) Log("ERR: " + r2.stderr);

                    if (r2.code == 0)
                        MessageBox.Show("Composer update finished successfully after install failed.");
                    else
                        MessageBox.Show($"Composer update also failed (code {r2.code}). Check the log.");
                }
                else
                {
                    MessageBox.Show("Composer install finished successfully.");
                }

                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Log("Composer error: " + ex.Message);
                MessageBox.Show("Composer error: " + ex.Message);
            }
        }

        // php artisan migrate
        private async void BtnMigrate_Click(object? sender, EventArgs e)
        {
            var targetFolder = txtInstallFolder.Text?.Trim();
            if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
            {
                MessageBox.Show("Select project folder.");
                return;
            }
            try
            {
                progressBar.Value = 0;
                Log("Running php artisan migrate --force ...");
                var phpExe = "php";
                var xamppPhp = Path.Combine("C:\\", "xampp", "php", "php.exe");
                if (File.Exists(xamppPhp)) phpExe = xamppPhp;

                var artisan = Path.Combine(targetFolder, "artisan");
                if (!File.Exists(artisan))
                {
                    MessageBox.Show("artisan not found in project folder. Is this a Laravel project?");
                    return;
                }

                var r = await RunProcessCaptureWithProcess(phpExe, $"\"{artisan}\" migrate --force", null, targetFolder);
                Log($"migrate exit {r.code}");
                if (!string.IsNullOrWhiteSpace(r.stdout)) Log(r.stdout);
                if (!string.IsNullOrWhiteSpace(r.stderr)) Log("ERR: " + r.stderr);
                MessageBox.Show(r.code == 0 ? "Migrate finished." : $"Migrate failed (code {r.code}). See log.");
                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Log("Migrate error: " + ex.Message);
                MessageBox.Show("Migrate error: " + ex.Message);
            }
        }

        // --- Enable PHP zip extension (edits php.ini) ---
        private async void BtnEnablePhpZip_Click(object? sender, EventArgs e)
        {
            // must be admin to edit system files and restart Apache
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show("This operation requires administrator rights. Restart the app as Administrator and try again.");
                return;
            }

            try
            {
                Log("Locating CLI php.ini...");
                // Try to find php.ini via php CLI (best)
                var phpIniPath = await GetPhpIniPathFromPhpCli();
                if (string.IsNullOrEmpty(phpIniPath))
                {
                    // fallback to common XAMPP path
                    var candidate = Path.Combine("C:\\", "xampp", "php", "php.ini");
                    if (File.Exists(candidate)) phpIniPath = candidate;
                }

                if (string.IsNullOrEmpty(phpIniPath) || !File.Exists(phpIniPath))
                {
                    MessageBox.Show("Could not locate php.ini. Ensure PHP is installed or specify php.ini manually. Check XAMPP path C:\\xampp\\php\\php.ini");
                    Log("php.ini not found.");
                    return;
                }

                Log($"php.ini found: {phpIniPath}");
                var confirm = MessageBox.Show($"Will backup and modify:\n{phpIniPath}\n\nThis will uncomment or add 'extension=zip' in php.ini.\nProceed?", "Enable zip extension", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;

                // Backup
                var bak = phpIniPath + ".deploykeybak." + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(phpIniPath, bak, overwrite: false);
                Log($"Backup created: {bak}");

                // Read, modify, write to temp then replace
                var text = File.ReadAllText(phpIniPath, Encoding.UTF8);
                var origText = text;

                // Ensure extension_dir is set sensibly if not present
                if (!Regex.IsMatch(text, @"^\s*extension_dir\s*=", RegexOptions.Multiline))
                {
                    // attempt to set extension_dir to ext relative to php.ini folder
                    var phpFolder = Path.GetDirectoryName(phpIniPath) ?? Path.GetDirectoryName(System.Environment.SystemDirectory) ?? "";
                    var extCandidate = Path.Combine(phpFolder, "ext").Replace('\\', '/');
                    text = $"extension_dir = \"{extCandidate}\"\r\n" + text;
                    Log($"Prepending extension_dir = \"{extCandidate}\"");
                }

                // Try to uncomment existing `;extension=zip` or `; extension=zip` or `;extension=zip.dll`
                var pattern = @"^[\s;]*extension\s*=\s*(?:[""']?zip(?:\.dll)?[""']?)\s*$";
                if (Regex.IsMatch(text, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    text = Regex.Replace(text, pattern, "extension = zip", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    Log("Uncommented existing extension=zip line.");
                }
                else
                {
                    // If no matching line, try to find a nearby spot to add the extension line. Add near [PHP_EXTENSIONS] or at end.
                    if (text.Contains("[PHP]", StringComparison.OrdinalIgnoreCase))
                    {
                        // append after [PHP] header if present
                        text = Regex.Replace(text, @"(\[PHP\].*?\r?\n)", $"$1extension = zip\r\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        Log("Inserted extension=zip after [PHP] section if present.");
                    }
                    else
                    {
                        text = text + Environment.NewLine + "extension = zip" + Environment.NewLine;
                        Log("Appended extension=zip to php.ini");
                    }
                }

                // Write to temp and replace
                var tmp = phpIniPath + ".tmp";
                File.WriteAllText(tmp, text, Encoding.UTF8);
                File.Replace(tmp, phpIniPath, bak + ".replacebackup");
                Log("php.ini updated (atomic replace).");

                // Attempt to restart Apache so CLI or Apache picks up change
                await TryRestartApacheAsync();

                // Show result: check php -m or php -r to see if zip is enabled
                var phpExe = "php";
                var xamppPhp = Path.Combine("C:\\", "xampp", "php", "php.exe");
                if (File.Exists(xamppPhp)) phpExe = xamppPhp;
                var check = await RunProcessCaptureWithProcess(phpExe, "-m", null, null);
                var modules = check.stdout ?? "";
                if (modules.IndexOf("zip", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MessageBox.Show("PHP zip extension appears enabled. Composer should be able to download zip packages now.");
                    Log("zip present in php -m output.");
                }
                else
                {
                    MessageBox.Show("php.ini changed. zip not detected in `php -m`. You may need to ensure php_zip.dll exists in the PHP ext folder or restart Apache/CLI session manually.");
                    Log("zip not detected in php -m output. Manual steps may be required.");
                }
            }
            catch (Exception ex)
            {
                Log("Enable PHP zip error: " + ex.Message);
                MessageBox.Show("Enable PHP zip error: " + ex.Message);
            }
        }

        // Try to determine php.ini via php CLI
        private async Task<string?> GetPhpIniPathFromPhpCli()
        {
            try
            {
                // try XAMPP PHP first
                var xamppPhp = Path.Combine("C:\\", "xampp", "php", "php.exe");
                string phpCmd = "php";
                if (File.Exists(xamppPhp)) phpCmd = xamppPhp;

                var r = await RunProcessCaptureWithProcess(phpCmd, "-r \"echo php_ini_loaded_file();\"", null, null);
                if (r.code == 0 && !string.IsNullOrWhiteSpace(r.stdout))
                {
                    var path = r.stdout.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
                }
            }
            catch (Exception ex)
            {
                Log("GetPhpIniPathFromPhpCli failed: " + ex.Message);
            }
            return null;
        }

        // --- Virtual host creation ---
        private async void BtnCreateVHost_Click(object? sender, EventArgs e)
        {
            var domain = txtVHostDomain.Text?.Trim();
            var targetFolder = txtInstallFolder.Text?.Trim();
            if (string.IsNullOrEmpty(domain))
            {
                MessageBox.Show("Enter domain name (e.g. myapp.local).");
                return;
            }
            if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
            {
                MessageBox.Show("Select project folder (DocumentRoot).");
                return;
            }

            if (!IsRunningAsAdmin())
            {
                MessageBox.Show("Virtual host creation requires administrator privileges. Restart app as administrator and try again.");
                return;
            }

            try
            {
                // Paths
                var xamppRoot = Path.Combine("C:\\", "xampp");
                var vhostsFile = Path.Combine(xamppRoot, "apache", "conf", "extra", "httpd-vhosts.conf");
                var hostsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System).Replace("\\system32", ""), "drivers\\etc\\hosts");
                var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (!File.Exists(hostsFile) && File.Exists(hostsPath)) hostsFile = hostsPath;

                // Create vhost entry
                var vhostEntry = new StringBuilder();
                vhostEntry.AppendLine();
                vhostEntry.AppendLine($"# Added by Deploy-Key Git Client on {DateTime.Now:yyyy-MM-dd HH:mm}");
                vhostEntry.AppendLine($"<VirtualHost *:80>");
                vhostEntry.AppendLine($"    ServerName {domain}");
                vhostEntry.AppendLine($"    DocumentRoot \"{targetFolder.Replace('\\', '/')}\""); 
                vhostEntry.AppendLine($"    <Directory \"{targetFolder.Replace('\\', '/')}\">");
                vhostEntry.AppendLine($"        Require all granted");
                vhostEntry.AppendLine($"        AllowOverride All");
                vhostEntry.AppendLine($"    </Directory>");
                vhostEntry.AppendLine($"</VirtualHost>");
                vhostEntry.AppendLine();

                // Append to vhosts file
                if (!File.Exists(vhostsFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(vhostsFile) ?? throw new Exception("Invalid vhosts path"));
                    File.WriteAllText(vhostsFile, vhostEntry.ToString(), Encoding.UTF8);
                    Log($"Created vhosts file at {vhostsFile}");
                }
                else
                {
                    File.AppendAllText(vhostsFile, vhostEntry.ToString(), Encoding.UTF8);
                    Log($"Appended vhost to {vhostsFile}");
                }

                // Append hosts entry
                var hostsLine = $"127.0.0.1 {domain}";
                var hostsText = File.ReadAllText(hostsFile);
                if (!hostsText.Contains(hostsLine))
                {
                    File.AppendAllText(hostsFile, Environment.NewLine + hostsLine + Environment.NewLine, Encoding.UTF8);
                    Log($"Appended hosts entry to {hostsFile}");
                }
                else
                {
                    Log("Hosts entry already exists.");
                }

                MessageBox.Show($"Virtual host entry written.\n\n- {vhostsFile}\n- {hostsFile}\n\nYou may need to restart Apache. The app will attempt to restart Apache now if possible (admin required).");
                // Attempt to restart Apache gracefully
                await TryRestartApacheAsync();
            }
            catch (Exception ex)
            {
                Log("Create vhost error: " + ex.Message);
                MessageBox.Show("Create vhost error: " + ex.Message);
            }
        }

        // Try restart Apache using XAMPP httpd or service; best-effort
        private async Task TryRestartApacheAsync()
        {
            try
            {
                // try apache bin
                var apacheBin = Path.Combine("C:\\", "xampp", "apache", "bin", "httpd.exe");
                if (File.Exists(apacheBin))
                {
                    Log("Restarting Apache via httpd.exe -k restart ...");
                    var r = await RunProcessCaptureWithProcess(apacheBin, "-k restart", null, null);
                    Log($"httpd exit {r.code}");
                    if (!string.IsNullOrWhiteSpace(r.stdout)) Log(r.stdout);
                    if (!string.IsNullOrWhiteSpace(r.stderr)) Log("ERR: " + r.stderr);
                    MessageBox.Show("Attempted to restart Apache. Check Apache control panel or service status.");
                    return;
                }

                // fallback: try 'net' stop/start of Apache service name common in XAMPP: Apache2.4
                var svcName = "Apache2.4";
                Log($"Attempting to restart service '{svcName}'");
                var stop = await RunProcessCaptureWithProcess("net", $"stop {svcName}", null, null);
                Log($"net stop exit {stop.code}");
                var start = await RunProcessCaptureWithProcess("net", $"start {svcName}", null, null);
                Log($"net start exit {start.code}");
                MessageBox.Show("Tried to restart Apache service. If that failed, restart XAMPP Apache manually.");
            }
            catch (Exception ex)
            {
                Log("Apache restart failed: " + ex.Message);
                MessageBox.Show("Apache restart failed. Restart XAMPP Apache manually.");
            }
        }

        // Register device - reads ProcessorId via PowerShell and posts to provided endpoint
        private async void BtnRegisterDevice_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dlg = new Form { Width = 520, Height = 220, Text = "Register Device" };
                var lblUrl = new Label { Text = "Endpoint URL (POST):", Left = 8, Top = 12, Width = 120 };
                var txtUrl = new TextBox { Left = 140, Top = 8, Width = 360, Text = "http://localhost/backoffice/check" };
                var lblEmail = new Label { Text = "Email:", Left = 8, Top = 48, Width = 120 };
                var txtEmail = new TextBox { Left = 140, Top = 44, Width = 360 };
                var lblPass = new Label { Text = "Password:", Left = 8, Top = 84, Width = 120 };
                var txtPass = new TextBox { Left = 140, Top = 80, Width = 360, UseSystemPasswordChar = true };
                var btnOk = new Button { Text = "OK", Left = 300, Top = 120, Width = 80, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 400, Top = 120, Width = 80, DialogResult = DialogResult.Cancel };
                dlg.Controls.AddRange(new Control[] { lblUrl, txtUrl, lblEmail, txtEmail, lblPass, txtPass, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                if (dlg.ShowDialog() != DialogResult.OK) return;

                var endpoint = txtUrl.Text.Trim();
                var email = txtEmail.Text.Trim();
                var pass = txtPass.Text;
                if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass)) { MessageBox.Show("Provide endpoint, email and password."); return; }

                Log("Getting ProcessorId via PowerShell...");
                var psResult = await RunProcessCaptureWithProcess("powershell", "-NoProfile -Command \"Get-CimInstance Win32_Processor | Select-Object -ExpandProperty ProcessorId\"", null, null);
                var processorId = (psResult.stdout ?? "").Trim();
                Log($"ProcessorId: {processorId}");

                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                var content = new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string,string>("email", email),
                    new KeyValuePair<string,string>("password", pass),
                    new KeyValuePair<string,string>("processorId", processorId)
                });

                Log($"Posting registration to {endpoint} ...");
                var resp = await http.PostAsync(endpoint, content);
                var body = await resp.Content.ReadAsStringAsync();
                Log($"POST status: {(int)resp.StatusCode}. Body: {body}");
                MessageBox.Show($"Server returned {(int)resp.StatusCode}. See log for response body.");
            }
            catch (Exception ex)
            {
                Log("Register device error: " + ex.Message);
                MessageBox.Show("Register device error: " + ex.Message);
            }
        }

        // Apply API URL by editing OrderController and marking skip-worktree
        private async void BtnApplyApiUrl_Click(object? sender, EventArgs e)
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

            var rel = "app/Http/Controllers/Sales/OrderController.php";
            var file = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file))
            {
                MessageBox.Show($"Controller not found at {file}");
                return;
            }

            try
            {
                var bak = file + ".deploybak." + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(file, bak);
                Log($"Backup created: {bak}");

                var text = File.ReadAllText(file, Encoding.UTF8);

                // find Http::post('#', and replace the '#' with quoted url
                var replaced = Regex.Replace(text, @"Http::post\(\s*(['""]?)#\1\s*,", $"Http::post('{url}',", RegexOptions.IgnoreCase);
                if (replaced == text)
                {
                    // fallback: replace Http::post('#' with Http::post('url'
                    replaced = text.Replace("Http::post('#'", $"Http::post('{url}'");
                }

                File.WriteAllText(file, replaced, Encoding.UTF8);
                Log("OrderController.php patched with API URL.");

                // mark skip-worktree so git pulls won't overwrite (best-effort)
                await ToggleSkipWorktreeInternal(projectRoot, rel, setSkip: true);
                MessageBox.Show("API URL applied and file marked skip-worktree (protected).");
            }
            catch (Exception ex)
            {
                Log("Apply API URL error: " + ex.Message);
                MessageBox.Show("Apply API URL error: " + ex.Message);
            }
        }

        private async void BtnToggleSkipWorktree_Click(object? sender, EventArgs e)
        {
            var rel = txtSkipPath.Text?.Trim();
            var projectRoot = txtInstallFolder.Text?.Trim();
            if (string.IsNullOrEmpty(rel) || string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
            {
                MessageBox.Show("Set project folder and relative path to toggle.");
                return;
            }

            try
            {
                await ToggleSkipWorktreeInternal(projectRoot, rel, setSkip: null); // toggle
                MessageBox.Show("Skip-worktree toggle attempted. Check log.");
            }
            catch (Exception ex)
            {
                Log("Toggle skip-worktree error: " + ex.Message);
                MessageBox.Show("Toggle skip-worktree error: " + ex.Message);
            }
        }

        private async Task ToggleSkipWorktreeInternal(string projectRoot, string relPath, bool? setSkip)
        {
            var path = relPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var gitArgsSet = $"update-index --skip-worktree \"{path}\"";
            var gitArgsUnset = $"update-index --no-skip-worktree \"{path}\"";
            var checkArgs = $"ls-files -v \"{path}\"";

            var env = CreateGitSshEnv(_privateKeyPath);
            var check = await RunProcessCaptureWithProcess("git", checkArgs, env, projectRoot);
            Log($"git ls-files exit {check.code} stdout: {check.stdout}");
            var isSkipped = (check.stdout ?? "").Split('\n').Any(l => l.Length > 0 && l[0] == 'S'); // 'S' indicates skip-worktree in some git versions

            if (setSkip == true)
            {
                var r = await RunProcessCaptureWithProcess("git", gitArgsSet, env, projectRoot);
                Log($"git {gitArgsSet} exit {r.code}");
            }
            else if (setSkip == false)
            {
                var r = await RunProcessCaptureWithProcess("git", gitArgsUnset, env, projectRoot);
                Log($"git {gitArgsUnset} exit {r.code}");
            }
            else
            {
                if (isSkipped)
                {
                    var r = await RunProcessCaptureWithProcess("git", gitArgsUnset, env, projectRoot);
                    Log($"git {gitArgsUnset} exit {r.code}");
                    Log($"Unmarked skip-worktree for {path}");
                }
                else
                {
                    var r = await RunProcessCaptureWithProcess("git", gitArgsSet, env, projectRoot);
                    Log($"git {gitArgsSet} exit {r.code}");
                    Log($"Marked skip-worktree for {path}");
                }
            }
        }

        // --- helpers ---

        // Convert https -> git@host:owner/repo.git if necessary
        private string ConvertToSshIfHttps(string url)
        {
            if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase) || url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
                return url;
            var m = Regex.Match(url, @"https?://([^/]+)/(.+)$");
            if (!m.Success) return url;
            var host = m.Groups[1].Value;
            var path = m.Groups[2].Value.Trim('/');
            return $"git@{host}:{path}";
        }

        private static string? DeriveRepoName(string url)
        {
            try
            {
                var cleaned = url.Trim();
                if (cleaned.EndsWith("/")) cleaned = cleaned[..^1];
                var parts = cleaned.Split(new[] { '/', ':' }, StringSplitOptions.RemoveEmptyEntries);
                var last = parts.LastOrDefault();
                if (string.IsNullOrEmpty(last)) return null;
                if (last.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) last = last[..^4];
                foreach (var c in Path.GetInvalidFileNameChars()) last = last.Replace(c, '_');
                return last;
            }
            catch { return null; }
        }

        // Replace or append env key/value in .env text
        private static string ReplaceOrAppendEnvKey(string envText, string key, string value)
        {
            if (value == null) value = "";
            var pattern = $"^{Regex.Escape(key)}=.*$";
            if (Regex.IsMatch(envText, pattern, RegexOptions.Multiline))
            {
                envText = Regex.Replace(envText, pattern, $"{key}={EscapeEnvValue(value)}", RegexOptions.Multiline);
            }
            else
            {
                envText += Environment.NewLine + $"{key}={EscapeEnvValue(value)}" + Environment.NewLine;
            }
            return envText;
        }
        private static string EscapeEnvValue(string v)
        {
            if (v == null) return "";
            if (v.Contains(' ') || v.Contains('"')) return "\"" + v.Replace("\"", "\\\"") + "\"";
            return v;
        }

        // RSA keygen and pub key builder
        public (string privatePem, string publicSsh) GenerateRsaOpenSshKeyPair(int bits = 4096, string comment = "deploy@client")
        {
            using var rsa = RSA.Create(bits);
            var pkcs1 = rsa.ExportRSAPrivateKey();
            string privPem = PemEncode("RSA PRIVATE KEY", pkcs1);

            var rsaParams = rsa.ExportParameters(false);
            var pubKey = BuildSshRsaPublicKey(rsaParams.Exponent, rsaParams.Modulus);
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
            if (value == null || value.Length == 0) return new byte[] { 0 };

            int leadingZeros = 0;
            while (leadingZeros < value.Length && value[leadingZeros] == 0) leadingZeros++;
            var trimmed = value.Skip(leadingZeros).ToArray();
            if (trimmed.Length == 0) trimmed = new byte[] { 0 };

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

        // Create environment override for GIT_SSH_COMMAND using a temp known_hosts file
        private (string, string)[] CreateGitSshEnv(string privateKeyPath)
        {
            try
            {
                var knownHosts = Path.Combine(Path.GetTempPath(), $"ssh_known_hosts_{Guid.NewGuid():N}.txt");
                File.WriteAllText(knownHosts, string.Empty);
                var gitSsh = $"ssh -i \"{privateKeyPath}\" -o IdentitiesOnly=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=\"{knownHosts}\"";
                Log($"Using temporary known_hosts: {knownHosts}");
                return new[] { ("GIT_SSH_COMMAND", gitSsh) };
            }
            catch (Exception ex)
            {
                Log("Failed to create temp known_hosts file: " + ex.Message);
                var gitSsh = $"ssh -i \"{privateKeyPath}\" -o IdentitiesOnly=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL";
                return new[] { ("GIT_SSH_COMMAND", gitSsh) };
            }
        }

        // --- Process runners that attach currentProcess so Cancel works ---

        private Task<(int code, string stdout, string stderr)> RunProcessCaptureWithProcess(string file, string args, (string, string)[]? env = null, string? workingDir = null)
        {
            var tcs = new TaskCompletionSource<(int, string, string)>();
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? Environment.CurrentDirectory : workingDir
            };
            if (env != null)
            {
                foreach (var kv in env) psi.Environment[kv.Item1] = kv.Item2;
            }

            try
            {
                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                lock (_procLock) _currentProcess = proc;
                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) { sbOut.AppendLine(e.Data); Log(e.Data); } };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) { sbErr.AppendLine(e.Data); Log("ERR: " + e.Data); } };
                proc.Exited += (s, e) =>
                {
                    lock (_procLock) _currentProcess = null;
                    tcs.SetResult((proc.ExitCode, sbOut.ToString(), sbErr.ToString()));
                    proc.Dispose();
                };
                if (!proc.Start()) tcs.SetException(new Exception("Failed to start process"));
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                lock (_procLock) _currentProcess = null;
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        // wrapper used by older code that doesn't need stdin
        private Task<(int code, string stdout, string stderr)> RunProcessCapture(string file, string args, (string, string)[]? env = null, string? workingDir = null)
            => RunProcessCaptureWithProcess(file, args, env, workingDir);

        // Run a process and feed stdin text, capture stdout/stderr
        private Task<(int code, string stdout, string stderr)> RunProcessWithStdinCaptureWithProcess(string file, string args, string stdinText, string? workingDir)
        {
            var tcs = new TaskCompletionSource<(int, string, string)>();
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? Environment.CurrentDirectory : workingDir
            };
            try
            {
                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                lock (_procLock) _currentProcess = proc;
                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) { sbOut.AppendLine(e.Data); Log(e.Data); } };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) { sbErr.AppendLine(e.Data); Log("ERR: " + e.Data); } };
                proc.Exited += (s, e) =>
                {
                    lock (_procLock) _currentProcess = null;
                    tcs.SetResult((proc.ExitCode, sbOut.ToString(), sbErr.ToString()));
                    proc.Dispose();
                };
                if (!proc.Start()) tcs.SetException(new Exception("Failed to start process"));
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                using (var sw = proc.StandardInput)
                {
                    sw.Write(stdinText);
                }
            }
            catch (Exception ex)
            {
                lock (_procLock) _currentProcess = null;
                tcs.SetException(ex);
            }
            return tcs.Task;
        }

        // Helper: recursive copy including hidden files, preserving attributes (best-effort)
        private void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            var src = new DirectoryInfo(sourceDir);
            var dst = new DirectoryInfo(targetDir);

            if (!dst.Exists) dst.Create();

            // copy files
            foreach (var file in src.GetFiles())
            {
                var targetFilePath = Path.Combine(dst.FullName, file.Name);
                // overwrite if exists
                file.CopyTo(targetFilePath, overwrite: true);
                try { File.SetAttributes(targetFilePath, file.Attributes); } catch { }
            }

            // copy directories
            foreach (var dir in src.GetDirectories())
            {
                var targetSubDir = Path.Combine(dst.FullName, dir.Name);
                CopyDirectoryRecursive(dir.FullName, targetSubDir);
            }
        }

        // Safe delete helper with retries
        private void TryDeleteDirectory(string path)
        {
            const int maxAttempts = 5;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                    return;
                }
                catch (IOException ioEx)
                {
                    Log($"Delete attempt {attempt} failed: {ioEx.Message}");
                    System.Threading.Thread.Sleep(200 * attempt);
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    Log($"Delete attempt {attempt} failed: {uaEx.Message}");
                    System.Threading.Thread.Sleep(200 * attempt);
                }
                catch (Exception ex)
                {
                    Log($"Delete error: {ex.Message}");
                    break;
                }
            }
            throw new Exception($"Unable to delete directory '{path}'. Close open handles and try again.");
        }

        private bool IsRunningAsAdmin()
        {
            try
            {
                var wi = WindowsIdentity.GetCurrent();
                var wp = new WindowsPrincipal(wi);
                return wp.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
