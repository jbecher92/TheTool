using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace TheTool
{
    public sealed class AppConfig
    {
        public string? DeploymentFlavor { get; init; }
        public string? SitesRootPath { get; init; }
        public string? SitesDrive { get; init; }
        public string? ValidatePath { get; init; }
        public string? ValidateDrive { get; init; }
        public string? InternalPath { get; init; }
        public string? InternalDrive { get; init; }

        public string GetSitesRootPath()
        {
            if (!string.IsNullOrWhiteSpace(SitesRootPath))
                return Path.GetFullPath(SitesRootPath);

            if (!string.IsNullOrWhiteSpace(SitesDrive))
                return Path.GetFullPath(SitesDrive);

            throw new InvalidOperationException("SitesRootPath or SitesDrive must be set in config.");
        }

        public string GetValidatePath()
        {
            if (!string.IsNullOrWhiteSpace(ValidatePath))
                return Path.GetFullPath(ValidatePath);

            if (!string.IsNullOrWhiteSpace(ValidateDrive))
                return Path.GetFullPath(ValidateDrive);

            throw new InvalidOperationException("ValidatePath or ValidateDrive must be set in config.");
        }

        public string GetInternalPath()
        {
            if (!string.IsNullOrWhiteSpace(InternalPath))
                return Path.GetFullPath(InternalPath);

            if (!string.IsNullOrWhiteSpace(InternalDrive))
                return Path.GetFullPath(InternalDrive);

            throw new InvalidOperationException("InternalPath or InternalDrive must be set in config.");
        }
    }

    internal static class AppConfigManager
    {
        private const string FileName = "config.json";
        private static readonly string ExeConfigPath = Path.Combine(AppContext.BaseDirectory, FileName);

        private static AppConfig? _cfg;

        public static AppConfig Config => _cfg ??= Load();

        private static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ExeConfigPath))
                    throw new FileNotFoundException($"Config file not found: {ExeConfigPath}");

                var json = File.ReadAllText(ExeConfigPath);
                var opts = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true
                };

                var cfg = JsonSerializer.Deserialize<AppConfig>(json, opts) ?? new AppConfig();

                ValidateDeploymentFlavor(cfg.DeploymentFlavor);

                return cfg;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading config.json:\n{ex.Message}",
                    "Config Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
                return new AppConfig(); // unreachable
            }
        }

        private static void ValidateDeploymentFlavor(string? flavor)
        {
            var validFlavors = new[] { "prod", "test", "internal" };
            if (string.IsNullOrWhiteSpace(flavor) || !validFlavors.Contains(flavor.Trim().ToLowerInvariant()))
            {
                MessageBox.Show(
                    $"Invalid DeploymentFlavor: '{flavor}'. Must be 'prod', 'test', or 'internal'.\nThe program will now exit.",
                    "Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }
    }

    //internal static class AppPaths
    //{
    //    public static string SitesRoot => AppConfigManager.Config.GetSitesRootPath();
    //    public static string ValidateRoot => AppConfigManager.Config.GetValidatePath();
    //    public static string InternalRoot => AppConfigManager.Config.GetInternalPath();
    //}
}
