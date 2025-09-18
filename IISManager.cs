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

        /// <summary>
        /// Ensure an app pool exists and is configured with sane defaults.
        /// </summary>
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

        /// <summary>
        /// Ensure a top-level IIS Site exists with a root app ("/") mapped to physicalPath and bound via binding (e.g. "*:80:" or "*:80:host").
        /// Also assigns its root application to the given app pool.
        /// </summary>
        public static void EnsureSite(string siteName, string appPoolName, string physicalPath, string binding)
        {
            if (string.IsNullOrWhiteSpace(siteName)) throw new ArgumentException("siteName required");
            if (string.IsNullOrWhiteSpace(appPoolName)) throw new ArgumentException("appPoolName required");
            if (string.IsNullOrWhiteSpace(physicalPath)) throw new ArgumentException("physicalPath required");
            if (string.IsNullOrWhiteSpace(binding)) throw new ArgumentException("binding required");

            Directory.CreateDirectory(physicalPath);

            using var sm = new ServerManager();

            var site = sm.Sites[siteName];
            if (site == null)
            {
                site = sm.Sites.Add(siteName, "http", binding, physicalPath);
            }
            else
            {
                // Update mapping/path
                var rootApp = site.Applications["/"] ?? site.Applications.Add("/", physicalPath);
                rootApp.VirtualDirectories["/"].PhysicalPath = physicalPath;

                // Normalize bindings to single HTTP binding provided
                site.Bindings.Clear();
                site.Bindings.Add(binding, "http");
            }

            // Root app → app pool
            site.Applications["/"].ApplicationPoolName = appPoolName;

            sm.CommitChanges();
        }

        /// <summary>
        /// Ensure a child application exists under an existing site.
        /// appPath must start with '/', e.g. "/CaseInfoSearch".
        /// </summary>
        public static void EnsureApp(string siteName, string appPath, string physicalPath, string appPoolName)
        {
            if (string.IsNullOrWhiteSpace(siteName)) throw new ArgumentException("siteName required");
            if (string.IsNullOrWhiteSpace(appPath) || !appPath.StartsWith("/"))
                throw new ArgumentException("appPath must start with '/'", nameof(appPath));
            if (string.IsNullOrWhiteSpace(physicalPath)) throw new ArgumentException("physicalPath required");
            if (string.IsNullOrWhiteSpace(appPoolName)) throw new ArgumentException("appPoolName required");

            Directory.CreateDirectory(physicalPath);

            using var sm = new ServerManager();
            var site = sm.Sites[siteName] ?? throw new InvalidOperationException($"Site '{siteName}' not found.");

            var app = site.Applications[appPath] ?? site.Applications.Add(appPath, physicalPath);
            app.ApplicationPoolName = appPoolName;

            // Ensure vdir points correctly
            app.VirtualDirectories["/"].PhysicalPath = physicalPath;

            sm.CommitChanges();
        }

        // ---------------------------
        // Back-compat convenience: Default Web Site mapping
        // ---------------------------

        /// <summary>
        /// Create or retarget an app under "Default Web Site" at "/{appSegmentName}" and assign an app pool.
        /// By default, does NOT stop/start the pool (set stopStartPool=true if you really need that behavior).
        /// </summary>
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

        public static void EnsurePoolStopped(string poolName, int timeoutMs = 60000)
        {
            using var sm = new ServerManager();
            var pool = sm.ApplicationPools.FirstOrDefault(p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase));
            if (pool == null) return;
            if (pool.State == ObjectState.Stopped || pool.State == ObjectState.Stopping) return;

            pool.Stop(); sm.CommitChanges();
            WaitForPoolState(sm, pool.Name, ObjectState.Stopped, timeoutMs);
        }

        public static void EnsurePoolStarted(string poolName, int timeoutMs = 60000)
        {
            using var sm = new ServerManager();
            var pool = sm.ApplicationPools.FirstOrDefault(p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase));
            if (pool == null) return;
            if (pool.State == ObjectState.Started || pool.State == ObjectState.Starting) return;

            pool.Start(); sm.CommitChanges();
            WaitForPoolState(sm, pool.Name, ObjectState.Started, timeoutMs);
        }

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

        // ---------------------------
        // Batch stop → do work → start
        // ---------------------------

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

            if (stoppedPools.Count > 0)
                log?.Invoke($"App pools stopped: {string.Join(", ", stoppedPools)}");

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

            if (startedPools.Count > 0)
                log?.Invoke($"App pools started: {string.Join(", ", startedPools)}");

            if (workError is not null) throw workError;
        }
    }
}
