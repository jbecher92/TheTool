using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace TheTool
{
    public static class FileManager
    {
        private static readonly string[] PreserveFiles = { "web.config", "appsettings.json" };

        public static void ApplyBuild(string siteName, string buildSourceDir, bool isProd)
        {
            string baseWebRoot = @"F:\\inetpub\\wwwroot";
            string targetDir = Path.Combine(baseWebRoot, siteName);

            if (!Directory.Exists(targetDir))
                throw new DirectoryNotFoundException($"Site directory not found: {targetDir}");

            string buildType = isProd ? "PROD" : "EXT";
            Console.WriteLine($"Applying {buildType} build to: {siteName}");
            Console.WriteLine($"From: {buildSourceDir}");

            UpdateClientDirectory(targetDir, buildSourceDir);
        }

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

        private static void CreateZipArchive(string sourceDir, string zipPath)
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(sourceDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }

        private static void CleanDirectoryExceptConfig(string dir)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                string fileName = Path.GetFileName(file);
                if (!PreserveFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    File.Delete(file);
            }

            foreach (string subDir in Directory.GetDirectories(dir))
                Directory.Delete(subDir, true);
        }

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
    }
}
