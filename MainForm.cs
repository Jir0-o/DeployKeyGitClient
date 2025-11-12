using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeployKeyGitClient
{
    public class MainForm : Form
    {
        private TextBox txtInstallFolder;
        private Button btnBrowse;
        private TextBox txtGitUrl;
        private Button btnGenerate;
        private Button btnSaveKeys;
        private Button btnCopy;
        private Button btnClone;
        private Button btnPull;
        private ProgressBar progressBar;
        private TextBox txtLog;

        private Button btnCancelOp;
        private Button btnSelectSql;
        private TextBox txtSqlPath;
        private Button btnExecSql;
        private TextBox txtDbHost, txtDbPort, txtDbName, txtDbUser, txtDbPass;
        private Button btnComposerInstall, btnMigrate;
        private Button btnEnablePhpZip;
        private TextBox txtVHostDomain;
        private Button btnCreateVHost;
        private Button btnRegisterDevice;
        private Button btnApplyApiUrl;
        private TextBox txtApiUrl;
        private TextBox txtSkipPath;
        private Button btnToggleSkipWorktree;
        private Button btnBackupDb;
        private Button btnGenerateEnv;

        private string? _privatePem;
        private string? _publicSsh;
        private string? _privateKeyPath;
        private Process? _currentProcess;
        private readonly object _procLock = new object();

        public MainForm()
        {
            Text = "Deploy-Key Git Client";
            Width = 1040;
            Height = 950;
            InitUi();
        }

        private void InitUi()
        {
            var pad = 8;
            var left = pad;
            var y = pad;

            Controls.Add(new Label { Text = "Install/Repo folder:", Left = left, Top = y + 6, Width = 140 });
            txtInstallFolder = new TextBox { Left = 160, Top = y, Width = 640 };
            Controls.Add(txtInstallFolder);
            btnBrowse = new Button { Text = "Browse...", Left = 810, Top = y, Width = 180 };
            btnBrowse.Click += BtnBrowse_Click;
            Controls.Add(btnBrowse);

            y += 36;
            Controls.Add(new Label { Text = "Git repo URL (HTTPS or SSH):", Left = left, Top = y + 6, Width = 200 });
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
            Controls.Add(new Label { Text = "Public deploy key (add to repo Settings → Deploy keys):", Left = left, Top = y + 6, Width = 420 });
            y += 20;
            var txtPublicKey = new TextBox { Left = left, Top = y, Width = 980, Height = 80, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            Controls.Add(txtPublicKey);

            y += 100;
            btnClone = new Button { Text = "Clone Repo", Left = left, Top = y, Width = 260 };
            btnClone.Click += BtnClone_Click;
            Controls.Add(btnClone);

            btnPull = new Button { Text = "Pull / Update", Left = left + 270, Top = y, Width = 160 };
            btnPull.Click += BtnPull_Click;
            Controls.Add(btnPull);

            progressBar = new ProgressBar { Left = left + 440, Top = y + 6, Width = 540, Height = 24 };
            Controls.Add(progressBar);

            // Database UI
            y += 44;
            Controls.Add(new Label { Text = "Local DB / SQL (XAMPP)", Left = left, Top = y + 6, Width = 200 });
            y += 28;
            txtDbHost = new TextBox { Left = left + 40, Top = y, Width = 120, Text = "127.0.0.1" };
            Controls.Add(new Label { Text = "Host:", Left = left, Top = y + 6 });
            Controls.Add(txtDbHost);
            txtDbPort = new TextBox { Left = left + 220, Top = y, Width = 60, Text = "3306" };
            Controls.Add(new Label { Text = "Port:", Left = left + 180, Top = y + 6 });
            Controls.Add(txtDbPort);
            txtDbName = new TextBox { Left = left + 330, Top = y, Width = 160, Text = "laravel" };
            Controls.Add(new Label { Text = "DB:", Left = left + 300, Top = y + 6 });
            Controls.Add(txtDbName);
            txtDbUser = new TextBox { Left = left + 540, Top = y, Width = 120, Text = "root" };
            Controls.Add(new Label { Text = "User:", Left = left + 500, Top = y + 6 });
            Controls.Add(txtDbUser);
            txtDbPass = new TextBox { Left = left + 720, Top = y, Width = 160, UseSystemPasswordChar = true };
            Controls.Add(new Label { Text = "Pass:", Left = left + 680, Top = y + 6 });
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

            y += 44;
            btnBackupDb = new Button { Text = "Backup Database", Left = left, Top = y, Width = 260 };
            btnBackupDb.Click += BtnBackupDb_Click;
            Controls.Add(btnBackupDb);

            btnGenerateEnv = new Button { Text = "Generate .env File", Left = left + 270, Top = y, Width = 240 };
            btnGenerateEnv.Click += BtnGenerateEnv_Click;
            Controls.Add(btnGenerateEnv);

            btnComposerInstall = new Button { Text = "Composer Install", Left = left + 540, Top = y, Width = 220 };
            btnComposerInstall.Click += BtnComposerInstall_Click;
            Controls.Add(btnComposerInstall);

            y += 44;
            btnMigrate = new Button { Text = "Run Migrations", Left = left, Top = y, Width = 260 };
            btnMigrate.Click += BtnMigrate_Click;
            Controls.Add(btnMigrate);

            btnEnablePhpZip = new Button { Text = "Enable PHP Zip Extension", Left = left + 270, Top = y, Width = 240 };
            btnEnablePhpZip.Click += BtnEnablePhpZip_Click;
            Controls.Add(btnEnablePhpZip);

            y += 44;
            btnRegisterDevice = new Button { Text = "Register Device", Left = left, Top = y, Width = 260 };
            btnRegisterDevice.Click += BtnRegisterDevice_Click;
            Controls.Add(btnRegisterDevice);

            txtApiUrl = new TextBox { Left = left + 270, Top = y, Width = 400, Text = "https://api.example.com/sync" };
            Controls.Add(txtApiUrl);

            btnApplyApiUrl = new Button { Text = "Apply API URL", Left = left + 680, Top = y, Width = 200 };
            btnApplyApiUrl.Click += BtnApplyApiUrl_Click;
            Controls.Add(btnApplyApiUrl);

            y += 44;
            txtSkipPath = new TextBox { Left = left, Top = y, Width = 750, Text = "app/Http/Controllers/Sales/OrderController.php" };
            Controls.Add(txtSkipPath);

            btnToggleSkipWorktree = new Button { Text = "Toggle Skip-Worktree", Left = left + 760, Top = y, Width = 180 };
            btnToggleSkipWorktree.Click += BtnToggleSkipWorktree_Click;
            Controls.Add(btnToggleSkipWorktree);

            y += 44;
            txtLog = new TextBox { Left = left, Top = y, Width = 980, Height = 300, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true };
            Controls.Add(txtLog);
        }

        private void Log(string msg)
        {
            if (txtLog.InvokeRequired)
                txtLog.Invoke(new Action(() => txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n")));
            else
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
        }

        // ---------------- BUTTON ACTIONS ----------------

        private void BtnBrowse_Click(object? s, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                txtInstallFolder.Text = dlg.SelectedPath;
        }

        private async void BtnClone_Click(object? s, EventArgs e)
        {
            try
            {
                await AppLogic.CloneIntoTempThenMoveAsync(
                    txtGitUrl.Text.Trim(),
                    txtInstallFolder.Text.Trim(),
                    v => progressBar.Value = v,
                    Log,
                    _privateKeyPath
                );
                MessageBox.Show("Clone completed successfully.");
            }
            catch (Exception ex)
            {
                Log("Clone error: " + ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private async void BtnPull_Click(object? s, EventArgs e)
        {
            try
            {
                await AppLogic.PullUpdateAsync(txtInstallFolder.Text.Trim(), v => progressBar.Value = v, Log, _privateKeyPath);
                MessageBox.Show("Repository updated successfully.");
            }
            catch (Exception ex)
            {
                Log("Pull error: " + ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private async void BtnExecSql_Click(object? s, EventArgs e)
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
                bool ok = await AppLogic.ImportSqlFileAsync(txtSqlPath.Text.Trim(), db, Log);
                MessageBox.Show(ok ? "SQL executed successfully." : "SQL execution failed.");
            }
            catch (Exception ex)
            {
                Log("SQL exec error: " + ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private void BtnSelectSql_Click(object? s, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "SQL files|*.sql" };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtSqlPath.Text = dlg.FileName;
        }

        private async void BtnBackupDb_Click(object? s, EventArgs e)
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
                var backupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{db.Database}_backup_{DateTime.Now:yyyyMMddHHmmss}.sql");
                bool ok = await AppLogic.BackupDatabaseAsync(db, backupPath, Log);
                MessageBox.Show(ok ? $"Backup saved: {backupPath}" : "Backup failed.");
            }
            catch (Exception ex)
            {
                Log("Backup error: " + ex.Message);
            }
        }

        private async void BtnGenerateEnv_Click(object? s, EventArgs e)
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
                await AppLogic.CreateEnvFromExampleAsync(txtInstallFolder.Text.Trim(), db, Log);
                MessageBox.Show(".env file generated successfully.");
            }
            catch (Exception ex)
            {
                Log(".env generation failed: " + ex.Message);
            }
        }

        private async void BtnComposerInstall_Click(object? s, EventArgs e)
        {
            try
            {
                await AppLogic.RunComposerInstallWithFallbackAsync(txtInstallFolder.Text.Trim(), Log);
                MessageBox.Show("Composer install/update done.");
            }
            catch (Exception ex)
            {
                Log("Composer error: " + ex.Message);
            }
        }

        private async void BtnMigrate_Click(object? s, EventArgs e)
        {
            try
            {
                await AppLogic.RunPhpArtisanMigrateAsync(txtInstallFolder.Text.Trim(), Log);
                MessageBox.Show("Migration done.");
            }
            catch (Exception ex)
            {
                Log("Migrate error: " + ex.Message);
            }
        }

        private async void BtnRegisterDevice_Click(object? s, EventArgs e)
        {
            try
            {
                bool ok = await AppLogic.ApplyProcessorIdToBackofficeControllerAsync(txtInstallFolder.Text.Trim(), Log);
                MessageBox.Show(ok ? "Device registered successfully." : "Device registration failed.");
            }
            catch (Exception ex)
            {
                Log("Device registration error: " + ex.Message);
            }
        }

        private async void BtnApplyApiUrl_Click(object? s, EventArgs e)
        {
            try
            {
                string apiUrl = txtApiUrl.Text.Trim();
                if (string.IsNullOrEmpty(apiUrl)) { MessageBox.Show("Enter API URL."); return; }

                var file = Path.Combine(txtInstallFolder.Text.Trim(), "app/Http/Controllers/Sales/OrderController.php");
                if (!File.Exists(file)) { MessageBox.Show("OrderController not found."); return; }

                var text = await File.ReadAllTextAsync(file, Encoding.UTF8);
                text = text.Replace("Http::post('#',", $"Http::post('{apiUrl}',");
                await File.WriteAllTextAsync(file, text, Encoding.UTF8);
                Log("API URL applied successfully.");
                await AppLogic.ToggleSkipWorktreeAsync(txtInstallFolder.Text.Trim(), "app/Http/Controllers/Sales/OrderController.php", Log);
                MessageBox.Show("API URL applied and file protected.");
            }
            catch (Exception ex)
            {
                Log("Apply API error: " + ex.Message);
            }
        }

        private async void BtnToggleSkipWorktree_Click(object? s, EventArgs e)
        {
            try
            {
                await AppLogic.ToggleSkipWorktreeAsync(txtInstallFolder.Text.Trim(), txtSkipPath.Text.Trim(), Log);
            }
            catch (Exception ex)
            {
                Log("Toggle skip-worktree error: " + ex.Message);
            }
        }

        private void BtnCancelOp_Click(object? s, EventArgs e)
        {
            lock (_procLock)
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    try
                    {
                        Log("Cancelling current process...");
                        _currentProcess.Kill(true);
                    }
                    catch (Exception ex)
                    {
                        Log("Cancel failed: " + ex.Message);
                    }
                }
            }
        }

        private void BtnGenerate_Click(object? s, EventArgs e)
        {
            try
            {
                using var rsa = System.Security.Cryptography.RSA.Create(4096);
                var priv = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
                _privatePem = priv;
                _publicSsh = "ssh-rsa " + Convert.ToBase64String(rsa.ExportRSAPublicKey());
                MessageBox.Show("Deploy key generated.");
            }
            catch (Exception ex)
            {
                Log("Key generation failed: " + ex.Message);
            }
        }

        private void BtnSaveKeys_Click(object? s, EventArgs e)
        {
            if (string.IsNullOrEmpty(_privatePem) || string.IsNullOrEmpty(_publicSsh))
            {
                MessageBox.Show("Generate keys first.");
                return;
            }

            var folder = txtInstallFolder.Text.Trim();
            if (string.IsNullOrEmpty(folder)) return;

            var sshDir = Path.Combine(folder, ".ssh");
            Directory.CreateDirectory(sshDir);
            File.WriteAllText(Path.Combine(sshDir, "deploy_key"), _privatePem);
            File.WriteAllText(Path.Combine(sshDir, "deploy_key.pub"), _publicSsh);
            _privateKeyPath = Path.Combine(sshDir, "deploy_key");
            Log("Keys saved to " + sshDir);
        }

        private void BtnCopy_Click(object? s, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_publicSsh))
            {
                Clipboard.SetText(_publicSsh);
                Log("Public key copied to clipboard.");
            }
        }

        private void BtnEnablePhpZip_Click(object? s, EventArgs e)
        {
            MessageBox.Show("This option edits php.ini. Use admin mode to modify.");
        }
    }
}
