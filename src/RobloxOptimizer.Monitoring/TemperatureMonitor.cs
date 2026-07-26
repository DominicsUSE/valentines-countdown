using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Monitoring;

/// <summary>
/// Reads CPU temperature from the ACPI thermal zone WMI class when the
/// system firmware exposes it (many OEM boards do not - this is a firmware
/// limitation, not something RPO can work around) and flags likely thermal
/// throttling from the documented "Processor Information\% of Maximum
/// Frequency" counter. GPU temperature has no reliable, vendor-neutral,
/// OS-native source, so it is always reported as unavailable with guidance
/// to use the GPU vendor's own tool.
/// </summary>
public sealed class TemperatureMonitor : ITemperatureMonitor, IDisposable
{
    private const string GpuTemperatureUnavailableReason =
        "GPU temperature is not exposed by Windows without a GPU vendor tool (e.g. GPU-Z, MSI Afterburner)";

    private readonly PerformanceCounter? _maxFrequencyCounter;
    private readonly PerformanceCounter? _cpuLoadCounter;
    private bool _disposed;

    public TemperatureMonitor()
    {
        _maxFrequencyCounter = TryCreateCounter("Processor Information", "% of Maximum Frequency", "_Total");
        _cpuLoadCounter = TryCreateCounter("Processor", "% Processor Time", "_Total");

        _maxFrequencyCounter?.NextValue();
        _cpuLoadCounter?.NextValue();
    }

    public Task<TemperatureSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var cpuTemp = ReadCpuTemperature();
        var throttle = ReadThrottleStatus();

        var snapshot = new TemperatureSnapshot
        {
            CpuTemperatureCelsius = cpuTemp,
            GpuTemperatureCelsius = Measurement<double>.Unavailable(GpuTemperatureUnavailableReason),
            CpuThrottleDetected = throttle,
            SampledAtUtc = DateTimeOffset.UtcNow
        };

        return Task.FromResult(snapshot);
    }

    private static Measurement<double> ReadCpuTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            using var results = searcher.Get();

            double? highestCelsius = null;

            foreach (ManagementBaseObject obj in results)
            {
                using (obj)
                {
                    if (obj["CurrentTemperature"] is not (long or ulong or int or uint))
                    {
                        continue;
                    }

                    var tenthsOfKelvin = Convert.ToDouble(obj["CurrentTemperature"]);
                    var celsius = tenthsOfKelvin / 10.0 - 273.15;

                    // Firmware occasionally reports an obviously bogus placeholder value; ignore it rather than show nonsense.
                    if (celsius is < -40 or > 130)
                    {
                        continue;
                    }

                    if (highestCelsius is null || celsius > highestCelsius)
                    {
                        highestCelsius = celsius;
                    }
                }
            }

            return highestCelsius is double value
                ? Measurement<double>.Of(value)
                : Measurement<double>.Unavailable("This system's firmware does not report CPU temperature to Windows (MSAcpi_ThermalZoneTemperature returned no usable data)");
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            return Measurement<double>.Unavailable("CPU temperature is not exposed by this system's firmware: " + ex.Message);
        }
    }

    private Measurement<bool> ReadThrottleStatus()
    {
        if (_maxFrequencyCounter is null || _cpuLoadCounter is null)
        {
            return Measurement<bool>.Unavailable("Processor frequency counters unavailable on this system");
        }

        try
        {
            var percentOfMaxFrequency = _maxFrequencyCounter.NextValue();
            var cpuLoad = _cpuLoadCounter.NextValue();

            // Heuristic: under sustained high load, a healthy CPU runs at or
            // near its rated frequency. If load is high but the CPU is
            // running well below its maximum frequency, that is consistent
            // with thermal or power throttling. This is an approximation,
            // not a certain diagnosis - it is surfaced to the user as such.
            var likelyThrottling = cpuLoad > 80 && percentOfMaxFrequency > 0 && percentOfMaxFrequency < 70;

            return Measurement<bool>.Of(likelyThrottling);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            return Measurement<bool>.Unavailable("Processor frequency read failed: " + ex.Message);
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
        _maxFrequencyCounter?.Dispose();
        _cpuLoadCounter?.Dispose();
    }
}
