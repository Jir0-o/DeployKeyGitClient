using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeployKeyGitClient; // to use SettingsManager & AppLogic

namespace DeployKeyGitClientAgent
{
    public class AgentForm : Form
    {
        private System.Windows.Forms.Timer _timer;
        private bool _isPullRunning = false;

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;

        // Cached settings so we don't read file every tick
        private string _repoFolder = "";
        private string _keyFolder = "";
        private string? _privateKeyPath = null;
        private string? _branchName = null;

        public AgentForm()
        {
            this.Load += AgentForm_Load;
            this.Shown += AgentForm_Shown;
            this.FormClosing += AgentForm_FormClosing;
        }

        private void AgentForm_Load(object? sender, EventArgs e)
        {
            // Hide window
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;

            // Load settings once at startup
            LoadSettings();

            // Tray icon so user knows it's running
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Pull now", null, async (s, ea) => await RunPullAsync());
            _trayMenu.Items.Add("Open main app (admin)", null, (s, ea) => LaunchMainAsAdmin());
            _trayMenu.Items.Add("Exit agent", null, (s, ea) => Application.Exit());

            _trayIcon = new NotifyIcon
            {
                Visible = true,
                Text = "DeployKeyGitClient Agent",
                Icon = System.Drawing.SystemIcons.Application, // replace with your .ico if you want
                ContextMenuStrip = _trayMenu
            };

            _trayIcon.DoubleClick += async (s, ea) => await RunPullAsync();

            // Timer: 1 hour
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 60 * 60 * 1000; // 1 hour
            // For testing, you can temporarily set: _timer.Interval = 5 * 60 * 1000;
            _timer.Tick += async (s, ea) => await RunPullAsync();
            _timer.Start();

            LogToFile("Agent started. Timer interval = " + (_timer.Interval / 1000) + " seconds.");
        }

        private void AgentForm_Shown(object? sender, EventArgs e)
        {
            // First pull shortly after startup (fire and forget)
            _ = RunPullAsync();
        }

        private void AgentForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settings = SettingsManager.Load();

                settings.TryGetValue("InstallFolder", out var installFolder);
                settings.TryGetValue("KeyFolder", out var keyFolderVal);
                settings.TryGetValue("GitBranch", out var branchVal);

                _repoFolder = (installFolder ?? "").Trim();
                _keyFolder  = (keyFolderVal ?? "").Trim();

                _branchName = string.IsNullOrWhiteSpace(branchVal)
                    ? null
                    : branchVal.Trim();

                if (!string.IsNullOrWhiteSpace(_keyFolder))
                {
                    var candidate = Path.Combine(_keyFolder, ".ssh", "deploy_key");
                    if (File.Exists(candidate))
                    {
                        _privateKeyPath = candidate;
                    }
                    else
                    {
                        _privateKeyPath = null;
                    }
                }
                else
                {
                    _privateKeyPath = null;
                }

                LogToFile(
                    $"Agent: Settings loaded. RepoFolder='{_repoFolder}', " +
                    $"KeyFolder='{_keyFolder}', Branch='{_branchName ?? "(default upstream)"}', " +
                    $"PrivateKeyPath='{_privateKeyPath ?? "(none)"}'"
                );
            }
            catch (Exception ex)
            {
                LogToFile("Agent: LoadSettings error: " + ex.Message);
            }
        }

        private async Task RunPullAsync()
        {
            if (_isPullRunning) return;
            _isPullRunning = true;

            try
            {
                // Re-load settings in case user changed things in main app
                LoadSettings();

                if (string.IsNullOrWhiteSpace(_repoFolder) || !Directory.Exists(_repoFolder))
                {
                    LogToFile("Agent: Repo folder not set or does not exist. Skipping pull.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_privateKeyPath) || !File.Exists(_privateKeyPath))
                {
                    LogToFile("Agent: Private key not found. Expected: " +
                              (string.IsNullOrEmpty(_keyFolder)
                                  ? "(KeyFolder not set)"
                                  : Path.Combine(_keyFolder, ".ssh", "deploy_key")));
                    return;
                }

                LogToFile(
                    $"Agent: Starting git pull. Repo='{_repoFolder}', " +
                    $"Branch='{_branchName ?? "(default upstream)"}', " +
                    $"Key='{_privateKeyPath}'"
                );

                await AppLogic.PullUpdateAsync(
                    _repoFolder,
                    v => { /* no progress bar in agent */ },
                    LogToFile,
                    _privateKeyPath,
                    _branchName
                );

                LogToFile("Agent: Auto-pull finished.");
            }
            catch (Exception ex)
            {
                LogToFile("Agent: Error in RunPullAsync: " + ex.Message);
            }
            finally
            {
                _isPullRunning = false;
            }
        }

        private void LaunchMainAsAdmin()
        {
            try
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var mainExe = Path.Combine(exeDir, "DeployKeyGitClient.exe");
                if (!File.Exists(mainExe))
                {
                    LogToFile("Agent: main exe not found at " + mainExe);
                    MessageBox.Show("Main application not found.", "DeployKeyGitClient Agent");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = mainExe,
                    UseShellExecute = true,
                    Verb = "runas" // UAC prompt
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                LogToFile("Agent: Failed to launch main app as admin: " + ex.Message);
                MessageBox.Show("Failed to launch main app as admin:\n" + ex.Message, "DeployKeyGitClient Agent");
            }
        }

        private void LogToFile(string line)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir = Path.Combine(appData, "DeployKeyGitClient");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "agent-log.txt");
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}";
                File.AppendAllText(logPath, text);
            }
            catch
            {
                // ignore logging failures
            }
        }
    }
}
