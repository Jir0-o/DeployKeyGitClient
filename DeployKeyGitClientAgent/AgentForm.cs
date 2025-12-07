using System;
using System.Collections.Generic;
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

        public AgentForm()
        {
            // We don't need visible UI
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

            // Tray icon so user knows it's running
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Open main app (admin)", null, (s, ea) => LaunchMainAsAdmin());
            _trayMenu.Items.Add("Exit agent", null, (s, ea) => Application.Exit());

            _trayIcon = new NotifyIcon
            {
                Visible = true,
                Text = "DeployKeyGitClient Agent",
                Icon = System.Drawing.SystemIcons.Application, // Replace with your .ico if you want
                ContextMenuStrip = _trayMenu
            };

            _trayIcon.DoubleClick += (s, ea) => LaunchMainAsAdmin();

            // Timer: 1 hour
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 60 * 60 * 1000; // 1 hour
            _timer.Tick += async (s, ea) => await RunPullAsync();
            _timer.Start();
        }

        private async void AgentForm_Shown(object? sender, EventArgs e)
        {
            // First pull shortly after startup
            await RunPullAsync();
        }

        private void AgentForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }

        private async Task RunPullAsync()
        {
            if (_isPullRunning) return;
            _isPullRunning = true;

            try
            {
                var settings = SettingsManager.Load();
                if (!settings.TryGetValue("InstallFolder", out var installFolder) ||
                    string.IsNullOrWhiteSpace(installFolder) ||
                    !Directory.Exists(installFolder))
                {
                    LogToFile("Agent: InstallFolder not set or not found, skipping pull.");
                    return;
                }

                string keyFolder = "";
                if (settings.TryGetValue("KeyFolder", out var keyFolderVal))
                    keyFolder = keyFolderVal ?? "";

                string? privateKeyPath = null;
                if (!string.IsNullOrWhiteSpace(keyFolder))
                {
                    var candidate = Path.Combine(keyFolder, ".ssh", "deploy_key");
                    if (File.Exists(candidate))
                        privateKeyPath = candidate;
                }

                LogToFile($"Agent: Starting auto-pull for '{installFolder}'.");

                // Use AppLogic.PullUpdateAsync; no UI progress
                await AppLogic.PullUpdateAsync(
                    installFolder,
                    v => { /* no progress bar in agent */ },
                    LogToFile,
                    privateKeyPath
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

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mainExe,
                    UseShellExecute = true,
                    Verb = "runas" // UAC prompt
                };
                System.Diagnostics.Process.Start(psi);
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
