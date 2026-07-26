namespace RobloxOptimizer.Core.Models;

/// <summary>
/// Point-in-time system resource usage. All fields come from real OS counters;
/// none are estimated.
/// </summary>
public sealed class SystemSnapshot
{
    public required Measurement<double> CpuUsagePercent { get; init; }
    public required Measurement<double> RamUsagePercent { get; init; }
    public required Measurement<double> RamUsedMegabytes { get; init; }
    public required Measurement<double> RamTotalMegabytes { get; init; }
    public required Measurement<double> DiskUsagePercent { get; init; }
    public required Measurement<double> GpuUsagePercent { get; init; }
    public required DateTimeOffset SampledAtUtc { get; init; }
}
