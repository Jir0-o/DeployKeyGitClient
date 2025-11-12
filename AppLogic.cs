using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DeployKeyGitClient
{
    public static class AppLogic
    {
        public class DbInfo
        {
            public string Host = "127.0.0.1";
            public string Port = "3306";
            public string Database = "laravel";
            public string User = "root";
            public string Password = "";
        }

        // --- High-level operations used by UI ---

        public static async Task CloneIntoTempThenMoveAsync(string gitUrl, string targetFolder, Action<int>? progress, Action<string> log, string? privateKeyPath = null)
        {
            progress?.Invoke(0);
            var sshUrl = ConvertToSshIfHttps(gitUrl);
            var repoName = DeriveRepoName(gitUrl) ?? "repo";
            var tempRoot = Path.Combine(Path.GetTempPath(), "deploygit_clone_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var tempRepo = Path.Combine(tempRoot, repoName);

            try
            {
                log($"Cloning {sshUrl} -> {tempRepo}");
                var env = CreateGitSshEnv(privateKeyPath);
                var r = await RunProcessCaptureAsync("git", $"clone \"{sshUrl}\" \"{tempRepo}\"", env, null, log);
                log($"git clone exit {r.code}");
                if (r.code != 0) throw new Exception($"git clone failed: {r.stderr}");

                progress?.Invoke(60);

                // move or copy into target
                if (Directory.Exists(targetFolder) && Directory.EnumerateFileSystemEntries(targetFolder).Any())
                {
                    // ensure caller already confirmed deletion
                    TryDeleteDirectory(targetFolder, log);
                    Directory.CreateDirectory(targetFolder);
                }

                try
                {
                    Directory.Move(tempRepo, targetFolder);
                    log("Moved temp repo into target using Directory.Move.");
                }
                catch
                {
                    log("Directory.Move failed. Falling back to recursive copy.");
                    CopyDirectoryRecursive(tempRepo, targetFolder, log);
                }

                progress?.Invoke(100);
                log("Clone and move complete.");
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) TryDeleteDirectory(tempRoot, log); } catch { }
            }
        }

        public static async Task PullUpdateAsync(string targetFolder, Action<int>? progress, Action<string> log, string? privateKeyPath = null)
        {
            progress?.Invoke(0);
            var env = CreateGitSshEnv(privateKeyPath);

            var status = await RunProcessCaptureAsync("git", "status --porcelain", env, targetFolder, log);
            bool hasLocalChanges = !string.IsNullOrWhiteSpace(status.stdout);
            if (hasLocalChanges)
            {
                log("Local changes detected. Stashing.");
                var stash = await RunProcessCaptureAsync("git", "stash --include-untracked", env, targetFolder, log);
                log($"stash exit {stash.code}");
            }

            var fetch = await RunProcessCaptureAsync("git", "fetch --all --prune", env, targetFolder, log);
            log($"fetch exit {fetch.code}");
            progress?.Invoke(40);

            var merge = await RunProcessCaptureAsync("git", "merge --ff-only @{u}", env, targetFolder, log);
            if (merge.code != 0)
            {
                log("Fast-forward failed. Trying pull --rebase.");
                var pull = await RunProcessCaptureAsync("git", "pull --rebase", env, targetFolder, log);
                log($"pull exit {pull.code}");
                if (pull.code != 0) log("Pull failed or conflicts occurred. Manual resolution required.");
            }
            else
            {
                log("Fast-forward succeeded.");
            }

            if (hasLocalChanges)
            {
                var pop = await RunProcessCaptureAsync("git", "stash pop", env, targetFolder, log);
                log($"stash pop exit {pop.code}");
            }

            progress?.Invoke(100);
        }

        public static async Task<bool> ImportSqlFileAsync(string sqlFilePath, DbInfo db, Action<string> log)
        {
            log($"Importing SQL file {sqlFilePath} into {db.Host}:{db.Port} DB `{db.Database}`");

            var mysqlExe = Path.Combine("C:\\", "xampp", "mysql", "bin", "mysql.exe");
            if (!File.Exists(mysqlExe)) mysqlExe = "mysql";

            // Create DB if not exists
            var createArgs = $"-h {db.Host} -P {db.Port} -u {db.User} " + (string.IsNullOrEmpty(db.Password) ? "" : $"-p{db.Password} ");
            createArgs += $"-e \"CREATE DATABASE IF NOT EXISTS `{db.Database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;\"";
            var create = await RunProcessCaptureAsync(mysqlExe, createArgs, null, null, log);
            log($"create db exit {create.code}");
            if (create.code != 0)
            {
                log("Create DB failed: " + create.stderr);
                return false;
            }

            // Import by streaming
            var importArgs = $"-h {db.Host} -P {db.Port} -u {db.User} " + (string.IsNullOrEmpty(db.Password) ? "" : $"-p{db.Password} ") + db.Database;
            var sqlText = await File.ReadAllTextAsync(sqlFilePath, Encoding.UTF8);
            var res = await RunProcessWithStdinAsync(mysqlExe, importArgs, sqlText, null, log);
            log($"mysql import exit {res.code}");
            if (!string.IsNullOrWhiteSpace(res.stderr)) log("ERR: " + res.stderr);
            return res.code == 0;
        }

        public static async Task<bool> BackupDatabaseAsync(DbInfo db, string outputPath, Action<string> log)
        {
            log($"Backing up DB `{db.Database}` to {outputPath}");
            var mysqldump = Path.Combine("C:\\", "xampp", "mysql", "bin", "mysqldump.exe");
            if (!File.Exists(mysqldump)) mysqldump = "mysqldump";

            var args = $"-h {db.Host} -P {db.Port} -u {db.User} " + (string.IsNullOrEmpty(db.Password) ? "" : $"-p{db.Password} ") + $"{db.Database}";
            var r = await RunProcessCaptureToFileAsync(mysqldump, args, outputPath, null, log);
            log($"mysqldump exit {r}");
            return r == 0 && File.Exists(outputPath);
        }

        public static async Task<bool> CreateEnvFromExampleAsync(string projectRoot, DbInfo db, Action<string> log)
        {
            var envExample = Path.Combine(projectRoot, ".env.example");
            var envFile = Path.Combine(projectRoot, ".env");
            if (!File.Exists(envExample)) { log(".env.example not found."); return false; }

            var text = await File.ReadAllTextAsync(envExample, Encoding.UTF8);
            text = ReplaceOrAppendEnvKey(text, "DB_HOST", db.Host);
            text = ReplaceOrAppendEnvKey(text, "DB_PORT", db.Port);
            text = ReplaceOrAppendEnvKey(text, "DB_DATABASE", db.Database);
            text = ReplaceOrAppendEnvKey(text, "DB_USERNAME", db.User);
            text = ReplaceOrAppendEnvKey(text, "DB_PASSWORD", db.Password ?? "");
            await File.WriteAllTextAsync(envFile, text, Encoding.UTF8);
            log("Created/updated .env from .env.example");
            return true;
        }

        public static async Task RunComposerInstallWithFallbackAsync(string projectRoot, Action<string> log)
        {
            log("Composer: attempting install");
            var composer = "composer";
            var composerPhar = Path.Combine(projectRoot, "composer.phar");
            var xamppPhp = Path.Combine("C:\\", "xampp", "php", "php.exe");
            var usePhp = File.Exists(composerPhar);
            var phpExe = File.Exists(xamppPhp) ? xamppPhp : "php";

            (int code, string stdout, string stderr) r;
            if (usePhp)
            {
                r = await RunProcessCaptureAsync(phpExe, $"\"{composerPhar}\" install --no-interaction --prefer-dist", null, projectRoot, log);
            }
            else
            {
                r = await RunProcessCaptureAsync(composer, "install --no-interaction --prefer-dist", null, projectRoot, log);
            }

            log($"composer install exit {r.code}");
            if (r.code != 0 || (r.stderr ?? "").Contains("Your lock file does not contain a compatible set of packages", StringComparison.OrdinalIgnoreCase))
            {
                log("Composer install failed - running composer update");
                if (usePhp)
                    r = await RunProcessCaptureAsync(phpExe, $"\"{composerPhar}\" update --no-interaction --prefer-dist", null, projectRoot, log);
                else
                    r = await RunProcessCaptureAsync(composer, "update --no-interaction --prefer-dist", null, projectRoot, log);

                log($"composer update exit {r.code}");
            }
        }

        public static async Task RunPhpArtisanMigrateAsync(string projectRoot, Action<string> log)
        {
            var xamppPhp = Path.Combine("C:\\", "xampp", "php", "php.exe");
            var phpExe = File.Exists(xamppPhp) ? xamppPhp : "php";
            var artisan = Path.Combine(projectRoot, "artisan");
            if (!File.Exists(artisan)) { log("artisan not found"); return; }
            var r = await RunProcessCaptureAsync(phpExe, $"\"{artisan}\" migrate --force", null, projectRoot, log);
            log($"migrate exit {r.code}");
        }

        public static async Task ToggleSkipWorktreeAsync(string projectRoot, string relPath, Action<string> log)
        {
            await ToggleSkipWorktreeInternal(projectRoot, relPath, null, log);
        }

        // Replace or set skip-worktree; used both by UI and AppLogic earlier
        private static async Task ToggleSkipWorktreeInternal(string projectRoot, string relPath, bool? setSkip, Action<string> log)
        {
            var path = relPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var check = await RunProcessCaptureAsync("git", $"ls-files -v \"{path}\"", null, projectRoot, log);
            var isSkipped = (check.stdout ?? "").Split('\n').Any(l => l.Length > 0 && l[0] == 'S');

            if (setSkip == true)
            {
                var r = await RunProcessCaptureAsync("git", $"update-index --skip-worktree \"{path}\"", null, projectRoot, log);
                log($"git update-index --skip-worktree exit {r.code}");
            }
            else if (setSkip == false)
            {
                var r = await RunProcessCaptureAsync("git", $"update-index --no-skip-worktree \"{path}\"", null, projectRoot, log);
                log($"git update-index --no-skip-worktree exit {r.code}");
            }
            else
            {
                if (isSkipped)
                {
                    var r = await RunProcessCaptureAsync("git", $"update-index --no-skip-worktree \"{path}\"", null, projectRoot, log);
                    log($"Unmarked skip-worktree for {path} exit {r.code}");
                }
                else
                {
                    var r = await RunProcessCaptureAsync("git", $"update-index --skip-worktree \"{path}\"", null, projectRoot, log);
                    log($"Marked skip-worktree for {path} exit {r.code}");
                }
            }
        }

        // Apply ProcessorId to BackofficeLoginController and mark skip-worktree
        public static async Task<bool> ApplyProcessorIdToBackofficeControllerAsync(string projectRoot, Action<string> log)
        {
            // get ProcessorId
            var ps = await RunProcessCaptureAsync("powershell", "-NoProfile -Command \"Get-CimInstance Win32_Processor | Select-Object -ExpandProperty ProcessorId\"", null, null, log);
            var processorId = (ps.stdout ?? "").Trim();
            if (string.IsNullOrWhiteSpace(processorId)) { log("ProcessorId not found"); return false; }

            var rel = Path.Combine("app", "Http", "Controllers", "BackofficeLoginController.php");
            var file = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file)) { log("BackofficeLoginController not found: " + file); return false; }

            var bak = file + ".deploybak." + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Copy(file, bak, true);
            log("Backup created: " + bak);

            var text = await File.ReadAllTextAsync(file, Encoding.UTF8);

            // try replace pattern of $processorId !== '...'
            var pattern = @"\$processorId\s*!==\s*['""]([0-9A-Fa-f]+)['""]";
            if (Regex.IsMatch(text, pattern))
            {
                var updated = Regex.Replace(text, pattern, $"$processorId !== '{processorId}'");
                await File.WriteAllTextAsync(file, updated, Encoding.UTF8);
                log("Replaced existing hard-coded ProcessorId.");
            }
            else if (text.Contains("BFEBFBFF000306C3"))
            {
                var updated = text.Replace("BFEBFBFF000306C3", processorId);
                await File.WriteAllTextAsync(file, updated, Encoding.UTF8);
                log("Replaced BFEB... pattern with current processorId.");
            }
            else
            {
                log("No matching pattern found to replace in controller.");
                return false;
            }

            // protect file
            await ToggleSkipWorktreeInternal(projectRoot, rel, true, log);
            return true;
        }

        // --- Utilities and process helpers ---

        private static string ConvertToSshIfHttps(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url!;
            if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase) || url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)) return url;
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
                var cleaned = url.Trim().TrimEnd('/');
                var parts = cleaned.Split(new[] { '/', ':' }, StringSplitOptions.RemoveEmptyEntries);
                var last = parts.LastOrDefault();
                if (string.IsNullOrEmpty(last)) return null;
                if (last.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) last = last.Substring(0, last.Length - 4);
                foreach (var c in Path.GetInvalidFileNameChars()) last = last.Replace(c, '_');
                return last;
            }
            catch { return null; }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir, Action<string> log)
        {
            var src = new DirectoryInfo(sourceDir);
            var dst = new DirectoryInfo(targetDir);
            if (!dst.Exists) dst.Create();

            foreach (var file in src.GetFiles())
            {
                var targetFilePath = Path.Combine(dst.FullName, file.Name);
                file.CopyTo(targetFilePath, true);
            }
            foreach (var dir in src.GetDirectories())
            {
                CopyDirectoryRecursive(dir.FullName, Path.Combine(dst.FullName, dir.Name), log);
            }
        }

        private static void TryDeleteDirectory(string path, Action<string> log)
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    return;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Delete attempt {attempt} failed: {ex.Message}");
                    System.Threading.Thread.Sleep(200 * attempt);
                }
            }
            throw new Exception($"Unable to delete directory '{path}'. Close open handles and try again.");
        }

        // Create GIT_SSH_COMMAND env override (same as earlier MainForm's helper)
        public static (string, string)[] CreateGitSshEnv(string? privateKeyPath)
        {
            try
            {
                if (string.IsNullOrEmpty(privateKeyPath)) return Array.Empty<(string, string)>();
                var knownHosts = Path.Combine(Path.GetTempPath(), $"ssh_known_hosts_{Guid.NewGuid():N}.txt");
                File.WriteAllText(knownHosts, "");
                var gitSsh = $"ssh -i \"{privateKeyPath}\" -o IdentitiesOnly=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=\"{knownHosts}\"";
                return new[] { ("GIT_SSH_COMMAND", gitSsh) };
            }
            catch
            {
                if (string.IsNullOrEmpty(privateKeyPath)) return Array.Empty<(string, string)>();
                var gitSsh = $"ssh -i \"{privateKeyPath}\" -o IdentitiesOnly=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL";
                return new[] { ("GIT_SSH_COMMAND", gitSsh) };
            }
        }

        // Process helpers - robust single-complete result and stdout/stderr capture
        private static async Task<(int code, string stdout, string stderr)> RunProcessCaptureAsync(string file, string args, (string, string)[]? env, string? workingDir, Action<string> log)
        {
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

            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) { sbOut.AppendLine(e.Data); log?.Invoke(e.Data); } };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) { sbErr.AppendLine(e.Data); log?.Invoke("ERR: " + e.Data); } };

            try
            {
                if (!proc.Start()) throw new Exception("Failed to start: " + file);
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync();
                return (proc.ExitCode, sbOut.ToString(), sbErr.ToString());
            }
            catch (Exception ex)
            {
                log?.Invoke("RunProcessCaptureAsync error: " + ex.Message);
                return (-1, sbOut.ToString(), sbErr.ToString() + Environment.NewLine + ex.Message);
            }
        }

        private static async Task<int> RunProcessCaptureToFileAsync(string file, string args, string outputFile, (string, string)[]? env, Action<string> log)
        {
            try
            {
                var r = await RunProcessCaptureAsync(file, args, env, null, log);
                if (r.code == 0 && !string.IsNullOrEmpty(r.stdout))
                {
                    // If mysqldump wrote to stdout then write to file
                    await File.WriteAllTextAsync(outputFile, r.stdout, Encoding.UTF8);
                }
                return r.code;
            }
            catch (Exception ex)
            {
                log("RunProcessCaptureToFileAsync error: " + ex.Message);
                return -1;
            }
        }

        private static async Task<(int code, string stdout, string stderr)> RunProcessWithStdinAsync(string file, string args, string stdinText, string? workingDir, Action<string> log)
        {
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

            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) { sbOut.AppendLine(e.Data); log?.Invoke(e.Data); } };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) { sbErr.AppendLine(e.Data); log?.Invoke("ERR: " + e.Data); } };

            try
            {
                if (!proc.Start()) throw new Exception("Failed to start: " + file);
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                if (!string.IsNullOrEmpty(stdinText))
                {
                    await proc.StandardInput.WriteAsync(stdinText);
                }
                proc.StandardInput.Close();
                await proc.WaitForExitAsync();
                return (proc.ExitCode, sbOut.ToString(), sbErr.ToString());
            }
            catch (Exception ex)
            {
                log?.Invoke("RunProcessWithStdinAsync error: " + ex.Message);
                return (-1, sbOut.ToString(), sbErr.ToString() + Environment.NewLine + ex.Message);
            }
        }

        // simple env key replace helper used earlier
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
    }
}
