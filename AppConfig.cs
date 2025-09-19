using System;
using System.IO;
using System.Text.Json;

namespace TheTool
{
    public sealed class AppConfig
    {
        public string? SitesRootPath { get; init; }
        public string? SitesDrive { get; init; } // e.g., "F:"
    }

    internal static class AppConfigManager
    {
        private const string FileName = "config.json";
        private static readonly string ExeDir = AppContext.BaseDirectory;
        private static readonly string ExeConfigPath = Path.Combine(ExeDir, FileName);

        private static AppConfig _cfg;

        public static AppConfig Config => _cfg ??= Load();

        public static string EnsureSitesRootOrThrow()
        {
            var cfg = Config;

            string path = null;
            if (!string.IsNullOrWhiteSpace(cfg?.SitesRootPath))
                path = cfg.SitesRootPath;
            else if (!string.IsNullOrWhiteSpace(cfg?.SitesDrive))
                path = Path.Combine(cfg.SitesDrive, "inetpub", "wwwroot");

            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException(
                    $"Missing config. Add 'SitesRootPath' or 'SitesDrive' in {ExeConfigPath}");

            // Normalize and validate
            path = Path.GetFullPath(path);
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Sites root not found: {path}");

            return path;
        }

        private static AppConfig Load()
        {
            if (!File.Exists(ExeConfigPath))
                return new AppConfig(); // no defaults; forces explicit config

            var json = File.ReadAllText(ExeConfigPath);
            var opts = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            return JsonSerializer.Deserialize<AppConfig>(json, opts) ?? new AppConfig();
        }
    }

    internal static class AppPaths
    {
        public static string SitesRoot => AppConfigManager.EnsureSitesRootOrThrow();
    }
}
