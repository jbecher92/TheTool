using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace TheTool
{
    public static class FileManager
    {
        public static string ResolveProdBasePath(string resolvedRoot, string state, string client)
        {
            if (string.IsNullOrWhiteSpace(resolvedRoot))
                throw new ArgumentException("resolvedRoot must be provided", nameof(resolvedRoot));
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException("state is required", nameof(state));
            if (string.IsNullOrWhiteSpace(client))
                throw new ArgumentException("client is required", nameof(client));

            string s = state.Trim().ToUpperInvariant();
            string c = client.Trim();

            // Production gets a state folder; internal/test do NOT.
            string flavor = (AppConfigManager.Config.DeploymentFlavor ?? "prod").Trim().ToLowerInvariant();

            string basePath = flavor == "prod"
                ? Path.Combine(resolvedRoot, s, s + c)  // PROD:   {root}\{STATE}\{STATE}{client}
                : Path.Combine(resolvedRoot, s + c);    // NON-PROD: {root}\{STATE}{client}

            return Path.GetFullPath(basePath);
        }

        public static string ResolveExternalRoot(
            string state,
            string client,
            string deploymentFlavor,
            string resolvedRoot,
            Dictionary<string, string>? /*unused*/ cache = null)
        {
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException("state is required", nameof(state));
            if (string.IsNullOrWhiteSpace(client))
                throw new ArgumentException("client is required", nameof(client));
            if (string.IsNullOrWhiteSpace(resolvedRoot))
                throw new ArgumentException("resolvedRoot must be provided", nameof(resolvedRoot));

            string s = state.Trim().ToUpperInvariant();
            string c = client.Trim();
            string flavor = (deploymentFlavor ?? "prod").Trim().ToLowerInvariant();
            string basePath = flavor == "prod" ? Path.Combine(resolvedRoot, s) : resolvedRoot;
            string containerName = "PbkExternal"; //Default container name to fall back on
            try
            {
                if (Directory.Exists(basePath))
                {
                    var match = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
                                         .Select(Path.GetFileName)
                                         .FirstOrDefault(name =>
                                             !string.IsNullOrEmpty(name) &&
                                             name.IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0);

                    // Wildcard search, only override if something was actually found
                    if (!string.IsNullOrEmpty(match))
                        containerName = match;
                }
            }
            catch { /* ignore */}

            string clientRoot = Path.Combine(basePath, containerName, s + c);
            return Path.GetFullPath(clientRoot);
        }

        public static void DeployProduction_Update(
            string state,
            string client,
            string resolvedRoot,
            string prevFolder,
            string newFolder,
            string newBuildArchivePath,
            Action<string>? onProgress = null)
        {
            void Report(string m) { try { onProgress?.Invoke(m); } catch { } }
            string siteTag = $"{state}{client}";

            if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("state required");
            if (string.IsNullOrWhiteSpace(client)) throw new ArgumentException("client required");
            if (string.IsNullOrWhiteSpace(newFolder)) throw new ArgumentException("newFolder required");
            if (string.IsNullOrWhiteSpace(newBuildArchivePath) || !File.Exists(newBuildArchivePath))
                throw new FileNotFoundException("New build archive not found", newBuildArchivePath);

            var ext = Path.GetExtension(newBuildArchivePath).ToLowerInvariant();
            if (ext != ".zip" && ext != ".7z")
                throw new NotSupportedException("Only .zip or .7z extraction is supported at this time.");

            var basePath = ResolveProdBasePath(resolvedRoot, state, client);
            var prevPath = Path.Combine(basePath, prevFolder);
            var newPath = Path.Combine(basePath, newFolder);

            EnsureDirExists(basePath);

            // Always create the new dated folder 
            Report($"{siteTag}: Updating Production - {newPath}");
            CreateSiteDirectory(newPath);

            bool creationPath = string.IsNullOrWhiteSpace(prevFolder) || !Directory.Exists(prevPath);
            bool deferredPreserveFromBuild = false;

            // Soft-note if previous lacked config.json 
            if (!creationPath)
            {
                var prevCfg = Path.Combine(prevPath, @"app\environments\config.json");
                if (!File.Exists(prevCfg))
                    Report($"{siteTag}[Config]: Previous build missing app\\environments\\config.json");
            }

            if (!creationPath)
            {
                // 1) Backup previous folder
                var backupZip = MakeProdBackupZipPath(basePath, state, client, prevFolder);
                CreateZipArchive(prevPath, backupZip);

                // 2) Rename previous folder to temp_* and operate on that
                var tempPath = MakeUniqueTempFolder(basePath, "temp_" + DateTime.Now.ToString("MMddyyyy_HHmmss"));
                Directory.Move(prevPath, tempPath);
                prevPath = tempPath;

                // 3) Clean temp (old) folder, keeping preserved files
                DeleteAllInsideExcept(prevPath, ProdPreserveRelative);

                var srcWeb = Path.Combine(prevPath, "web.config");
                var srcCfg = Path.Combine(prevPath, @"app\environments\config.json");
                if (!File.Exists(srcWeb))
                    Report($"{siteTag}[Preserve]: web.config not found in previous build (continuing).");
                if (!File.Exists(srcCfg))
                    Report($"{siteTag}[Config]: Missing preserved file - {srcCfg}");

                // 4) Copy preserved files from temp into the new folder 
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

            // 6) Final preserved rules/validation (soft for config.json, hard for web.config)
            if (!creationPath)
            {
                // Ensure preserved files from temp are the final ones (self-copy guarded)
                ForceCopyPreservedFiles(prevPath, newPath);
            }
            else
            {
                if (deferredPreserveFromBuild)
                    EnsurePreservedFromBuildOrFail(newPath); // only hard-require web.config
                else
                    ForceCopyPreservedFilesFromBase(basePath, newPath);
            }

            // 6b) If config.json is still missing, try to seed ONLY that file from the prod archive (soft behavior)
            var newCfg = Path.Combine(newPath, @"app\environments\config.json");
            if (!File.Exists(newCfg))
                TrySeedSingleConfigFromArchive(newBuildArchivePath, newPath, onProgress);

            // 7) Cleanup temp (old) folder and deployed zip
            if (!creationPath)
                TryDeleteDirRecursive(prevPath);

            try { File.Delete(dstZip); } catch { /* ignore */ }

            // 8) Keep only the most recent backup zip
            CleanupBackupsKeepMostRecent(basePath, state, client);

            Report($"{siteTag}: Production Update Complete.");
        }


        public static void DeployExternal_Update(
            string resolvedRoot,
            string state,
            string client,
            string backupTag,
            string? caseInfoSearchZip,
            string? esubpoenaZip,
            string? dataAccessZip,
            Action<string>? onProgress = null)
        {
            void Report(string msg) { try { onProgress?.Invoke(msg); } catch { } }
            string siteTag = $"{state}{client}";

            if (string.IsNullOrWhiteSpace(resolvedRoot)) throw new ArgumentException("resolvedRoot required");
            if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("state required");
            if (string.IsNullOrWhiteSpace(client)) throw new ArgumentException("client required");
            if (string.IsNullOrWhiteSpace(backupTag)) throw new ArgumentException("backupTag required");

            //Resolve deterministically, then do any disk checks/creates here
            string externalRoot = ResolveExternalRoot(state, client, AppConfigManager.Config.DeploymentFlavor ?? "prod", resolvedRoot);

            // Choose the folder (PbkExternal vs ExternalSites)
            string s = state.ToUpperInvariant();
            string flavor = (AppConfigManager.Config.DeploymentFlavor ?? "prod").Trim().ToLowerInvariant();
            string basePath = flavor == "prod" ? Path.Combine(resolvedRoot, s) : resolvedRoot;

            string candidatePbk = Path.Combine(basePath, "PbkExternal", s + client);
            string candidateExt = Path.Combine(basePath, "ExternalSites", s + client);

            // Locate an existing external base by wildcard
            var externalBase = Directory.GetDirectories(basePath)
                .FirstOrDefault(d => Path.GetFileName(d)
                    .IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0);

            if (externalBase == null)
            {
                Report($"{siteTag}[Skip]: No external base folder found under '{basePath}' for state '{s}'. ");
                return; 
            }

            // Require an existing subfolder for this client
            externalRoot = Path.Combine(externalBase, s + client);
            if (!Directory.Exists(externalRoot))
            {
                Report($"{siteTag}[Skip]: External root not found for '{s}{client}' under '{externalBase}'. ");
                return;
            }

            // Validate archives 
            if (!string.IsNullOrWhiteSpace(caseInfoSearchZip)) ValidateBuildName(caseInfoSearchZip, "caseinfosearch");
            if (!string.IsNullOrWhiteSpace(esubpoenaZip)) ValidateBuildName(esubpoenaZip, "esubpoena");
            if (!string.IsNullOrWhiteSpace(dataAccessZip)) ValidateBuildName(dataAccessZip, "dataaccess");

            // Determine DataAccess folder name at execution time only
            string daFolder = Directory.Exists(Path.Combine(externalRoot, "PBKDataAccess")) ? "PBKDataAccess" : "DataAccess";

            // Canonical paths
            string cisPath = Path.Combine(externalRoot, "CaseInfoSearch");
            string esPath = Path.Combine(externalRoot, "eSubpoena");
            string daPath = Path.Combine(externalRoot, daFolder);

            // Backup tag directory
            string backupDir = GetExternalBackupDir(externalRoot, backupTag);

            // --- Backups ---
            if (!string.IsNullOrWhiteSpace(caseInfoSearchZip))
                TryBackupFolder(cisPath, Path.Combine(backupDir, "CaseInfoSearch.zip"), Report);

            if (!string.IsNullOrWhiteSpace(esubpoenaZip))
                TryBackupFolder(esPath, Path.Combine(backupDir, "eSubpoena.zip"), Report);

            if (!string.IsNullOrWhiteSpace(dataAccessZip))
                TryBackupFolder(daPath, Path.Combine(backupDir, daFolder + ".zip"), Report);

            // --- Deploy roles ---
            DeployRoleIfProvided("CaseInfoSearch", caseInfoSearchZip, cisPath);
            DeployRoleIfProvided("eSubpoena", esubpoenaZip, esPath);
            DeployRoleIfProvided(daFolder, dataAccessZip, daPath); 

            // Cleanup old backups
            KeepOnlyMostRecentBackup(externalRoot, backupTag, Report);

            Report($"{siteTag}: External Update(s) Complete.");

            // -------- Local helpers ----------

            void DeployRoleIfProvided(string roleDisplayName, string? archivePath, string rolePath)
            {
                if (string.IsNullOrWhiteSpace(archivePath))
                    return;

                if (!File.Exists(archivePath))
                {
                    Report($"{siteTag}: {roleDisplayName} — archive not found. Skipping.");
                    return;
                }

                Report($"{siteTag}: Updating {roleDisplayName}- {rolePath}");

                // Record whether previous role folder had config.json
                var prevCfg = Path.Combine(rolePath, @"app\environments\config.json");
                if (RequiresAppEnvironmentsConfig(roleDisplayName) && !File.Exists(prevCfg))
                    Report($"{siteTag}[Config]:  Previous build missing app\\environments\\config.json for {roleDisplayName}.");

                var ext = Path.GetExtension(archivePath).ToLowerInvariant();
                if (ext != ".zip" && ext != ".7z")
                {
                    Report($"{siteTag}{roleDisplayName}[Skip]: archive must be .zip or .7z");
                    return;
                }

                Directory.CreateDirectory(rolePath);
                DeleteAllInsideExcept(rolePath, ExternalPreserveRelative);

                var dstArchive = Path.Combine(rolePath, Path.GetFileName(archivePath));
                try
                {
                    File.Copy(archivePath, dstArchive, overwrite: true);
                }
                catch (Exception ex)
                {
                    Report($"{siteTag}{roleDisplayName}[Skip]: failed to stage archive: {ex.Message}.");
                    return;
                }

                ExtractArchiveSkipOverwrite(dstArchive, rolePath);
                FlattenExtraLayer(rolePath);

                try { if (File.Exists(dstArchive)) File.Delete(dstArchive); } catch { }

                // If still missing, try to seed ONLY config.json from this role's archive 
                var newCfg = Path.Combine(rolePath, @"app\environments\config.json");
                if (RequiresAppEnvironmentsConfig(roleDisplayName) && !File.Exists(newCfg))
                    TrySeedSingleConfigFromArchive(archivePath, rolePath, onProgress);
            }


            static void TryBackupFolder(string sourceDir, string archivePath, Action<string> log)
            {
                if (!Directory.Exists(sourceDir)) { log($"[Backup] Skip: '{sourceDir}' not found."); return; }
                if (!Directory.EnumerateFileSystemEntries(sourceDir).Any()) { log($"[Backup] Skip: '{sourceDir}' empty."); return; }

                Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
                CreateZipArchive(sourceDir, archivePath);
            }
        }

        // =====================================================================
        //                              I/O UTILITIES
        // =====================================================================

        private static bool TrySeedSingleConfigFromArchive(string archivePath, string destRoot, Action<string>? log)
        {
            const string relConfig = @"app\environments\config.json";
            var outPath = Path.Combine(destRoot, relConfig);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

                var ext = Path.GetExtension(archivePath).ToLowerInvariant();
                if (ext == ".zip")
                {
                    using var archive = ZipFile.OpenRead(archivePath);

                    // Find ANY entry whose normalized path ends with app/environments/config.json
                    string suffix = "app/environments/config.json";
                    var entry = archive.Entries.FirstOrDefault(e =>
                        e != null &&
                        !string.IsNullOrEmpty(e.FullName) &&
                        e.FullName.Replace('\\', '/').EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

                    if (entry == null)
                    {
                        log?.Invoke("[Config] Build did not contain app\\environments\\config.json.");
                        return false;
                    }

                    entry.ExtractToFile(outPath, overwrite: true);
                    log?.Invoke("[Config] Seeded app\\environments\\config.json from build.");
                    return true;
                }

                if (ext == ".7z")
                {
                    var sevenZipExe = Find7zExe();
                    if (sevenZipExe == null)
                    {
                        log?.Invoke("[Config] 7z.exe not found; cannot seed app\\environments\\config.json.");
                        return false;
                    }

                    // Use recursive mask so it matches in nested PBK/DBK wrappers:
                    //   -r : recurse
                    //   -aos: skip overwrite of existing 
                    //   mask: *\app\environments\config.json
                    var psi = new ProcessStartInfo
                    {
                        FileName = sevenZipExe,
                        Arguments = $"x \"{archivePath}\" -o\"{destRoot}\" -y -aos -r \"*\\app\\environments\\config.json\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var p = Process.Start(psi)!;
                    p.WaitForExit();

                    if (p.ExitCode == 0 && File.Exists(outPath))
                    {
                        log?.Invoke("[Config] Seeded app\\environments\\config.json from build.");
                        return true;
                    }

                    log?.Invoke("[Config] Build did not contain app\\environments\\config.json.");
                    return false;
                }

                log?.Invoke("[Config] Unsupported archive type for seeding.");
                return false;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Config] Failed to seed app\\environments\\config.json: {ex.Message}");
                return false;
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
                        continue; 

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

            string[] guesses =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
            };
            foreach (var g in guesses)
                if (File.Exists(g)) return g;

            return null;
        }

        public static void CreateSiteDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void CreateZipArchive(string sourceDir, string zipPath)
        {
            var sourceFull = Path.GetFullPath(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                            + Path.DirectorySeparatorChar;
            var destFull = Path.GetFullPath(zipPath);

            if (File.Exists(destFull))
                File.Delete(destFull);

            if (destFull.StartsWith(sourceFull, StringComparison.OrdinalIgnoreCase))
            {
                var tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
                try
                {
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                    ZipFile.CreateFromDirectory(sourceDir, tempZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                    File.Move(tempZip, destFull);
                }
                finally
                {
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                }
            }
            else
            {
                ZipFile.CreateFromDirectory(sourceDir, destFull, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
        }

        private static string GetExternalBackupDir(string externalRoot, string tag)
        {
            var dir = Path.Combine(externalRoot, tag);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void KeepOnlyMostRecentBackup(string externalRoot, string currentTag, Action<string>? log = null)
        {
            var dirs = Directory.EnumerateDirectories(externalRoot)
                                .Select(d => new DirectoryInfo(d))
                                .Where(di =>
                                {
                                    var name = di.Name;
                                    return name.Length == 8 && name.All(char.IsDigit);
                                })
                                .OrderByDescending(di => di.Name)
                                .ToList();

            bool first = true;
            foreach (var di in dirs)
            {
                if (first) { first = false; continue; }
                try
                {
                    di.Delete(true);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[Backup] Failed to purge '{di.FullName}': {ex.Message}");
                }
            }
        }

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
                throw new InvalidOperationException($"Invalid build archive name for {siteType}: {fileName}");
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

        private static void EnsureDirExists(string path) => Directory.CreateDirectory(path);

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
                    catch { }
                }
            }

            TryDeleteDst();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // If the source vanished (e.g., temp path), log and skip this preserve copy
                    if (!File.Exists(src))
                    {
                        return;
                    }

                    var sattr = File.GetAttributes(src);
                    if ((sattr & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(src, sattr & ~FileAttributes.ReadOnly);

                    File.Copy(src, dst, overwrite: false);
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(delayMs);
                    TryDeleteDst();
                }
                catch (UnauthorizedAccessException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(delayMs);
                    TryDeleteDst();
                }
                // Non-retryable / not worth retrying: log and continue overall deployment
                catch (FileNotFoundException)
                {
                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    return;
                }
                catch (PathTooLongException)
                {
                    return;
                }
            }

            // Final attempt after retries — do not crash the run
            try
            {
                File.Copy(src, dst, overwrite: false);
            }
            catch (Exception)
            {
            }

        }

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

            var allDirs = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(p => p.Length);

            foreach (var d in allDirs)
            {
                if (IsProtectedDir(d)) continue;

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
            try { Directory.Delete(path, recursive: false); } catch { }
        }

        private static void TryDeleteDirRecursive(string path)
        {
            try { Directory.Delete(path, recursive: true); } catch { }
        }

        private static string MakeProdBackupZipPath(string basePath, string state, string client, string prevFolder)
        {
            return Path.Combine(basePath, $"{state}{client}_{prevFolder}.zip");
        }

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
                foreach (var p in ordered.Skip(1))
                {
                    try { File.Delete(p); } catch { }
                }
            }
            catch { }
        }

        private static void ForceCopyPreservedFilesFromBase(string basePath, string newPath)
        {
            CopyOverwriteSkipSelf(Path.Combine(basePath, @"web.config"),
                                  Path.Combine(newPath, @"web.config"));

            CopyOverwriteSkipSelf(Path.Combine(basePath, @"app\environments\config.json"),
                                  Path.Combine(newPath, @"app\environments\config.json"));
        }

        private static void ForceCopyPreservedFiles(string prevPath, string newPath)
        {
            CopyOverwriteSkipSelf(Path.Combine(prevPath, @"web.config"),
                                  Path.Combine(newPath, @"web.config"));

            CopyOverwriteSkipSelf(Path.Combine(prevPath, @"app\environments\config.json"),
                                  Path.Combine(newPath, @"app\environments\config.json"));
        }

        private static void CopyOverwriteSkipSelf(string src, string dst)
        {
            if (PathsEqual(src, dst)) return;

            try
            {
                string? dir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                // If the source doesn't exist (or its directory doesn't), silently skip.
                if (!File.Exists(src))
                    return;

                File.Copy(src, dst, overwrite: true);
            }
            catch (FileNotFoundException)
            {
                
            }
            catch (DirectoryNotFoundException)
            {
                
            }
        }


        private static bool IsProtectedDir(string path)
        {
            var name = Path.GetFileName(path);
            return name.IndexOf("webconfigbackup", StringComparison.OrdinalIgnoreCase) >= 0;
        }

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
                    var rel = Path.GetRelativePath(top, srcFile);
                    var dst = Path.Combine(root, rel);

                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

                    if (!File.Exists(dst))
                    {
                        try { File.Move(srcFile, dst); }
                        catch
                        {
                            try { File.Copy(srcFile, dst, overwrite: false); } catch { }
                        }
                    }
                }

                if (!IsProtectedDir(top))
                    TryDeleteDirRecursive(top);
            }

            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                         .OrderByDescending(p => p.Length))
            {
                try
                {
                    if (IsProtectedDir(dir)) continue;
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir, false);
                }
                catch { }
            }
        }

        private static void EnsurePreservedFromBuildOrFail(string newPath)
        {
            var dstWeb = Path.Combine(newPath, "web.config");
            if (!File.Exists(dstWeb))
                throw new FileNotFoundException($"Required web.config not found in new build at: {dstWeb}");

            // app\environments\config.json is soft: do not throw if missing.
        }

        private static bool PathsEqual(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }

        private static void FlattenExtraLayer(string appRoot)
        {
            if (string.IsNullOrWhiteSpace(appRoot) || !Directory.Exists(appRoot))
                return;

            string appName = Path.GetFileName(
                appRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var candidates = new List<string>();

            var sameName = Path.Combine(appRoot, appName);
            if (Directory.Exists(sameName))
                candidates.Add(sameName);

            string? counterpart = appName.Equals("DataAccess", StringComparison.OrdinalIgnoreCase) ? "PBKDataAccess"
                                  : appName.Equals("PBKDataAccess", StringComparison.OrdinalIgnoreCase) ? "DataAccess"
                                  : null;
            if (counterpart != null)
            {
                var cp = Path.Combine(appRoot, counterpart);
                if (Directory.Exists(cp))
                    candidates.Add(cp);
            }

            if (candidates.Count == 0) return;

            foreach (var nested in candidates)
                MergeUpNoOverwrite(nested, appRoot);

            static void MergeUpNoOverwrite(string nested, string appRoot)
            {
                foreach (var srcFile in Directory.EnumerateFiles(nested, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(nested, srcFile);
                    string dst = Path.Combine(appRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

                    if (File.Exists(dst)) continue;

                    try { File.Move(srcFile, dst); }
                    catch { try { File.Copy(srcFile, dst, overwrite: false); } catch { } }
                }

                foreach (var dir in Directory.EnumerateDirectories(nested, "*", SearchOption.AllDirectories)
                                             .OrderByDescending(p => p.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir, false);
                    }
                    catch { }
                }

                try
                {
                    if (!Directory.EnumerateFileSystemEntries(nested).Any())
                        Directory.Delete(nested, false);
                    else
                        Directory.Delete(nested, true);
                }
                catch { }
            }
        }

        private static bool RequiresAppEnvironmentsConfig(string appFolderName)
        {
            // Only EAP/CaseInfoSearch and eSubpoena require app\environments\config.json
            return appFolderName.Equals("CaseInfoSearch", StringComparison.OrdinalIgnoreCase)
                || appFolderName.Equals("eSubpoena", StringComparison.OrdinalIgnoreCase);
        }


        //private static bool TrySeedSingleConfigFromArchive(string archivePath, string destRoot, Action<string>? log)
        //{
        //    const string relConfig = @"app\environments\config.json";
        //    var outPath = Path.Combine(destRoot, relConfig);

        //    try
        //    {
        //        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        //        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        //        if (ext == ".zip")
        //        {
        //            using var archive = ZipFile.OpenRead(archivePath);
        //            // Normalize both sides to forward slashes for comparison
        //            string want = "app/environments/config.json";
        //            var entry = archive.Entries.FirstOrDefault(e =>
        //                string.Equals(e.FullName.Replace('\\', '/'), want, StringComparison.OrdinalIgnoreCase));

        //            if (entry == null)
        //            {
        //                log?.Invoke("[Config] Build did not contain app\\environments\\config.json.");
        //                return false;
        //            }

        //            entry.ExtractToFile(outPath, overwrite: true);
        //            log?.Invoke("[Config] Seeded app\\environments\\config.json from build.");
        //            return true;
        //        }

        //        if (ext == ".7z")
        //        {
        //            var sevenZipExe = Find7zExe();
        //            if (sevenZipExe == null)
        //            {
        //                log?.Invoke("[Config] 7z.exe not found; cannot seed app\\environments\\config.json.");
        //                return false;
        //            }

        //            // Extract just the one file, skipping overwrites elsewhere (-aos).
        //            var psi = new ProcessStartInfo
        //            {
        //                FileName = sevenZipExe,
        //                Arguments = $"x \"{archivePath}\" -o\"{destRoot}\" -y -aos \"{relConfig}\"",
        //                UseShellExecute = false,
        //                CreateNoWindow = true,
        //                RedirectStandardOutput = true,
        //                RedirectStandardError = true
        //            };

        //            using var p = Process.Start(psi)!;
        //            p.WaitForExit();

        //            if (p.ExitCode == 0 && File.Exists(outPath))
        //            {
        //                log?.Invoke("[Config] Seeded app\\environments\\config.json from build.");
        //                return true;
        //            }

        //            log?.Invoke("[Config] Build did not contain app\\environments\\config.json.");
        //            return false;
        //        }

        //        log?.Invoke("[Config] Unsupported archive type for seeding.");
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        log?.Invoke($"[Config] Failed to seed app\\environments\\config.json: {ex.Message}");
        //        return false;
        //    }
        //}
    }
}
