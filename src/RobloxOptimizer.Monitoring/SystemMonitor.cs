using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Monitoring;

/// <summary>
/// Reads CPU, RAM, disk, and GPU utilization from built-in Windows performance
/// counters and Win32 APIs. Every counter is created once and reused, because
/// rate-based <see cref="PerformanceCounter"/> values require two samples to
/// be meaningful - the first read after construction is intentionally primed
/// and discarded.
/// </summary>
public sealed class SystemMonitor : ISystemMonitor, IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _diskIdleCounter;
    private readonly List<PerformanceCounter> _gpuEngineCounters = new();
    private readonly object _gpuLock = new();
    private DateTime _lastGpuRefreshUtc = DateTime.MinValue;
    private bool _disposed;

    public SystemMonitor()
    {
        _cpuCounter = TryCreateCounter("Processor", "% Processor Time", "_Total");
        _diskIdleCounter = TryCreateCounter("PhysicalDisk", "% Idle Time", "_Total");

        // Prime rate-based counters so the first real reading is meaningful.
        _cpuCounter?.NextValue();
        _diskIdleCounter?.NextValue();

        RefreshGpuEngineCounters();
    }

    public Task<SystemSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var cpu = ReadPercent(_cpuCounter, "CPU usage counter unavailable on this system");
        var disk = ReadDiskUsage();
        var (ramUsedMb, ramTotalMb, ramPercent) = ReadRam();
        var gpu = ReadGpuUsage();

        var snapshot = new SystemSnapshot
        {
            CpuUsagePercent = cpu,
            DiskUsagePercent = disk,
            RamUsedMegabytes = ramUsedMb,
            RamTotalMegabytes = ramTotalMb,
            RamUsagePercent = ramPercent,
            GpuUsagePercent = gpu,
            SampledAtUtc = DateTimeOffset.UtcNow
        };

        return Task.FromResult(snapshot);
    }

    private static Measurement<double> ReadPercent(PerformanceCounter? counter, string unavailableReason)
    {
        if (counter is null)
        {
            return Measurement<double>.Unavailable(unavailableReason);
        }

        try
        {
            var value = counter.NextValue();
            return Measurement<double>.Of(Math.Clamp(value, 0, 100));
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            return Measurement<double>.Unavailable("Performance counter read failed: " + ex.Message);
        }
    }

    private Measurement<double> ReadDiskUsage()
    {
        if (_diskIdleCounter is null)
        {
            return Measurement<double>.Unavailable("Disk activity counter unavailable on this system");
        }

        try
        {
            var idle = _diskIdleCounter.NextValue();
            var busy = 100.0 - idle;
            return Measurement<double>.Of(Math.Clamp(busy, 0, 100));
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            return Measurement<double>.Unavailable("Disk activity counter read failed: " + ex.Message);
        }
    }

    private static (Measurement<double> usedMb, Measurement<double> totalMb, Measurement<double> percent) ReadRam()
    {
        var status = new MEMORYSTATUSEX();
        status.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();

        if (!GlobalMemoryStatusEx(ref status))
        {
            var unavailable = Measurement<double>.Unavailable("GlobalMemoryStatusEx failed");
            return (unavailable, unavailable, unavailable);
        }

        var totalMb = status.ullTotalPhys / 1024.0 / 1024.0;
        var availMb = status.ullAvailPhys / 1024.0 / 1024.0;
        var usedMb = totalMb - availMb;
        var percent = totalMb > 0 ? usedMb / totalMb * 100.0 : 0;

        return (Measurement<double>.Of(usedMb), Measurement<double>.Of(totalMb), Measurement<double>.Of(Math.Clamp(percent, 0, 100)));
    }

    /// <summary>
    /// GPU usage via the "GPU Engine" performance counter category (Windows
    /// 10 1803+) - the same source Task Manager's GPU graph uses. Sums the
    /// 3D-engine instances across all processes; this is a best-effort
    /// approximation, not a vendor-verified figure.
    /// </summary>
    private Measurement<double> ReadGpuUsage()
    {
        lock (_gpuLock)
        {
            if ((DateTime.UtcNow - _lastGpuRefreshUtc) > TimeSpan.FromSeconds(10))
            {
                RefreshGpuEngineCounters();
            }

            if (_gpuEngineCounters.Count == 0)
            {
                return Measurement<double>.Unavailable("GPU Engine performance counters not present on this system");
            }

            try
            {
                double total = 0;
                foreach (var counter in _gpuEngineCounters)
                {
                    total += counter.NextValue();
                }

                return Measurement<double>.Of(Math.Clamp(total, 0, 100));
            }
            catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
            {
                return Measurement<double>.Unavailable("GPU usage read failed: " + ex.Message);
            }
        }
    }

    private void RefreshGpuEngineCounters()
    {
        lock (_gpuLock)
        {
            foreach (var counter in _gpuEngineCounters)
            {
                counter.Dispose();
            }

            _gpuEngineCounters.Clear();
            _lastGpuRefreshUtc = DateTime.UtcNow;

            try
            {
                if (!PerformanceCounterCategory.Exists("GPU Engine"))
                {
                    return;
                }

                var category = new PerformanceCounterCategory("GPU Engine");
                var instanceNames = category.GetInstanceNames();

                foreach (var instanceName in instanceNames)
                {
                    if (!instanceName.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var counter = TryCreateCounter("GPU Engine", "Utilization Percentage", instanceName);
                    if (counter is not null)
                    {
                        counter.NextValue(); // prime
                        _gpuEngineCounters.Add(counter);
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
            {
                // Leave the list empty - GetSnapshotAsync will report GPU usage as unavailable.
                _ = ex;
            }
        }
    }

    private static PerformanceCounter? TryCreateCounter(string category, string counterName, string instance)
    {
        try
        {
            if (!PerformanceCounterCategory.Exists(category))
            {
                return null;
            }

            return new PerformanceCounter(category, counterName, instance, readOnly: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cpuCounter?.Dispose();
        _diskIdleCounter?.Dispose();

        lock (_gpuLock)
        {
            foreach (var counter in _gpuEngineCounters)
            {
                counter.Dispose();
            }

            _gpuEngineCounters.Clear();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
