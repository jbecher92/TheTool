using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TheTool
{
    public static partial class IISManager
    {
        // ---------------------------
        // Discovery
        // ---------------------------
        public static class IisRepoint
        {
            public static bool TryRepointAppForPoolToPath(
                string appPoolName,
                string targetPhysicalPath,
                Action<string>? log)
            {
                if (string.IsNullOrWhiteSpace(appPoolName) ||
                    string.IsNullOrWhiteSpace(targetPhysicalPath))
                {
                    log?.Invoke($"{appPoolName}[IIS][FATAL]: invalid repoint arguments.");
                    return false;
                }

                var targetFull = Path.GetFullPath(
                    targetPhysicalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                if (!Directory.Exists(targetFull))
                {
                    log?.Invoke($"{appPoolName}[IIS][FATAL]: target folder does not exist: '{targetFull}'. Refusing to repoint.");
                    return false;
                }

                using var sm = new ServerManager();

                var app = sm.Sites
                    .SelectMany(s => s.Applications)
                    .FirstOrDefault(a => string.Equals(a.ApplicationPoolName, appPoolName, StringComparison.OrdinalIgnoreCase));

                if (app == null)
                {
                    if (!appPoolName.EndsWith("PBKDataAccess", StringComparison.OrdinalIgnoreCase))
                    {
                        log?.Invoke($"{appPoolName}[IIS]: No application found using this app pool. Skipping repoint.");
                    }
                    return false;
                }

                var vdir = app.VirtualDirectories["/"];
                if (vdir == null)
                {
                    log?.Invoke($"{appPoolName}[IIS][FATAL]: Root virtual directory not found. Refusing to repoint.");
                    return false;
                }

                var current = (vdir.PhysicalPath ?? string.Empty)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var currentFull = Path.GetFullPath(current);

                if (string.Equals(currentFull, targetFull, StringComparison.OrdinalIgnoreCase))
                {
                    // Already pointing at the canonical folder
                    return true;
                }

                // Basic safety: do not hop drives
                var currentRoot = Path.GetPathRoot(currentFull);
                var targetRoot = Path.GetPathRoot(targetFull);
                if (!string.IsNullOrEmpty(currentRoot) &&
                    !string.IsNullOrEmpty(targetRoot) &&
                    !string.Equals(currentRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke($"{appPoolName}[IIS][FATAL]: refusing to change drive from '{currentRoot}' to '{targetRoot}'.");
                    return false;
                }

                vdir.PhysicalPath = targetFull;
                sm.CommitChanges();

                return true;
            }
        }


        public static class IisReadHelpers
        {
            
            public static bool TryGetPhysicalPathForPool(string appPoolName, out string physicalPath)
            {
                physicalPath = string.Empty;
                if (string.IsNullOrWhiteSpace(appPoolName)) return false;

                using var sm = new ServerManager();
                foreach (var site in sm.Sites)
                {
                    foreach (var app in site.Applications)
                    {
                        if (!string.Equals(app.ApplicationPoolName, appPoolName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Root vdir of this application
                        var vdir = app.VirtualDirectories["/"];
                        var path = vdir?.PhysicalPath;
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            physicalPath = path;
                            return true;
                        }
                    }
                }
                return false;
            }
            //Used for creates to view standard file pathing
            public static bool TryGetFirstPhysicalPath(IEnumerable<string> appPoolNames, out string physicalPath)
            {
                physicalPath = string.Empty;
                if (appPoolNames is null) return false;
                foreach (var name in appPoolNames)
                {
                    if (TryGetPhysicalPathForPool(name, out physicalPath))
                        return true;
                }
                return false;
            }
        }

        public static List<string> GetIISAppPools(string? serverName = null)
        {
            using var mgr = string.IsNullOrWhiteSpace(serverName)
                ? new ServerManager()
                : ServerManager.OpenRemote(serverName);

            return mgr.ApplicationPools
                      .Select(ap => ap.Name)
                      .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                      .ToList();
        }

        // ---------------------------
        // Core create/update helpers
        // ---------------------------

        public static void EnsureAppPool(
            string appPoolName,
            string managedRuntimeVersion = "v4.0",
            ManagedPipelineMode pipeline = ManagedPipelineMode.Integrated,
            bool enable32BitOnWin64 = false,
            bool autoStart = true)
        {
            if (string.IsNullOrWhiteSpace(appPoolName))
                throw new ArgumentException("appPoolName required", nameof(appPoolName));

            using var sm = new ServerManager();
            var pool = sm.ApplicationPools.FirstOrDefault(p =>
                           p.Name.Equals(appPoolName, StringComparison.OrdinalIgnoreCase))
                       ?? sm.ApplicationPools.Add(appPoolName);

            pool.ManagedRuntimeVersion = managedRuntimeVersion;
            pool.ManagedPipelineMode = pipeline;
            pool.Enable32BitAppOnWin64 = enable32BitOnWin64;
            pool.AutoStart = autoStart;

            sm.CommitChanges();
        }

        public static void EnsureAppUnderDefault(
            string appSegmentName,
            string physicalPath,
            string appPoolName,
            bool stopStartPool = false,
            int poolTimeoutMs = 60000)
        {
            if (string.IsNullOrWhiteSpace(appSegmentName)) throw new ArgumentException("appSegmentName required");
            if (string.IsNullOrWhiteSpace(physicalPath)) throw new ArgumentException("physicalPath required");
            if (string.IsNullOrWhiteSpace(appPoolName)) throw new ArgumentException("appPoolName required");

            EnsureAppPool(appPoolName);

            using var sm = new ServerManager();
            var defaultSite = sm.Sites.FirstOrDefault(s =>
                s.Name.Equals("Default Web Site", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Default Web Site not found.");

            var appPath = "/" + appSegmentName;
            var app = defaultSite.Applications[appPath] ?? defaultSite.Applications.Add(appPath, physicalPath);
            app.ApplicationPoolName = appPoolName;

            var vdir = app.VirtualDirectories["/"] ?? app.VirtualDirectories.Add("/", physicalPath);
            vdir.PhysicalPath = physicalPath;

            sm.CommitChanges();

            if (!stopStartPool) return;

            // Optional stop/start (rarely needed for just remapping)
            var pool = sm.ApplicationPools.FirstOrDefault(p =>
                p.Name.Equals(appPoolName, StringComparison.OrdinalIgnoreCase));
            if (pool == null) throw new InvalidOperationException($"App pool '{appPoolName}' not found after commit.");

            if (pool.State == ObjectState.Started || pool.State == ObjectState.Starting)
            {
                pool.Stop(); sm.CommitChanges();
                WaitForPoolState(sm, pool.Name, ObjectState.Stopped, poolTimeoutMs);
            }

            pool.Start(); sm.CommitChanges();
            WaitForPoolState(sm, pool.Name, ObjectState.Started, poolTimeoutMs);
        }

        // ---------------------------
        // Pool state helpers
        // ---------------------------

        private static void WaitForPoolState(ServerManager sm, string poolName, ObjectState desired, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var pool = sm.ApplicationPools.FirstOrDefault(p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidOperationException($"App pool '{poolName}' not found while waiting for state {desired}.");
                if (pool.State == desired) return;
                Thread.Sleep(200);
            }
            throw new TimeoutException($"App pool '{poolName}' did not reach state {desired} within {timeoutMs}ms.");
        }

        private static async Task WaitForPoolStateAsync(ServerManager sm, string poolName, ObjectState desired, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var pool = sm.ApplicationPools.FirstOrDefault(p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidOperationException($"App pool '{poolName}' not found while waiting for state {desired}.");
                if (pool.State == desired) return;
                await Task.Delay(200).ConfigureAwait(false);
            }
            throw new TimeoutException($"App pool '{poolName}' did not reach state {desired} within {timeoutMs} ms.");
        }


        public static async Task RunWithPoolsStoppedAsync(
            string[] poolNames,
            Func<Task> work,
            Action<string>? log = null,
            int timeoutMs = 60000)
        {
            if (work is null) throw new ArgumentNullException(nameof(work));

            var targets = (poolNames ?? Array.Empty<string>())
                          .Where(n => !string.IsNullOrWhiteSpace(n))
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .ToArray();

            var stoppedPools = new List<string>();
            var startedPools = new List<string>();

            using var sm = new ServerManager();

            // Stop targeted pools
            foreach (var name in targets)
            {
                var pool = sm.ApplicationPools.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (pool == null) continue;

                if (pool.State == ObjectState.Started || pool.State == ObjectState.Starting)
                {
                    pool.Stop(); sm.CommitChanges();
                    await WaitForPoolStateAsync(sm, name, ObjectState.Stopped, timeoutMs).ConfigureAwait(false);
                    stoppedPools.Add(name);
                }
            }

            if (stoppedPools?.Count > 0)
                LogPoolsByClient(stoppedPools, log, "App pools stopped:");

            Exception? workError = null;
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                workError = ex;
            }

            // Start targeted pools (sequential, slight stagger)
            foreach (var name in targets)
            {
                var pool = sm.ApplicationPools.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (pool == null) continue;

                if (pool.State == ObjectState.Stopped || pool.State == ObjectState.Stopping)
                {
                    pool.Start(); sm.CommitChanges();
                    await WaitForPoolStateAsync(sm, name, ObjectState.Started, timeoutMs).ConfigureAwait(false);
                    startedPools.Add(name);
                    await Task.Delay(150).ConfigureAwait(false); // small stagger
                }
            }

            if (startedPools?.Count > 0)
                LogPoolsByClient(startedPools, log, "App pools started:");

            if (workError is not null) throw workError;
        }

        static string BaseClientKey(string pool)
        {
            // strip known suffixes to get the STATE+CLIENT base
            string[] suffixes = { "CaseInfoSearch", "eSubpoena", "PBKDataAccess", "DataAccess" };
            foreach (var suf in suffixes)
                if (pool.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                    return pool[..^suf.Length];
            return pool; // Prod has no suffix
        }

        static string RoleForPool(string pool)
        {
            return pool;
        }

        static void LogPoolsByClient(IEnumerable<string> pools, Action<string>? log, string header)
        {
            log?.Invoke(header);

            foreach (var g in pools
                .GroupBy(p => BaseClientKey(p), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key))
            {
                var roles = g.Select(RoleForPool)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(r => r);
                log?.Invoke($" -{string.Join(", ", roles)}");
            }
        }
    }
}