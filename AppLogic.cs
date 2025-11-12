using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        // Directory under project root where protected function bodies are stored
        private const string ProtectedStoreFolderName = ".deploy_protected";

        // Expose method for UI to kill current child process
        private static readonly object _procLock = new object();
        private static Process? _currentProcess;
        public static void KillCurrentProcess()
        {
            lock (_procLock)
            {
                try { if (_currentProcess != null && !_currentProcess.HasExited) _currentProcess.Kill(true); } catch { }
            }
        }

        // Helper to save public/private key text to a folder (used by MainForm Save)
        public static void SaveKeysToFolder(string keyFolder, string publicKeyText, string? privatePem = null)
        {
            if (string.IsNullOrEmpty(keyFolder)) throw new ArgumentException("keyFolder required");
            var sshDir = Path.Combine(keyFolder, ".ssh");
            Directory.CreateDirectory(sshDir);
            File.WriteAllText(Path.Combine(sshDir, "deploy_key.pub"), publicKeyText ?? "");
            if (!string.IsNullOrEmpty(privatePem))
            {
                File.WriteAllText(Path.Combine(sshDir, "deploy_key"), privatePem);
            }
        }

        // Helper to load public key from a folder (returns null if not found)
        public static string? LoadPublicKeyFromFolder(string keyFolder)
        {
            if (string.IsNullOrEmpty(keyFolder)) return null;
            var candidates = new[] {
                Path.Combine(keyFolder, ".ssh", "deploy_key.pub"),
                Path.Combine(keyFolder, "deploy_key.pub")
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return File.ReadAllText(c, Encoding.UTF8);
            }
            return null;
        }

        // --- High-level operations used by UI (clone/pull/sql etc) ---
        // (Existing methods kept; unchanged except process helpers have shared _currentProcess)

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
                if (r.code != 0)
                {
                    log("git clone failed: " + r.stderr);
                    throw new Exception("git clone failed: " + r.stderr);
                }

                progress?.Invoke(60);

                // move or copy into target
                if (Directory.Exists(targetFolder) && Directory.EnumerateFileSystemEntries(targetFolder).Any())
                {
                    TryDeleteDirectory(targetFolder, log);
                    Directory.CreateDirectory(targetFolder);
                }

                try
                {
                    Directory.Move(tempRepo, targetFolder);
                    log("Moved temp repo into target using Directory.Move.");
                }
                catch (Exception ex)
                {
                    log("Directory.Move failed. Falling back to recursive copy. " + ex.Message);
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

            // After pull, reapply protected functions if any present
            await ReapplyProtectedFunctionsAsync(targetFolder, log);
        }

        // SQL import / backup / env creation - unchanged from previous, included for completeness
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

        // internal helper used by ToggleSkipWorktreeAsync and other callers
        private static async Task ToggleSkipWorktreeInternal(string projectRoot, string relPath, bool? setSkip, Action<string> log)
        {
            try
            {
                var path = relPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                // Check current skip-worktree status
                var check = await RunProcessCaptureAsync("git", $"ls-files -v \"{path}\"", null, projectRoot, log);
                var isSkipped = (check.stdout ?? "").Split('\n').Any(l => l.Length > 0 && l[0] == 'S');

                if (setSkip == true)
                {
                    var r = await RunProcessCaptureAsync("git", $"update-index --skip-worktree \"{path}\"", null, projectRoot, log);
                    log?.Invoke($"git update-index --skip-worktree exit {r.code}");
                }
                else if (setSkip == false)
                {
                    var r = await RunProcessCaptureAsync("git", $"update-index --no-skip-worktree \"{path}\"", null, projectRoot, log);
                    log?.Invoke($"git update-index --no-skip-worktree exit {r.code}");
                }
                else
                {
                    if (isSkipped)
                    {
                        var r = await RunProcessCaptureAsync("git", $"update-index --no-skip-worktree \"{path}\"", null, projectRoot, log);
                        log?.Invoke($"Unmarked skip-worktree for {path} (exit {r.code})");
                    }
                    else
                    {
                        var r = await RunProcessCaptureAsync("git", $"update-index --skip-worktree \"{path}\"", null, projectRoot, log);
                        log?.Invoke($"Marked skip-worktree for {path} (exit {r.code})");
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("ToggleSkipWorktreeInternal error: " + ex.Message);
            }
        }


        // ---------------- Function-protect workflow ----------------

        /// <summary>
        /// Save a function body from a PHP controller into the protected store.
        /// The stored file will be under: {projectRoot}/.deploy_protected/<controller-file-name>__<function>.php
        /// </summary>
        public static async Task<bool> ProtectFunctionAsync(string projectRoot, string controllerRelPath, string functionName, Action<string> log)
        {
            try
            {
                var controllerFile = Path.Combine(projectRoot, controllerRelPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(controllerFile)) { log("Controller file not found: " + controllerFile); return false; }

                var text = await File.ReadAllTextAsync(controllerFile, Encoding.UTF8);

                // find function definition (basic PHP function inside class: public function check(Request $request) { ... })
                // This is a best-effort regex. It handles 'public/private/protected function fname(...) { ... }'
                var pattern = $@"(public|protected|private)\s+function\s+{Regex.Escape(functionName)}\s*\([^\)]*\)\s*\{{";
                var m = Regex.Match(text, pattern);
                if (!m.Success) { log($"Function '{functionName}' not found with expected signature."); return false; }

                var startIndex = m.Index + m.Length - 1; // index of '{'
                // Find matching closing brace for that function body
                int braceLevel = 1;
                int i = startIndex + 1;
                while (i < text.Length && braceLevel > 0)
                {
                    if (text[i] == '{') braceLevel++;
                    else if (text[i] == '}') braceLevel--;
                    i++;
                }
                if (braceLevel != 0) { log("Failed to parse function body - unmatched braces."); return false; }

                var body = text.Substring(startIndex + 1, i - startIndex - 2); // inner content

                // store body to protected folder
                var storeDir = Path.Combine(projectRoot, ProtectedStoreFolderName);
                Directory.CreateDirectory(storeDir);
                var safeControllerName = Path.GetFileName(controllerFile).Replace('.', '_');
                var storeFileName = $"{safeControllerName}__{functionName}.body.php";
                var storePath = Path.Combine(storeDir, storeFileName);
                await File.WriteAllTextAsync(storePath, body, Encoding.UTF8);
                log("Saved function body to " + storePath);

                // Also write a small metadata file listing mapping (optional)
                var metaPath = Path.Combine(storeDir, "index.txt");
                var metaLine = $"{controllerRelPath.Replace('\\','/')}|{functionName}|{storeFileName}";
                File.AppendAllText(metaPath, metaLine + Environment.NewLine, Encoding.UTF8);

                return true;
            }
            catch (Exception ex)
            {
                log("ProtectFunctionAsync error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Reapply protected function bodies stored in .deploy_protected to their controller files.
        /// This will search for matching controller and function and replace the function body with stored body.
        /// </summary>
        public static async Task<bool> ReapplyProtectedFunctionsAsync(string projectRoot, Action<string> log)
        {
            try
            {
                var storeDir = Path.Combine(projectRoot, ProtectedStoreFolderName);
                if (!Directory.Exists(storeDir)) { log("No protected store found."); return false; }

                var indexPath = Path.Combine(storeDir, "index.txt");
                IEnumerable<string> lines;
                if (File.Exists(indexPath))
                    lines = File.ReadAllLines(indexPath, Encoding.UTF8).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l));
                else
                {
                    // fallback: find files matching pattern
                    lines = Directory.GetFiles(storeDir, "*.body.php").Select(f =>
                    {
                        var fname = Path.GetFileName(f); // <controller>__<fn>.body.php
                        var parts = fname.Split(new[] { "__" }, StringSplitOptions.None);
                        if (parts.Length >= 2)
                        {
                            var ctrlFile = parts[0].Replace('_', '.'); // approximate
                            var fn = parts[1].Replace(".body.php", "");
                            return $"{ctrlFile}|{fn}|{fname}";
                        }
                        return null;
                    }).Where(s => s != null).Cast<string>();
                }

                var reappliedCount = 0;
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length < 3) continue;
                    var rel = parts[0];
                    var fn = parts[1];
                    var storeFile = parts[2];
                    var storePath = Path.Combine(storeDir, storeFile);
                    if (!File.Exists(storePath)) { log($"Stored body not found: {storePath}"); continue; }

                    var controllerFile = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(controllerFile)) { log($"Controller file not found: {controllerFile}, skipping."); continue; }

                    var text = await File.ReadAllTextAsync(controllerFile, Encoding.UTF8);
                    var pattern = $@"(public|protected|private)\s+function\s+{Regex.Escape(fn)}\s*\([^\)]*\)\s*\{{";
                    var m = Regex.Match(text, pattern);
                    if (!m.Success) { log($"Function '{fn}' not found in {rel}."); continue; }

                    var startIndex = m.Index + m.Length - 1;
                    int braceLevel = 1;
                    int i = startIndex + 1;
                    while (i < text.Length && braceLevel > 0)
                    {
                        if (text[i] == '{') braceLevel++;
                        else if (text[i] == '}') braceLevel--;
                        i++;
                    }
                    if (braceLevel != 0) { log($"Failed to parse function body for {fn} in {rel}."); continue; }

                    var newBody = await File.ReadAllTextAsync(storePath, Encoding.UTF8);
                    // Surround newBody with exact same indentation as original open brace line
                    var before = text.Substring(0, startIndex + 1);
                    var after = text.Substring(i - 1); // starts with closing brace
                    var updated = before + Environment.NewLine + newBody + Environment.NewLine + after;
                    await File.WriteAllTextAsync(controllerFile, updated, Encoding.UTF8);
                    log($"Reapplied protected function '{fn}' into {rel}");
                    reappliedCount++;
                }

                return reappliedCount > 0;
            }
            catch (Exception ex)
            {
                log("ReapplyProtectedFunctionsAsync error: " + ex.Message);
                return false;
            }
        }

        // For convenience: a helper that applies ProcessorId into Backoffice controller (existing)
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

            // protect file (file-level)
            await ToggleSkipWorktreeInternal(projectRoot, rel, true, log);
            return true;
        }

        // --- Utilities and process helpers (same as before but shared _currentProcess) ---

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

        // Process helpers: set shared _currentProcess so UI can kill
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

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) { sbOut.AppendLine(e.Data); log?.Invoke(e.Data); } };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) { sbErr.AppendLine(e.Data); log?.Invoke("ERR: " + e.Data); } };

            try
            {
                lock (_procLock) { _currentProcess = proc; }
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
            finally
            {
                try { lock (_procLock) { proc?.Dispose(); _currentProcess = null; } } catch { }
            }
        }

        private static async Task<int> RunProcessCaptureToFileAsync(string file, string args, string outputFile, (string, string)[]? env, Action<string> log)
        {
            try
            {
                var r = await RunProcessCaptureAsync(file, args, env, null, log);
                if (r.code == 0 && !string.IsNullOrEmpty(r.stdout))
                {
                    // If program wrote dump to stdout (mysqldump), write to file
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

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) { sbOut.AppendLine(e.Data); log?.Invoke(e.Data); } };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) { sbErr.AppendLine(e.Data); log?.Invoke("ERR: " + e.Data); } };

            try
            {
                lock (_procLock) { _currentProcess = proc; }
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
            finally
            {
                try { lock (_procLock) { proc?.Dispose(); _currentProcess = null; } } catch { }
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
