using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public static class TempCleaner
{
    private const string Root =
        @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files";

    /// <summary>
    /// Deletes ASP.NET temp folders for the given app pool names (in parallel).
    /// Call this AFTER stopping app pools and BEFORE starting them again.
    /// </summary>
    public static async Task ClearForAppPoolsAsync(
        IEnumerable<string> appPoolNames,
        Action<string>? log = null,
        int maxDegreeOfParallelism = 4)
    {
        if (!Directory.Exists(Root)) return;

        var targets = appPoolNames?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => Path.Combine(Root, s.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .ToList() ?? new List<string>();

        if (targets.Count == 0) return;

        // Run deletions concurrently but with a sensible cap
        using var throttler = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = targets.Select(async dir =>
        {
            await throttler.WaitAsync().ConfigureAwait(false);
            try { TryHardDelete(dir, log); }
            finally { throttler.Release(); }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    // ---- internals: robust delete with retries ----

    private static void TryHardDelete(string dir, Action<string>? log)
    {
        if (!Directory.Exists(dir)) return;

        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                ClearReadOnlyFlags(dir);
                Directory.Delete(dir, recursive: true);
                log?.Invoke($"[ASP.NET Temp64] Deleted: {dir}");
                return;
            }
            catch (IOException ioEx)
            {
                log?.Invoke($"[ASP.NET Temp64] (Attempt {attempt}) IO: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                log?.Invoke($"[ASP.NET Temp64] (Attempt {attempt}) Access: {uaEx.Message}");
            }
            Thread.Sleep(300); 
        }

        // last resort: force-empty then delete once more
        try
        {
            ForceEmptyDirectory(dir, log);
            Directory.Delete(dir, recursive: true);
            log?.Invoke($"[ASP.NET Temp64] Deleted after force-empty: {dir}");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[ASP.NET Temp64] FAILED '{dir}': {ex.Message}");
        }
    }

    private static void ClearReadOnlyFlags(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        foreach (var d in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(d, FileAttributes.Directory);
    }

    private static void ForceEmptyDirectory(string dir, Action<string>? log)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); }
            catch (Exception ex) { log?.Invoke($"[ASP.NET Temp64] File lock '{f}': {ex.Message}"); }
        }

        // delete deepest dirs first
        foreach (var d in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(p => p.Length))
        {
            try { Directory.Delete(d, recursive: false); }
            catch (Exception ex) { log?.Invoke($"[ASP.NET Temp64] Dir lock '{d}': {ex.Message}"); }
        }
    }
}
