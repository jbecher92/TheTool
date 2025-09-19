using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace TheTool
{
    public static class FileManager
    {
        //retain these files when cleaning out the target directory
        private static readonly string[] PreserveFiles = { "web.config", "appsettings.json" };

        // NEW: optional override to redirect production base path resolution (used for non-prod wrappers)
        // Signature: (state, client) => basePath (without tag)
        // Default: null (production behavior unchanged)
        public static Func<string, string, string>? ProdBasePathOverride { get; set; }

        public static void DeployProduction_Update(
            string state,
            string client,
            string prevFolder,
            string newFolder,
            string newBuildArchivePath,
            Action<string>? onProgress = null)
        {
            void Report(string m) { try { onProgress?.Invoke(m); } catch { } }

            if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("state required");
            if (string.IsNullOrWhiteSpace(client)) throw new ArgumentException("client required");
            if (string.IsNullOrWhiteSpace(newFolder)) throw new ArgumentException("newFolder required");
            if (string.IsNullOrWhiteSpace(newBuildArchivePath) || !File.Exists(newBuildArchivePath))
                throw new FileNotFoundException("New build archive not found", newBuildArchivePath);

            // Allow .zip and .7z
            var ext = Path.GetExtension(newBuildArchivePath).ToLowerInvariant();
            if (ext != ".zip" && ext != ".7z")
                throw new NotSupportedException("Only .zip or .7z extraction is supported at this time.");

            var basePath = ResolveProdBasePath(state, client);
            var prevPath = Path.Combine(basePath, prevFolder);
            var newPath = Path.Combine(basePath, newFolder);

            EnsureDirExists(basePath);

            // Always create the new dated folder (re-use if exists – supports same-day updates)
            CreateSiteDirectory(newPath, Report);

            bool creationPath = string.IsNullOrWhiteSpace(prevFolder) || !Directory.Exists(prevPath);
            bool deferredPreserveFromBuild = false;

            if (!creationPath)
            {
                // 1) Backup previous folder
                var backupZip = MakeProdBackupZipPath(basePath, state, client, prevFolder);
                CreateZipArchive(prevPath, backupZip);

                // 2) Immediately rename previous folder to temp_yyyyMMdd_HHmmss and update prevPath
                var tempPath = MakeUniqueTempFolder(basePath, "temp_" + DateTime.Now.ToString("MMddyyyy_HHmmss"));
                Directory.Move(prevPath, tempPath);
                prevPath = tempPath; // from here on, operate against the temp folder

                // 3) Clean temp (old) folder, keeping preserved files
                DeleteAllInsideExcept(prevPath, ProdPreserveRelative);

                // 4) Copy preserved files from temp into the new folder (safe if same-day; self-copy guarded)
                CopyPreservedFiles(prevPath, newPath, ProdPreserveRelative);
            }
            else
            {
                // No previous dated folder — try preserved from base; if missing, defer to post-extract
                try
                {
                    ForceCopyPreservedFilesFromBase(basePath, newPath);
                }
                catch (FileNotFoundException)
                {
                    deferredPreserveFromBuild = true;
                }
            }

            // 5) Copy archive into new folder and extract (no overwrite), then flatten PBK/DBK wrapper
            var dstZip = Path.Combine(newPath, Path.GetFileName(newBuildArchivePath));
            File.Copy(newBuildArchivePath, dstZip, overwrite: true);
            ExtractArchiveSkipOverwrite(dstZip, newPath);
            MergeFlattenTopFolderNoOverwrite(newPath);

            // 6) Final preserved rules/validation
            if (!creationPath)
            {
                // Ensure preserved files from temp are the final ones (self-copy guarded)
                ForceCopyPreservedFiles(prevPath, newPath);
            }
            else
            {
                if (deferredPreserveFromBuild)
                    EnsurePreservedFromBuildOrFail(newPath);
                else
                    ForceCopyPreservedFilesFromBase(basePath, newPath);
            }

            // 7) Cleanup temp (old) folder and deployed zip
            if (!creationPath)
                TryDeleteDirRecursive(prevPath); // prevPath now points to temp_*

            try { File.Delete(dstZip); } catch { /* ignore */ }

            // 8) Keep only the most recent backup zip
            CleanupBackupsKeepMostRecent(basePath, state, client);
        }

        // -------- External controller --------
        public static void DeployExternal_Update(
            string state,
            string client,
            string externalRoot,
            string backupTag,
            string? caseInfoSearchZip,
            string? esubpoenaZip,
            string? dataAccessZip,
            Action<string>? onProgress = null)
        {
            void Report(string msg)
            {
                try { onProgress?.Invoke(msg); } catch { /* ignore UI errors */ }
            }

            if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("state required");
            if (string.IsNullOrWhiteSpace(client)) throw new ArgumentException("client required");
            if (string.IsNullOrWhiteSpace(externalRoot)) throw new ArgumentException("externalRoot required");
            if (!Directory.Exists(externalRoot))
                throw new DirectoryNotFoundException($"External root not found: {externalRoot}");

            // Validate names if provided
            if (!string.IsNullOrWhiteSpace(caseInfoSearchZip)) ValidateBuildName(caseInfoSearchZip, "caseinfosearch");
            if (!string.IsNullOrWhiteSpace(esubpoenaZip)) ValidateBuildName(esubpoenaZip, "esubpoena");
            if (!string.IsNullOrWhiteSpace(dataAccessZip)) ValidateBuildName(dataAccessZip, "dataaccess");

            // Decide DataAccess folder name (prefer PBKDataAccess if present)
            string daFolder = Directory.Exists(Path.Combine(externalRoot, "PBKDataAccess")) ? "PBKDataAccess" : "DataAccess";

            // Canonical target paths (no extra layer)
            string cisPath = Path.Combine(externalRoot, "CaseInfoSearch");
            string esPath = Path.Combine(externalRoot, "eSubpoena");
            string daPath = Path.Combine(externalRoot, daFolder);

            // Create a *tag* folder directly under the client external root for backups, e.g. ...\KSTest1\20250918\
            string backupDir = GetExternalBackupDir(externalRoot, backupTag);

            // --- Per-app BACKUP (only for roles we’re updating) ---
            if (!string.IsNullOrWhiteSpace(caseInfoSearchZip))
            {
                TryBackupFolder(cisPath, Path.Combine(backupDir, "CaseInfoSearch.zip"), Report);
            }
            if (!string.IsNullOrWhiteSpace(esubpoenaZip))
            {
                TryBackupFolder(esPath, Path.Combine(backupDir, "eSubpoena.zip"), Report);
            }
            if (!string.IsNullOrWhiteSpace(dataAccessZip))
            {
                TryBackupFolder(daPath, Path.Combine(backupDir, daFolder + ".zip"), Report);
            }

            // --- Deploy each role provided (overwrite-in-place, preserve exclusions) ---
            DeployRoleIfProvided("CaseInfoSearch", caseInfoSearchZip, cisPath);
            DeployRoleIfProvided("eSubpoena", esubpoenaZip, esPath);
            DeployRoleIfProvided(daFolder, dataAccessZip, daPath); // archive name & folder match (DataAccess or PBKDataAccess)

            // Keep ONLY the most recent backup directory (purge older tag folders)
            KeepOnlyMostRecentBackup(externalRoot, backupTag, Report);

            Report($"External update for {state}{client} completed.");

            // ------------- local helpers ------------- 

            void DeployRoleIfProvided(string roleDisplayName, string? archivePath, string rolePath)
            {
                if (string.IsNullOrWhiteSpace(archivePath))
                    return; // skip if not part of this deployment

                var ext = Path.GetExtension(archivePath).ToLowerInvariant();
                if (ext != ".zip" && ext != ".7z")
                {
                    Report($"{roleDisplayName}: skipping — archive must be .zip or .7z");
                    return;
                }

                Directory.CreateDirectory(rolePath);

                // Clear contents but keep preserved items (configs, etc.)
                DeleteAllInsideExcept(rolePath, ExternalPreserveRelative);

                // Copy the archive beside the extraction target (your existing pattern)
                var dstArchive = Path.Combine(rolePath, Path.GetFileName(archivePath));
                File.Copy(archivePath, dstArchive, overwrite: true);

                // Extract (zip natively, 7z via helper) with skip-overwrite semantics
                ExtractArchiveSkipOverwrite(dstArchive, rolePath);

                // Ensure we never end up with ...\App\App after extraction
                FlattenExtraLayer(rolePath);

                // Remove the copied archive after successful extract (consistent with prod)
                try { if (File.Exists(dstArchive)) File.Delete(dstArchive); } catch { /* ignore */ }

                Report($"{roleDisplayName}: done.");
            }

            // Backup if folder exists and isn’t empty; uses your existing CreateZipArchive
            static void TryBackupFolder(string sourceDir, string archivePath, Action<string> log)
            {
                if (!Directory.Exists(sourceDir)) { log($"[Backup] Skip: '{sourceDir}' not found."); return; }
                if (!Directory.EnumerateFileSystemEntries(sourceDir).Any()) { log($"[Backup] Skip: '{sourceDir}' empty."); return; }

                // Ensure parent exists
                Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

                // Use your existing zip helper for consistency with Production
                CreateZipArchive(sourceDir, archivePath);
                log($"[Backup] {archivePath}");
            }
        }

        private static string MakeUniqueTempFolder(string parent, string baseName)
        {
            var candidate = Path.Combine(parent, baseName);
            if (!Directory.Exists(candidate)) return candidate;

            int i = 1;
            while (true)
            {
                var c = Path.Combine(parent, baseName + $"_{i}");
                if (!Directory.Exists(c)) return c;
                i++;
            }
        }

        // Extracts .zip (native) and .7z (via 7z.exe) into destRoot, SKIPPING existing files
        private static void ExtractArchiveSkipOverwrite(string archivePath, string destRoot)
        {
            var ext = Path.GetExtension(archivePath).ToLowerInvariant();
            Directory.CreateDirectory(destRoot);

            if (ext == ".zip")
            {
                using var archive = ZipFile.OpenRead(archivePath);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    var destPath = Path.Combine(destRoot, entry.FullName);
                    var destDir = Path.GetDirectoryName(destPath)!;
                    Directory.CreateDirectory(destDir);

                    if (File.Exists(destPath))
                        continue; // preserve existing (e.g., web.config, config.json)

                    entry.ExtractToFile(destPath, overwrite: false);
                }
                return;
            }

            if (ext == ".7z")
            {
                var sevenZipExe = Find7zExe()
                    ?? throw new NotSupportedException("7-Zip (7z.exe) not found. Install 7-Zip or add it to PATH.");

                // -y  : assume Yes on all queries
                // -aos: skip extracting files that already exist (preserve)
                var psi = new ProcessStartInfo
                {
                    FileName = sevenZipExe,
                    Arguments = $"x \"{archivePath}\" -o\"{destRoot}\" -y -aos",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi)!;
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    throw new InvalidOperationException($"7z extraction failed (exit {proc.ExitCode}).");
                return;
            }

            throw new NotSupportedException($"Unsupported archive type: {ext}");
        }

        private static string? Find7zExe()
        {
            // 1) PATH
            try
            {
                var path = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(path))
                {
                    foreach (var dir in path.Split(Path.PathSeparator))
                    {
                        try
                        {
                            var candidate = Path.Combine(dir.Trim('"'), "7z.exe");
                            if (File.Exists(candidate)) return candidate;
                        }
                        catch { /* ignore */ }
                    }
                }
            }
            catch { /* ignore */ }

            // 2) Common installs
            string[] guesses =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
            };
            foreach (var g in guesses)
                if (File.Exists(g)) return g;

            return null;
        }

        // Updates the client directory by creating a backup, cleaning it, and copying new build files from the source directory.
        public static void UpdateClientDirectory(string targetDir, string newBuildSourceDir)
        {
            if (!Directory.Exists(targetDir))
                throw new DirectoryNotFoundException($"Target directory not found: {targetDir}");

            if (!Directory.Exists(newBuildSourceDir))
                throw new DirectoryNotFoundException($"Build source not found: {newBuildSourceDir}");

            string archivePath = Path.Combine(targetDir, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

            CreateZipArchive(targetDir, archivePath);
            CleanDirectoryExceptConfig(targetDir);
            CopyNewBuildFiles(newBuildSourceDir, targetDir);
        }

        // Creates a new site directory at the specified path, throwing an exception if it already exists.
        public static void CreateSiteDirectory(string path, Action<string>? onProgress = null)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                onProgress?.Invoke($"Created new directory: {path}");
            }
            else
            {
                // Instead of throwing, just reuse the folder (supports same-day updates)
                onProgress?.Invoke($"Directory already exists, continuing update: {path}");
            }
        }

        // Creates a ZIP archive from the specified source directory to the specified ZIP file path, overwriting if it already exists.
        private static void CreateZipArchive(string sourceDir, string zipPath)
        {
            // Normalize
            var sourceFull = Path.GetFullPath(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                            + Path.DirectorySeparatorChar;
            var destFull = Path.GetFullPath(zipPath);

            // Always delete the final target if it exists (old backup)
            if (File.Exists(destFull))
                File.Delete(destFull);

            // If the destination .zip is INSIDE the sourceDir, create elsewhere then move it in.
            if (destFull.StartsWith(sourceFull, StringComparison.OrdinalIgnoreCase))
            {
                var tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
                try
                {
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                    System.IO.Compression.ZipFile.CreateFromDirectory(sourceDir, tempZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                    // Move into place (overwrite already handled above)
                    File.Move(tempZip, destFull);
                }
                finally
                {
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* ignore */ }
                }
            }
            else
            {
                // Normal case: zip target is outside the source dir
                System.IO.Compression.ZipFile.CreateFromDirectory(sourceDir, destFull, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
        }

        // Create one tag folder directly under the client's External root (no "_backups")
        private static string GetExternalBackupDir(string externalRoot, string tag)
        {
            var dir = Path.Combine(externalRoot, tag);
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Keep ONLY the most recent backup directory (by tag name or last write); delete older ones.
        private static void KeepOnlyMostRecentBackup(string externalRoot, string currentTag, Action<string>? log = null)
        {
            var dirs = Directory.EnumerateDirectories(externalRoot)
                                .Select(d => new DirectoryInfo(d))
                                .Where(di =>
                                {
                                    var name = di.Name;
                                    // Prefer YYYYMMDD tags; otherwise fall back to any 8-digit folder name
                                    return name.Length == 8 && name.All(char.IsDigit);
                                })
                                .OrderByDescending(di => di.Name) // YYYYMMDD sorts lexicographically
                                .ToList();

            // We want to keep just ONE: the most recent (which is usually the one we just created)
            bool first = true;
            foreach (var di in dirs)
            {
                if (first) { first = false; continue; } // keep the newest
                try
                {
                    di.Delete(true);
                    log?.Invoke($"[Backup] Purged older backup: {di.FullName}");
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[Backup] Failed to purge '{di.FullName}': {ex.Message}");
                }
            }
        }

        // Cleans the specified directory by deleting all files and subdirectories except for those specified in the PreserveFiles list.
        private static void CleanDirectoryExceptConfig(string dir)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                string fileName = Path.GetFileName(file);
                if (!PreserveFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    File.Delete(file);
            }

            foreach (string subDir in Directory.GetDirectories(dir))
            {
                if (IsProtectedDir(subDir)) continue; // do not delete *webconfigbackup* folders
                Directory.Delete(subDir, true);
            }
        }

        // Copies new build files from the source directory to the destination directory, maintaining their relative paths.
        private static void CopyNewBuildFiles(string sourceDir, string destDir)
        {
            foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, filePath);
                string destPath = Path.Combine(destDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(filePath, destPath, overwrite: true);
            }
        }

        //Validates the build archive name based on the site type and throws exceptions if invalid.
        public static void ValidateBuildName(string archivePath, string siteType)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("Build path is required");

            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Build archive not found", archivePath);

            var fileName = Path.GetFileName(archivePath);

            bool isValid = siteType.ToLowerInvariant() switch
            {
                "production" => fileName.StartsWith("PBK", StringComparison.OrdinalIgnoreCase)
                                 || fileName.StartsWith("DBK", StringComparison.OrdinalIgnoreCase),

                "caseinfosearch" => fileName.StartsWith("CaseInfoSearch_", StringComparison.OrdinalIgnoreCase),

                "esubpoena" => fileName.StartsWith("eSubpoena_", StringComparison.OrdinalIgnoreCase),

                "dataaccess" => fileName.StartsWith("PBKDataAccess_", StringComparison.OrdinalIgnoreCase),

                _ => throw new ArgumentException($"Unknown site type: {siteType}")
            };

            if (!isValid)
                throw new InvalidOperationException(
                    $"Invalid build archive name for {siteType}: {fileName}");
        }

        public static bool IsValidBuildName(string? archivePath, string siteType)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                return false;

            var fileName = Path.GetFileName(archivePath);

            return siteType.ToLowerInvariant() switch
            {
                "production" => fileName.StartsWith("PBK", StringComparison.OrdinalIgnoreCase)
                                 || fileName.StartsWith("DBK", StringComparison.OrdinalIgnoreCase),

                "caseinfosearch" => fileName.StartsWith("CaseInfoSearch_", StringComparison.OrdinalIgnoreCase),

                "esubpoena" => fileName.StartsWith("eSubpoena_", StringComparison.OrdinalIgnoreCase),

                "dataaccess" => fileName.StartsWith("PBKDataAccess_", StringComparison.OrdinalIgnoreCase),

                _ => false
            };
        }

        // Ensures that the directory exists, creating it if necessary.
        private static void EnsureDirExists(string path)
        {
            Directory.CreateDirectory(path);
        }

        // Copies preserved files from the source root to the destination root, maintaining their relative paths.
        private static void CopyPreservedFiles(string srcRoot, string dstRoot, IEnumerable<string> relPaths)
        {
            foreach (var rel in relPaths)
            {
                var src = Path.Combine(srcRoot, rel);
                var dst = Path.Combine(dstRoot, rel);
                CopyFileWithRetry(src, dst);
            }
        }

        private static void CopyFileWithRetry(string src, string dst, int maxAttempts = 8, int delayMs = 250)
        {
            // Short-circuit if src == dst (same-day update self-copy)
            if (PathsEqual(src, dst)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

            void TryDeleteDst()
            {
                if (File.Exists(dst))
                {
                    try
                    {
                        var attr = File.GetAttributes(dst);
                        if ((attr & FileAttributes.ReadOnly) != 0)
                            File.SetAttributes(dst, attr & ~FileAttributes.ReadOnly);
                        File.Delete(dst);
                    }
                    catch { /* swallow; will retry */ }
                }
            }

            TryDeleteDst();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (File.Exists(src))
                    {
                        var sattr = File.GetAttributes(src);
                        if ((sattr & FileAttributes.ReadOnly) != 0)
                            File.SetAttributes(src, sattr & ~FileAttributes.ReadOnly);
                    }

                    File.Copy(src, dst, overwrite: false);
                    return; // success
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(delayMs);
                    TryDeleteDst();
                    continue;
                }
                catch (UnauthorizedAccessException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(delayMs);
                    TryDeleteDst();
                    continue;
                }
            }
            File.Copy(src, dst, overwrite: false);
        }

        // Deletes all files and directories inside the specified root, except for those specified in the relKeep list.
        private static void DeleteAllInsideExcept(string root, IEnumerable<string> relKeep)
        {
            var keepFullPaths = new HashSet<string>(
                relKeep.Select(r => Path.GetFullPath(Path.Combine(root, r))),
                StringComparer.OrdinalIgnoreCase);

            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(f);
                if (!keepFullPaths.Contains(full))
                {
                    TryDeleteFile(full);
                }
            }

            // Delete empty directories from deepest upward, skipping any that contain preserved files
            var allDirs = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(p => p.Length);

            foreach (var d in allDirs)
            {
                if (IsProtectedDir(d)) continue; // protect webconfigbackup dirs

                var dirFull = Path.GetFullPath(d);
                bool containsKept = keepFullPaths.Any(k =>
                    k.StartsWith(dirFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                if (!containsKept)
                {
                    TryDeleteDir(dirFull);
                }
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                var attr = File.GetAttributes(path);
                if ((attr & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attr & ~FileAttributes.ReadOnly);
                File.Delete(path);
            }
            catch { }
        }

        private static void TryDeleteDir(string path)
        {
            try { Directory.Delete(path, recursive: false); } catch { /* ignore */ }
        }

        private static void TryDeleteDirRecursive(string path)
        {
            try { Directory.Delete(path, recursive: true); } catch { /* ignore */ }
        }

        public static void SafeDeleteDirectory(string path)
        {
            TryDeleteDirRecursive(path);
        }

        public static string ResolveProdBasePath(string state, string client)
        {
            // If an override is provided, use it. This allows temporary redirection for non-prod flows.
            if (ProdBasePathOverride is not null)
                return ProdBasePathOverride(state, client);

            var root = AppPaths.SitesRoot; // throws if misconfigured in our helper

            var s = (state ?? "").Trim().ToUpperInvariant();
            var c = (client ?? "").Trim();

            if (s.Length == 0) throw new ArgumentException("state is required", nameof(state));
            if (c.Length == 0) throw new ArgumentException("client is required", nameof(client));

            return Path.Combine(root, s, s + c);
        }

        private static string MakeProdBackupZipPath(string basePath, string state, string client, string prevFolder)
        {
            return Path.Combine(basePath, $"{state}{client}_{prevFolder}.zip");
        }

        // NOTE: Removed MakeExternalBackupZipPath — we now do per-app backups under <externalRoot>\<tag>\*.zip

        // List of relative paths to preserve during production deployment cleanup
        private static readonly string[] ProdPreserveRelative =
        {
            @"app\environments\config.json",
            @"web.config"
        };

        private static readonly string[] ExternalPreserveRelative =
        {
            @"web.config",
            @"app\environments\config.json"
        };

        private static bool HasNonPreservedFiles(string root)
        {
            var preserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetFullPath(Path.Combine(root, "web.config")),
                Path.GetFullPath(Path.Combine(root, @"app\environments\config.json"))
            };

            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(f);
                if (!preserved.Contains(full))
                    return true;
            }
            return false;
        }

        private static bool VerifyProductionDeployment(string newPath)
        {
            try
            {
                if (!Directory.Exists(newPath)) return false;
                var webConfig = Path.Combine(newPath, "web.config");
                if (!File.Exists(webConfig)) return false;
                return HasNonPreservedFiles(newPath);
            }
            catch { return false; }
        }

        // Keep only the most recent {STATE}{CLIENT}_*.zip in basePath (delete older ones)
        private static void CleanupBackupsKeepMostRecent(string basePath, string state, string client)
        {
            try
            {
                var prefix = (state + client) + "_";
                var zips = Directory.EnumerateFiles(basePath, "*.zip", SearchOption.TopDirectoryOnly)
                                    .Where(p => Path.GetFileName(p)
                                        .StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                if (zips.Count <= 1) return;

                DateTime ExtractNameDateOrMin(string path)
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    var idx = name.LastIndexOf('_');
                    if (idx >= 0 && name.Length >= idx + 1 + 8)
                    {
                        var stamp = name.Substring(idx + 1, 8);
                        if (DateTime.TryParseExact(stamp, "MMddyyyy", null,
                            System.Globalization.DateTimeStyles.None, out var dt))
                            return dt;
                    }
                    return File.GetLastWriteTimeUtc(path);
                }

                var ordered = zips.OrderByDescending(ExtractNameDateOrMin).ToList();
                var keep = ordered.First();
                foreach (var p in ordered.Skip(1))
                {
                    try { File.Delete(p); } catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        }

        // When there is no dated prev folder, copy preserved files from basePath itself (overwrite)
        private static void ForceCopyPreservedFilesFromBase(string basePath, string newPath)
        {
            CopyOverwriteSkipSelf(Path.Combine(basePath, @"web.config"),
                                  Path.Combine(newPath, @"web.config"));

            CopyOverwriteSkipSelf(Path.Combine(basePath, @"app\environments\config.json"),
                                  Path.Combine(newPath, @"app\environments\config.json"));
        }

        // Force the preserved files to be the final copies in the new version folder (overwrite)
        private static void ForceCopyPreservedFiles(string prevPath, string newPath)
        {
            CopyOverwriteSkipSelf(Path.Combine(prevPath, @"web.config"),
                                  Path.Combine(newPath, @"web.config"));

            CopyOverwriteSkipSelf(Path.Combine(prevPath, @"app\environments\config.json"),
                                  Path.Combine(newPath, @"app\environments\config.json"));
        }

        private static void CopyOverwriteSkipSelf(string src, string dst)
        {
            if (PathsEqual(src, dst)) return; // no-op on self-copy (same-day update)
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, overwrite: true);
        }

        // Never delete folders that contain "webconfigbackup" (case-insensitive)
        private static bool IsProtectedDir(string path)
        {
            var name = Path.GetFileName(path);
            return name.IndexOf("webconfigbackup", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Flatten ANY top-level PBK/DBK folder(s) into 'root' (no overwrites), then remove them.
        // Works even if 'root' already contains 'app' and 'web.config'.
        private static void MergeFlattenTopFolderNoOverwrite(string root)
        {
            if (!Directory.Exists(root)) return;

            var wrappers = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly)
                .Where(d =>
                {
                    var n = Path.GetFileName(d);
                    return n.StartsWith("PBK", StringComparison.OrdinalIgnoreCase)
                        || n.StartsWith("DBK", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (wrappers.Count == 0) return;

            foreach (var top in wrappers)
            {
                foreach (var srcFile in Directory.EnumerateFiles(top, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(top, srcFile); // keep build's internal structure
                    var dst = Path.Combine(root, rel);

                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

                    // DO NOT overwrite existing files (preserved configs already copied)
                    if (!File.Exists(dst))
                    {
                        try { File.Move(srcFile, dst); }
                        catch
                        {
                            try { File.Copy(srcFile, dst, overwrite: false); } catch { /* ignore */ }
                        }
                    }
                }

                // Remove the wrapper folder (guard protected names just in case)
                if (!IsProtectedDir(top))
                    TryDeleteDirRecursive(top);
            }

            // Prune empty dirs but never touch *webconfigbackup*
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                         .OrderByDescending(p => p.Length))
            {
                try
                {
                    if (IsProtectedDir(dir)) continue;
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir, false);
                }
                catch { /* ignore */ }
            }
        }

        // Creation-path fallback: ensure preserved files exist after extraction/flatten by using build outputs.
        // - web.config is REQUIRED; if still missing, throw.
        // - config.json is considered required per your latest notes ("this file will always exist"), so also throw if missing.
        private static void EnsurePreservedFromBuildOrFail(string newPath)
        {
            var dstWeb = Path.Combine(newPath, "web.config");
            if (!File.Exists(dstWeb))
                throw new FileNotFoundException($"Required web.config not found in new build at: {dstWeb}");

            var dstCfg = Path.Combine(newPath, @"app\environments\config.json");
            if (!File.Exists(dstCfg))
                throw new FileNotFoundException($"Required app\\environments\\config.json not found in new build at: {dstCfg}");
        }

        // Applies a build from the specified source directory to the target site directory, preserving certain files.
        public static void ApplyBuild(string siteName, string buildSourceDir, bool isProd)
        {
            string baseWebRoot = AppPaths.SitesRoot;
            string targetDir = Path.Combine(baseWebRoot, siteName);

            if (!Directory.Exists(targetDir))
                throw new DirectoryNotFoundException($"Site directory not found: {targetDir}");

            string buildType = isProd ? "PROD" : "EXT";
            Console.WriteLine($"Applying {buildType} build to: {siteName}");
            Console.WriteLine($"From: {buildSourceDir}");

            UpdateClientDirectory(targetDir, buildSourceDir);
        }

        // -----------------------------
        // NEW: convenience getters for callers/IISManager
        // -----------------------------
        public static (string BasePath, string PrevPath, string NewPath) GetProductionPaths(
            string state, string client, string prevFolder, string newFolder)
        {
            var basePath = ResolveProdBasePath(state, client);
            return (
                BasePath: basePath,
                PrevPath: Path.Combine(basePath, prevFolder),
                NewPath: Path.Combine(basePath, newFolder)
            );
        }

        public static (string ExternalRoot, string CaseInfoSearchPath, string ESubpoenaPath, string DataAccessPath)
            GetExternalRolePaths(string externalRoot)
        {
            return (
                ExternalRoot: externalRoot,
                CaseInfoSearchPath: Path.Combine(externalRoot, "CaseInfoSearch"),
                ESubpoenaPath: Path.Combine(externalRoot, "eSubpoena"),
                DataAccessPath: Path.Combine(externalRoot, "DataAccess")
            );
        }

        // -----------------------------
        // small path/helper utilities
        // -----------------------------
        private static bool PathsEqual(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Flattens a redundant wrapper folder inside appRoot:
        /// - If a subfolder exists with the same name as appRoot, flatten it.
        /// - Special-case: DataAccess & PBKDataAccess are treated as interchangeable wrappers;
        ///   flatten whichever appears nested (e.g., ...\DataAccess\PBKDataAccess or ...\PBKDataAccess\DataAccess).
        /// Preserved files already in appRoot are never overwritten.
        /// </summary>
        private static void FlattenExtraLayer(string appRoot)
        {
            if (string.IsNullOrWhiteSpace(appRoot) || !Directory.Exists(appRoot))
                return;

            string appName = Path.GetFileName(
                appRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // Candidates to flatten:
            var candidates = new List<string>();

            // 1) Same-name wrapper: ...\AppRoot\AppRoot
            var sameName = Path.Combine(appRoot, appName);
            if (Directory.Exists(sameName))
                candidates.Add(sameName);

            // 2) DataAccess/PBKDataAccess counterpart wrapper
            string? counterpart = appName.Equals("DataAccess", StringComparison.OrdinalIgnoreCase) ? "PBKDataAccess"
                                  : appName.Equals("PBKDataAccess", StringComparison.OrdinalIgnoreCase) ? "DataAccess"
                                  : null;
            if (counterpart != null)
            {
                var cp = Path.Combine(appRoot, counterpart);
                if (Directory.Exists(cp))
                    candidates.Add(cp);
            }

            // If nothing obvious, nothing to do.
            if (candidates.Count == 0) return;

            foreach (var nested in candidates)
                MergeUpNoOverwrite(nested, appRoot);

            // Local: merge nested -> appRoot without overwriting, then best-effort delete wrapper
            static void MergeUpNoOverwrite(string nested, string appRoot)
            {
                // Move/merge files
                foreach (var srcFile in Directory.EnumerateFiles(nested, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(nested, srcFile);
                    string dst = Path.Combine(appRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

                    if (File.Exists(dst)) continue; // never overwrite

                    try { File.Move(srcFile, dst); }
                    catch { try { File.Copy(srcFile, dst, overwrite: false); } catch { /* ignore */ } }
                }

                // Prune empty directories under the wrapper (deepest-first)
                foreach (var dir in Directory.EnumerateDirectories(nested, "*", SearchOption.AllDirectories)
                                             .OrderByDescending(p => p.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir, false);
                    }
                    catch { /* ignore */ }
                }

                // Remove wrapper if empty; else best-effort recursive delete
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(nested).Any())
                        Directory.Delete(nested, false);
                    else
                        Directory.Delete(nested, true);
                }
                catch { /* ignore */ }
            }
        }
    }
}
