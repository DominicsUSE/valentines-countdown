using System.Diagnostics;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization;

/// <summary>
/// Read-only snapshot of running processes with meaningful CPU/RAM usage,
/// for the user to review and optionally close from the Optimize tab. RPO
/// never selects or closes anything here on its own - see
/// docs/SAFETY_MODEL.md for the processes this always excludes.
/// </summary>
public static class BackgroundAppScanner
{
    private const int MaxResults = 25;

    public static async Task<IReadOnlyList<BackgroundAppInfo>> ScanAsync(TimeSpan sampleWindow, CancellationToken cancellationToken = default)
    {
        var processes = Process.GetProcesses();

        try
        {
            var initialCpuTimes = new Dictionary<int, TimeSpan>();
            foreach (var process in processes)
            {
                initialCpuTimes[process.Id] = TryGetCpuTime(process);
            }

            await Task.Delay(sampleWindow, cancellationToken).ConfigureAwait(false);

            var results = new List<BackgroundAppInfo>();

            foreach (var process in processes)
            {
                if (ProtectedProcesses.IsProtected(process.ProcessName))
                {
                    continue;
                }

                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                try
                {
                    process.Refresh();
                }
                catch (Exception)
                {
                    continue;
                }

                var afterCpuTime = TryGetCpuTime(process);
                var beforeCpuTime = initialCpuTimes.GetValueOrDefault(process.Id);
                var cpuDelta = afterCpuTime - beforeCpuTime;

                var cpuPercent = cpuDelta > TimeSpan.Zero && sampleWindow > TimeSpan.Zero
                    ? Measurement<double>.Of(Math.Round(cpuDelta.TotalMilliseconds / (sampleWindow.TotalMilliseconds * Environment.ProcessorCount) * 100.0, 1))
                    : Measurement<double>.Of(0);

                var memoryMb = TryGetMemoryMb(process);

                var hasVisibleWindow = TryHasVisibleWindow(process);
                var isMeaningful = (memoryMb.IsAvailable && memoryMb.Value >= 100) || (cpuPercent.IsAvailable && cpuPercent.Value >= 1.0) || hasVisibleWindow;

                if (!isMeaningful)
                {
                    continue;
                }

                results.Add(new BackgroundAppInfo
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    WindowTitle = TryGetWindowTitle(process),
                    CpuPercent = cpuPercent,
                    MemoryMegabytes = memoryMb,
                    IsProtected = false
                });
            }

            return results
                .OrderByDescending(app => (app.CpuPercent.IsAvailable ? app.CpuPercent.Value : 0) * 2 + (app.MemoryMegabytes.IsAvailable ? app.MemoryMegabytes.Value : 0) / 100.0)
                .Take(MaxResults)
                .ToList();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static TimeSpan TryGetCpuTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime;
        }
        catch (Exception)
        {
            return TimeSpan.Zero;
        }
    }

    private static Measurement<double> TryGetMemoryMb(Process process)
    {
        try
        {
            return Measurement<double>.Of(Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1));
        }
        catch (Exception)
        {
            return Measurement<double>.Unavailable("Access denied");
        }
    }

    private static string? TryGetWindowTitle(Process process)
    {
        try
        {
            return string.IsNullOrWhiteSpace(process.MainWindowTitle) ? null : process.MainWindowTitle;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryHasVisibleWindow(Process process)
    {
        try
        {
            return process.MainWindowHandle != IntPtr.Zero;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
