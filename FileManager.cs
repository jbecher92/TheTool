using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace TheTool
{
    public static class FileManager
    {
        // ===============================
        //      IIS-AWARE RESOLVERS
        // ===============================

        public static string ResolveProdBasePath(string resolvedRoot, string state, string client, string? siteFolderNameOverride = null)
        {
            if (string.IsNullOrWhiteSpace(resolvedRoot))
                throw new ArgumentException("resolvedRoot must be provided", nameof(resolvedRoot));
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException("state is required", nameof(state));
            if (string.IsNullOrWhiteSpace(client))
                throw new ArgumentException("client is required", nameof(client));

            string s = state.Trim().ToUpperInvariant();
            string c = client.Trim();
            string siteName = siteFolderNameOverride ?? (s + c);

            string normalized = Path.GetFullPath(resolvedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string last = Path.GetFileName(normalized);

            // If resolvedRoot is already the IIS site base (…\STATE\STATECLIENT), return as-is.
            if (string.Equals(last, siteName, StringComparison.OrdinalIgnoreCase))
                return normalized;

            // Legacy fallback (supports old callers that pass a high-level root)
            string possible = Path.Combine(normalized, s, siteName);
            return Path.GetFullPath(possible);
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

            // If resolvedRoot already points to external root (…\*External*\STATECLIENT), honor it.
            string norm = Path.GetFullPath(resolvedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string last = Path.GetFileName(norm);

            static bool IsRole(string name) =>
                name.Equals("CaseInfoSearch", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("eSubpoena", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("DataAccess", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("PBKDataAccess", StringComparison.OrdinalIgnoreCase);

            // If the last segment is a role folder, external root is its parent (…\*External*\STATECLIENT)
            if (IsRole(last))
            {
                string parent = Path.GetDirectoryName(norm)!;
                string tail = Path.GetFileName(parent);
                if (string.Equals(tail, s + c, StringComparison.OrdinalIgnoreCase))
                    return parent;
            }
            else
            {
                // Last segment could already be STATECLIENT
                if (string.Equals(last, s + c, StringComparison.OrdinalIgnoreCase))
                {
                    string parent = Path.GetDirectoryName(norm) ?? "";
                    string container = Path.GetFileName(parent);
                    if (!string.IsNullOrEmpty(container) &&
                        container.IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0)
                        return norm; // already the external root
                }
            }

            //External folder check
            bool rootLooksExternal =
                last.IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0;

            if (rootLooksExternal)
            {
                string clientRoot = Path.Combine(norm, s + c);
                return Path.GetFullPath(clientRoot);
            }

            // Legacy fallback
            string flavor = (deploymentFlavor ?? "prod").Trim().ToLowerInvariant();
            bool nestByState = flavor == "prod";
            string basePath = nestByState ? Path.Combine(resolvedRoot, s) : resolvedRoot;

            // Pick existing external container name if present
            string containerName = "PbkExternal";
            try
            {
                if (Directory.Exists(basePath))
                {
                    var match = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
                                         .Select(Path.GetFileName)
                                         .FirstOrDefault(name =>
                                             !string.IsNullOrEmpty(name) &&
                                             name.IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!string.IsNullOrEmpty(match))
                        containerName = match;
                }
            }
            catch { /* ignore */ }

            string clientRootFallback = Path.Combine(basePath, containerName, s + c);
            return Path.GetFullPath(clientRootFallback);
        }



        // =====================================================================
        //                           PRODUCTION 
        // =====================================================================

        public static void DeployProduction_Update(
            string state,
            string client,
            string resolvedRoot,     // IIS base or legacy high-level root
            string prevFolder,       // empty or missing => non-dated update branch
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

            var ext = Path.GetExtension(newBuildArchivePath).ToLowerInvariant();
            if (ext != ".zip" && ext != ".7z")
                throw new NotSupportedException("Only .zip or .7z extraction is supported at this time.");

            string siteTag = $"{state}{client}";
            string basePath = Path.GetFullPath(resolvedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            EnsureDirExists(basePath);

            string newPath = Path.Combine(basePath, newFolder);
            string prevPathTagged = string.IsNullOrWhiteSpace(prevFolder) ? string.Empty : Path.Combine(basePath, prevFolder);
            bool hasPrevTag = !string.IsNullOrWhiteSpace(prevFolder) && Directory.Exists(prevPathTagged);
            
            // ===== Non-dated update (base was live) =====
            if (!hasPrevTag)
            {
                // 1) Backup the BASE folder (zip lives in base; suffix '_base' avoids collisions)
                string backupZip = MakeProdBackupZipPath(basePath, state, client, "base");
                Report($"{siteTag}: Creating Backup - {backupZip}");
                CreateZipArchive(basePath, backupZip);

                // 2) Create the new dated folder
                Report($"{siteTag}: Updating Production - {newPath}");
                CreateSiteDirectory(newPath);

                // 3) Copy preserved files from BASE → new tag (so we can safely purge base)
                CopyPreservedFiles(basePath, newPath, ProdPreserveRelative);

                // 4) Stage & extract new build into new tag (no-overwrite), then flatten PBK/DBK wrapper
                string staged = Path.Combine(newPath, Path.GetFileName(newBuildArchivePath));
                File.Copy(newBuildArchivePath, staged, overwrite: true);
                ExtractArchiveSkipOverwrite(staged, newPath);
                MergeFlattenTopFolderNoOverwrite(newPath);

                // 5) If config.json still missing, seed softly from the build
                string newCfg = Path.Combine(newPath, @"app\environments\config.json");
                if (!File.Exists(newCfg))
                    TrySeedSingleConfigFromArchive(newBuildArchivePath, newPath, onProgress);

                // 6) Delete staged archive
                try { File.Delete(staged); } catch { /* ignore */ }

                // 7) Now purge the BASE, keeping the backup zip and the NEW TAG
                //    (webconfigbackup is auto-protected via IsProtectedDir)
                var keep = new List<string>();
                var backupRel = Path.GetFileName(backupZip);
                if (!string.IsNullOrEmpty(backupRel)) keep.Add(backupRel);
                keep.Add(newFolder); // <- preserve the entire new tag directory
                DeleteAllInsideExcept(basePath, keep);

                // 8) Keep only most recent backup zip for this site
                CleanupBackupsKeepMostRecent(basePath, state, client);

                // 9) Web backup & SSRS key update against the NEW tag
                WebBackup(basePath, state, client, newFolder, () => DateTime.Now.ToString("MMddyyyy"), onProgress);
                UpdateSSRSKey(basePath, state, client, prevFolder: "base", newFolder, onProgress);

                Report($"{siteTag}: Production Update Complete.");
            }
            // ===== Dated → dated update=====
            else
            {
                string prevPath = prevPathTagged;

                // 1) Backup previous dated folder
                string backupZip = MakeProdBackupZipPath(basePath, state, client, prevFolder);
                Report($"{siteTag}: Creating Backup - {backupZip}");
                CreateZipArchive(prevPath, backupZip);

                // 2) Rename previous folder to temp_* and operate on that
                string tempPrev = MakeUniqueTempFolder(basePath, "temp_" + DateTime.Now.ToString("MMddyyyy_HHmmss"));
                Directory.Move(prevPath, tempPrev);
                prevPath = tempPrev;

                // 3) Create new dated folder and copy preserved files from temp → new
                Report($"{siteTag}: Updating Production - {newPath}");
                CreateSiteDirectory(newPath);

                DeleteAllInsideExcept(prevPath, ProdPreserveRelative);
                var srcWeb = Path.Combine(prevPath, "web.config");
                var srcCfg = Path.Combine(prevPath, @"app\environments\config.json");
                if (!File.Exists(srcWeb))
                    Report($"{siteTag}[Preserve]: web.config not found in previous build (continuing).");
                if (!File.Exists(srcCfg))
                    Report($"{siteTag}[Config]: Missing preserved file - {srcCfg}");

                CopyPreservedFiles(prevPath, newPath, ProdPreserveRelative);

                // 4) Stage & extract new build into new tag (no-overwrite), then flatten PBK/DBK wrapper
                string staged = Path.Combine(newPath, Path.GetFileName(newBuildArchivePath));
                File.Copy(newBuildArchivePath, staged, overwrite: true);
                ExtractArchiveSkipOverwrite(staged, newPath);
                MergeFlattenTopFolderNoOverwrite(newPath);

                // 5) Ensure preserved from temp are final (self-copy guarded)
                ForceCopyPreservedFiles(prevPath, newPath);

                // 6) Seed config.json from archive if still missing (soft)
                string newCfg = Path.Combine(newPath, @"app\environments\config.json");
                if (!File.Exists(newCfg))
                    TrySeedSingleConfigFromArchive(newBuildArchivePath, newPath, onProgress);

                // 7) Cleanup temp prev and staged zip
                TryDeleteDirRecursive(prevPath);
                try { File.Delete(staged); } catch { /* ignore */ }

                // 8) Keep only the most recent backup zip
                CleanupBackupsKeepMostRecent(basePath, state, client);

                // 9) Web backup (new tag) & SSRS key update
                WebBackup(basePath, state, client, newFolder, () => DateTime.Now.ToString("MMddyyyy"), onProgress);
                UpdateSSRSKey(basePath, state, client, prevFolder, newFolder, onProgress);

                Report($"{siteTag}: Production Update Complete.");
            }
        }

        // =====================================================================
        //                           EXTERNALS
        // =====================================================================

        public static void DeployExternal_Update(
            string resolvedRoot,     // may be a flat role folder or an external root
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

            // Normalize IIS-derived path + pick an "external base" for flat detection
            var baseFromIis = Path.GetFullPath(
                resolvedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // If the last segment starts with the siteTag then treat its parent as the external container
            // Otherwise use the path as-is.
            string lastSegment = Path.GetFileName(baseFromIis);
            string externalBase =
                lastSegment.StartsWith(siteTag, StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(baseFromIis) is string parent
                    ? parent
                    : baseFromIis;

            Report($"{siteTag}: External Base resolved → {externalBase}");

            string flatPrefix = siteTag;  

            // Legacy flat folders directly under the external base:
            string flatCis = Path.Combine(externalBase, flatPrefix + "CaseInfoSearch");
            string flatEsu = Path.Combine(externalBase, flatPrefix + "eSubpoena");
            string flatDA = Path.Combine(externalBase, flatPrefix + "DataAccess");
            string flatPBK = Path.Combine(externalBase, flatPrefix + "PBKDataAccess");

            bool hasFlatCis = Directory.Exists(flatCis);
            bool hasFlatEsu = Directory.Exists(flatEsu);
            bool hasFlatDA = Directory.Exists(flatDA);
            bool hasFlatPBK = Directory.Exists(flatPBK);

            // "Flat" means we actually have at least one AZDifferent{Role} folder in the external base.
            bool flatMode = hasFlatCis || hasFlatEsu || hasFlatDA || hasFlatPBK;

            string externalRoot;
            string cisPath;
            string esPath;
            string daPath;
            string backupRoot;
            string daFolder;

            // FLAT → CANONICAL MIGRATION
            if (flatMode)
            {
                daFolder = hasFlatPBK ? "PBKDataAccess" : "DataAccess";

                // Decide the canonical client root under the detected external base:
                bool baseLooksExternal =
                    externalBase.IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0;

                string clientRoot = baseLooksExternal
                    ? Path.Combine(externalBase, siteTag)
                    : Path.Combine(externalBase, state, "PbkExternal", siteTag);

                string cisRoot = Path.Combine(clientRoot, "CaseInfoSearch");
                string esRoot = Path.Combine(clientRoot, "eSubpoena");
                string daRoot = Path.Combine(clientRoot, daFolder);

                // Tell the user what is about to happen (before we move anything)
                Report($"{siteTag}: Detected flat external layout. Migrating to '{clientRoot}'. Legacy folders suffixed with '_old_{backupTag}'.");

                Directory.CreateDirectory(clientRoot);
                if (hasFlatCis) Directory.CreateDirectory(cisRoot);
                if (hasFlatEsu) Directory.CreateDirectory(esRoot);
                if (hasFlatDA || hasFlatPBK) Directory.CreateDirectory(daRoot);

                // Copy legacy content into the normalized layout (no overwrite).
                if (hasFlatCis) CopyDirectoryContents(flatCis, cisRoot);
                if (hasFlatEsu) CopyDirectoryContents(flatEsu, esRoot);
                if (hasFlatDA || hasFlatPBK)
                {
                    string flatDaSource = hasFlatPBK ? flatPBK : flatDA;
                    CopyDirectoryContents(flatDaSource, daRoot);
                }

                // Rename legacy flat folders to *_old_<tag> so the user can later clean them up.
                void TryRenameLegacy(string path)
                {
                    if (!Directory.Exists(path)) return;
                    string tagged = path + "_old_" + backupTag;
                    try
                    {
                        Directory.Move(path, tagged);
                        // Intentionally no per-folder log to keep output clean.
                    }
                    catch (Exception ex)
                    {
                        Report($"{siteTag}[External]: Failed to rename legacy folder '{path}' → '{tagged}': {ex.Message}");
                    }
                }

                TryRenameLegacy(flatCis);
                TryRenameLegacy(flatEsu);
                TryRenameLegacy(flatDA);
                TryRenameLegacy(flatPBK);

                externalRoot = clientRoot;
                cisPath = cisRoot;
                esPath = esRoot;
                daPath = daRoot;
                backupRoot = clientRoot;
            }
            else
            {
                // -----------------------------------------------------------------
                //   NON-FLAT: already under an external root or config-root style.
                //     - Handles PbkExternal layouts
                //     - Handles misnamed client roots 
                // -----------------------------------------------------------------
                externalRoot = baseFromIis;

                string? parentDir = Path.GetDirectoryName(externalRoot);
                string parentName = Path.GetFileName(parentDir ?? string.Empty);
                bool parentLooksExternal = parentDir != null &&
                                           parentName.IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0;

                if (parentLooksExternal)
                {
                    // We are under some *External* container (e.g. ...\KY\PbkExternal\KYFartsmouth OR ...\KY\PbkExternal\Fartsmouth).
                    string last = Path.GetFileName(externalRoot);

                    if (!string.Equals(last, siteTag, StringComparison.OrdinalIgnoreCase))
                    {
                        // Misnamed client root (Fartsmouth vs KYFartsmouth). Migrate to canonical name.
                        string canonicalRoot = Path.Combine(parentDir!, siteTag);
                        Directory.CreateDirectory(canonicalRoot);

                        CopyDirectoryContents(externalRoot, canonicalRoot);

                        string oldName = externalRoot + "_old_" + backupTag;
                        try
                        {
                            Directory.Move(externalRoot, oldName);
                            Report($"{siteTag}: Detected misnamed external root '{externalRoot}'. Migrated to '{canonicalRoot}'. Old folder renamed to '{oldName}'.");
                        }
                        catch (Exception ex)
                        {
                            Report($"{siteTag}[External]: Failed to rename old external root '{externalRoot}' → '{oldName}': {ex.Message}");
                        }

                        externalRoot = canonicalRoot;
                    }
                    // else: already canonical, nothing to migrate
                }
                else
                {
                    // Fallback
                    externalRoot = ResolveExternalRoot(
                        state,
                        client,
                        AppConfigManager.Config.DeploymentFlavor ?? "prod",
                        resolvedRoot);
                }

                daFolder = Directory.Exists(Path.Combine(externalRoot, "PBKDataAccess"))
                    ? "PBKDataAccess"
                    : "DataAccess";

                cisPath = Path.Combine(externalRoot, "CaseInfoSearch");
                esPath = Path.Combine(externalRoot, "eSubpoena");
                daPath = Path.Combine(externalRoot, daFolder);
                backupRoot = externalRoot;
            }

            if (!string.IsNullOrWhiteSpace(caseInfoSearchZip)) ValidateBuildName(caseInfoSearchZip, "caseinfosearch");
            if (!string.IsNullOrWhiteSpace(esubpoenaZip)) ValidateBuildName(esubpoenaZip, "esubpoena");
            if (!string.IsNullOrWhiteSpace(dataAccessZip)) ValidateBuildName(dataAccessZip, "dataaccess");

            // Backup tag directory under the per-client backup root
            string backupDir = GetExternalBackupDir(backupRoot, backupTag);
            Report($"{siteTag}: Creating External Backup - {backupDir}.");

            // Backups
            if (!string.IsNullOrWhiteSpace(caseInfoSearchZip))
                TryBackupFolder(cisPath, Path.Combine(backupDir, "CaseInfoSearch.zip"), Report);

            if (!string.IsNullOrWhiteSpace(esubpoenaZip))
                TryBackupFolder(esPath, Path.Combine(backupDir, "eSubpoena.zip"), Report);

            if (!string.IsNullOrWhiteSpace(dataAccessZip))
                TryBackupFolder(daPath, Path.Combine(backupDir, daFolder + ".zip"), Report);

            // Deploy roles 
            DeployRoleIfProvided("CaseInfoSearch", caseInfoSearchZip, cisPath);
            DeployRoleIfProvided("eSubpoena", esubpoenaZip, esPath);
            DeployRoleIfProvided(daFolder, dataAccessZip, daPath);

            //Repoint IIS app pools
            RepointExternalPoolsIfPresent(state, client, cisPath, esPath, daPath, daFolder, Report);

            // Clean old backups 
            KeepOnlyMostRecentBackup(backupRoot, backupTag, Report);

            Report($"{siteTag}: External Update(s) Complete.");

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

                // Post-deploy rewrites (CIS/eSub only)
                try
                {
                    if (roleDisplayName.Equals("CaseInfoSearch", StringComparison.OrdinalIgnoreCase) ||
                        roleDisplayName.Equals("eSubpoena", StringComparison.OrdinalIgnoreCase))
                    {
                        string siteTagLocal = $"{state}{client}";
                        UpdateExternalIndexBaseHref(rolePath, siteTagLocal, Report, roleDisplayName);
                        UpdateExternalRewriteAction(rolePath, siteTagLocal, Report, roleDisplayName);
                    }
                }
                catch (Exception ex)
                {
                    Report($"{state}{client}[{roleDisplayName}][ERROR]: Post-deploy rewrites failed: {ex.Message}");
                }
            }

            static void TryBackupFolder(string sourceDir, string archivePath, Action<string> log)
            {
                if (!Directory.Exists(sourceDir)) { log($"[Backup] Skip: '{sourceDir}' not found."); return; }
                if (!Directory.EnumerateFileSystemEntries(sourceDir).Any()) { log($"[Backup] Skip: '{sourceDir}' empty."); return; }

                Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
                CreateZipArchive(sourceDir, archivePath);
            }

            static void CopyDirectoryContents(string src, string dst)
            {
                if (!Directory.Exists(src)) return;

                // Create all subdirectories
                foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(src, dir);
                    Directory.CreateDirectory(Path.Combine(dst, rel));
                }

                // Copy files, but do NOT overwrite existing files in the new layout
                foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(src, file);
                    var target = Path.Combine(dst, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                    if (!File.Exists(target))
                    {
                        File.Copy(file, target, overwrite: false);
                    }
                }
            }

            static void RepointExternalPoolsIfPresent(
                string stateLocal,
                string clientLocal,
                string cisRootLocal,
                string esRootLocal,
                string daRootLocal,
                string daFolderLocal,
                Action<string> log)
            {
                string siteTagLocal = stateLocal + clientLocal;

                void TryRepointPool(string poolSuffix, string targetPath)
                {
                    if (string.IsNullOrWhiteSpace(targetPath)) return;
                    if (!Directory.Exists(targetPath)) return;

                    string poolName = siteTagLocal + poolSuffix;
                    try
                    {
                        IISManager.IisRepoint.TryRepointAppForPoolToPath(poolName, targetPath, log);
                    }
                    catch (Exception ex)
                    {
                        log($"{poolName}[IIS][ERROR]: Failed to repoint to '{targetPath}': {ex.Message}");
                    }
                }

                // Only repoint to folders that actually exist.
                TryRepointPool("CaseInfoSearch", cisRootLocal);
                TryRepointPool("eSubpoena", esRootLocal);
                TryRepointPool(daFolderLocal, daRootLocal);
            }
        }

        // =====================================================================
        //                       CONFIG CHANGES
        // =====================================================================
        public static void WebBackup(
            string prodBasePathOrRoot,    // pass IIS prod base path
            string state,
            string client,
            string newFolder,
            Func<string> todayTagProvider,
            Action<string>? onProgress = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(prodBasePathOrRoot)) throw new ArgumentException(nameof(prodBasePathOrRoot));
                if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException(nameof(state));
                if (string.IsNullOrWhiteSpace(client)) throw new ArgumentException(nameof(client));
                if (string.IsNullOrWhiteSpace(newFolder)) throw new ArgumentException(nameof(newFolder));

                string prodBasePath = Path.GetFullPath(prodBasePathOrRoot);
                string configPath = Path.Combine(prodBasePath, newFolder, "web.config");
                if (!File.Exists(configPath))
                {
                    onProgress?.Invoke($"{state}{client}[Web]: web.config not found for backup at '{configPath}' - skipped.");
                    return;
                }
                string backupDir = Path.Combine(prodBasePath, "webconfigbackup");
                Directory.CreateDirectory(backupDir);

                string backupPath = Path.Combine(backupDir, $"web_backup_{todayTagProvider()}.config");
                File.Copy(configPath, backupPath, overwrite: true);

                onProgress?.Invoke($"{state}{client}[Web]: Backup created - {backupPath}");
            }
            catch (Exception ex)
            {
                onProgress?.Invoke($"[ERROR] BackupWebConfigForTag failed: {ex.Message}");
            }
        }

        public static void UpdateSSRSKey(
            string prodBasePathOrRoot,    // pass IIS prod base path
            string state,
            string client,
            string prevFolder,   
            string newFolder,
            Action<string>? onProgress = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(prodBasePathOrRoot)) throw new ArgumentException(nameof(prodBasePathOrRoot));
                if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException(nameof(state));
                if (string.IsNullOrWhiteSpace(client)) throw new ArgumentException(nameof(client));
                if (string.IsNullOrWhiteSpace(newFolder)) throw new ArgumentException(nameof(newFolder));

                string siteTag = $"{state}{client}";
                string basePath = Path.GetFullPath(prodBasePathOrRoot);

                // 1) Pick target web.config
                string primary = Path.Combine(basePath, newFolder, "web.config");
                string legacy = Path.Combine(basePath, "web.config");

                string? targetConfig = File.Exists(primary) ? primary
                                      : File.Exists(legacy) ? legacy
                                      : null;

                if (targetConfig is null)
                {
                    onProgress?.Invoke($"{siteTag}[Web]: web.config not found at '{primary}' or '{legacy}' — skipped.");
                    return;
                }

                // 2) Detect existing Reports dir under the new tag 
                string reportsDirName = Directory.Exists(Path.Combine(basePath, newFolder, "Reports")) ? "Reports"
                                    : Directory.Exists(Path.Combine(basePath, newFolder, "reports")) ? "reports"
                                    : string.Empty;

                if (string.IsNullOrEmpty(reportsDirName))
                {
                    onProgress?.Invoke($"{siteTag}[Web]: Reports folder not found under '{Path.Combine(basePath, newFolder)}' — skipped.");
                    return;
                }

                // 3) Build final path 
                string targetReportsPath = Path.Combine(basePath, newFolder, reportsDirName);
                if (!targetReportsPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    targetReportsPath += Path.DirectorySeparatorChar;

                string configValue = targetReportsPath.Replace(@"\", @"\\"); // <-- preserve \\ formatting

                var doc = XDocument.Load(targetConfig, LoadOptions.PreserveWhitespace);
                var appSettings = doc.Root?.Element("appSettings");
                if (appSettings == null)
                {
                    onProgress?.Invoke($"{siteTag}[Web]: appSettings missing - skipped.");
                    return;
                }

                var target = appSettings.Elements("add")
                    .FirstOrDefault(e => string.Equals((string?)e.Attribute("key"), "RSLocalReportFolder", StringComparison.OrdinalIgnoreCase));

                if (target == null)
                {
                    onProgress?.Invoke($"{siteTag}[Web]: RSLocalReportFolder key not found - skipped.");
                    return;
                }

                target.SetAttributeValue("value", configValue);
                doc.Save(targetConfig);

                onProgress?.Invoke($"{siteTag}[Web]: Key Modified - <add key=\"RSLocalReportFolder\" value=\"{configValue}\" />");
            }
            catch (Exception ex)
            {
                onProgress?.Invoke($"[ERROR] UpdateSSRSKey failed: {ex.Message}");
            }
        }

        public static void UpdateExternalIndexBaseHref(string rolePath, string siteTag, Action<string>? log, string roleDisplay)
        {
            // rolePath: ...\CaseInfoSearch or ...\eSubpoena
            try
            {
                string indexPath = Path.Combine(rolePath, @"app\index.html");
                if (!File.Exists(indexPath)) { log?.Invoke($"{siteTag}{roleDisplay}[Skip]: app\\index.html not found."); return; }

                string html = File.ReadAllText(indexPath);
                string original = html;

                string appName = rolePath.EndsWith("CaseInfoSearch", StringComparison.OrdinalIgnoreCase) ? "CaseInfoSearch" : "eSubpoena";

                var rx = new Regex($@"(<base\s+href=""/){appName}[^/""]*(/app/""\s*>)",
                                   RegexOptions.IgnoreCase);

                if (!rx.IsMatch(html))
                {
                    log?.Invoke($"{siteTag}{roleDisplay}[Skip]: Expected '<base href=\"/{appName}*/app/\">' not found");
                    return;
                }

                html = rx.Replace(html, $"$1{siteTag}{appName}$2", 1);

                if (string.Equals(html, original, StringComparison.Ordinal))
                {
                    log?.Invoke($"{siteTag}{roleDisplay}[Warning]: app\\index.html unchanged.");
                    return;
                }

                File.WriteAllText(indexPath, html);
            }
            catch (Exception ex)
            {
                log?.Invoke($"{siteTag}{roleDisplay}[Error]: index.html update failed: {ex.Message}");
            }
        }

        public static void UpdateExternalRewriteAction(string rolePath, string siteTag, Action<string>? log, string roleDisplay)
        {
            try
            {
                string cfgPath = Path.Combine(rolePath, @"app\web.config");
                if (!File.Exists(cfgPath)) { log?.Invoke($"{siteTag}{roleDisplay}[Skip]: app\\web.config not found."); return; }

                var doc = XDocument.Load(cfgPath, LoadOptions.PreserveWhitespace);

                var actions = doc.Descendants()
                                 .Where(e => string.Equals(e.Name.LocalName, "action", StringComparison.OrdinalIgnoreCase))
                                 .ToList();

                if (actions.Count == 0)
                {
                    log?.Invoke($"{siteTag}{roleDisplay}[Skip]: No <action> elements found in app\\web.config.");
                    return;
                }

                bool isCis = rolePath.EndsWith("CaseInfoSearch", StringComparison.OrdinalIgnoreCase);
                string appName = isCis ? "CaseInfoSearch" : "eSubpoena";
                string desiredUrl = $"/{siteTag}{appName}/app/";

                var target = actions.FirstOrDefault(a =>
                {
                    var urlAttr = a.Attributes().FirstOrDefault(x => string.Equals(x.Name.LocalName, "url", StringComparison.OrdinalIgnoreCase));
                    if (urlAttr is null) return false;
                    var v = urlAttr.Value ?? string.Empty;
                    return v.StartsWith($"/{appName}", StringComparison.OrdinalIgnoreCase)
                           || v.StartsWith($"/{siteTag}{appName}", StringComparison.OrdinalIgnoreCase);
                });

                if (target == null)
                {
                    var expected = isCis ? "/CaseInfoSearch*/(maybe /app/)" : "/eSubpoena*/(maybe /app/)";
                    log?.Invoke($"{siteTag}{roleDisplay}[Skip]: Expected <action ... url=\"{expected}\"> not found.");
                    return;
                }

                var url = target.Attributes().First(x => string.Equals(x.Name.LocalName, "url", StringComparison.OrdinalIgnoreCase));
                if (string.Equals(url.Value, desiredUrl, StringComparison.Ordinal))
                {
                    log?.Invoke($"{siteTag}{roleDisplay}: app\\web.config unchanged.");
                    return;
                }

                url.Value = desiredUrl;
                doc.Save(cfgPath);
            }
            catch (Exception ex)
            {
                log?.Invoke($"{siteTag}{roleDisplay}[Error]: web.config update failed: {ex.Message}");
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
                catch (FileNotFoundException) { return; }
                catch (DirectoryNotFoundException) { return; }
                catch (PathTooLongException) { return; }
            }

            try
            {
                File.Copy(src, dst, overwrite: false);
            }
            catch { }
        }

        private static void DeleteAllInsideExcept(string root, IEnumerable<string> relKeep)
        {
            // Normalize keep paths (full) relative to root
            var keepFullPaths = new HashSet<string>(
                relKeep.Select(r => Path.GetFullPath(Path.Combine(root, r))),
                StringComparer.OrdinalIgnoreCase);

            // Split into files vs directories that actually exist right now
            var keepDirs = new HashSet<string>(
                keepFullPaths.Where(Directory.Exists),
                StringComparer.OrdinalIgnoreCase);

            var keepFiles = new HashSet<string>(
                keepFullPaths.Where(File.Exists),
                StringComparer.OrdinalIgnoreCase);

            // 1) Delete files not kept AND not under any kept directory
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(f);

                // skip files that are explicitly kept
                if (keepFiles.Contains(full))
                    continue;

                // skip any file that lives under a kept directory
                if (keepDirs.Any(dir => full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    continue;

                TryDeleteFile(full);
            }

            // 2) Delete directories that are not protected and do not equal/contain any kept path
            var allDirs = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(p => p.Length);

            foreach (var d in allDirs)
            {
                if (IsProtectedDir(d))
                    continue;

                var dirFull = Path.GetFullPath(d);

                // do not delete a directory that is itself kept
                if (keepDirs.Contains(dirFull))
                    continue;

                // do not delete ancestors of kept paths
                bool containsKept = keepFullPaths.Any(k =>
                    k.StartsWith(dirFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                if (!containsKept)
                    TryDeleteDir(dirFull);
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

                if (!File.Exists(src))
                    return;

                File.Copy(src, dst, overwrite: true);
            }
            catch (FileNotFoundException)
            { }
            catch (DirectoryNotFoundException)
            { }
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
    }
}
