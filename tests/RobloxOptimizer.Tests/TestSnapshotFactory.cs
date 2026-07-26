using RobloxOptimizer.Core.Enums;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Tests;

/// <summary>Builds a healthy-by-default <see cref="DiagnosticsSnapshot"/> so each test only has to override the metric(s) it cares about.</summary>
internal static class TestSnapshotFactory
{
    public static DiagnosticsSnapshot CreateHealthy(
        double? averageFps = 90,
        double? averagePingMs = 30,
        double? jitterMs = 5,
        double? packetLossPercent = 0,
        double? cpuPercent = 20,
        double? ramPercent = 40,
        double? ramTotalMb = 16000,
        double? gpuPercent = 30,
        ConnectionType connectionType = ConnectionType.Ethernet,
        int? wifiSignalPercent = null,
        bool? cpuThrottling = false,
        int detectedGpuCount = 1,
        IReadOnlyList<BackgroundAppInfo>? backgroundApps = null,
        double? downloadKbps = 500,
        double? uploadKbps = 100)
    {
        return new DiagnosticsSnapshot
        {
            System = new SystemSnapshot
            {
                CpuUsagePercent = ToMeasurement(cpuPercent),
                RamUsagePercent = ToMeasurement(ramPercent),
                RamUsedMegabytes = ToMeasurement(ramTotalMb is double t ? t * (ramPercent ?? 0) / 100.0 : (double?)null),
                RamTotalMegabytes = ToMeasurement(ramTotalMb),
                DiskUsagePercent = Measurement<double>.Of(10),
                GpuUsagePercent = ToMeasurement(gpuPercent),
                SampledAtUtc = DateTimeOffset.UtcNow
            },
            Temperature = new TemperatureSnapshot
            {
                CpuTemperatureCelsius = Measurement<double>.Of(55),
                GpuTemperatureCelsius = Measurement<double>.Unavailable("test"),
                CpuThrottleDetected = cpuThrottling is bool tv ? Measurement<bool>.Of(tv) : Measurement<bool>.Unavailable("test"),
                SampledAtUtc = DateTimeOffset.UtcNow
            },
            Fps = new FpsSnapshot
            {
                Current = ToMeasurement(averageFps),
                Average = ToMeasurement(averageFps),
                Minimum = ToMeasurement(averageFps),
                Maximum = ToMeasurement(averageFps),
                SampledAtUtc = DateTimeOffset.UtcNow
            },
            Network = new NetworkSnapshot
            {
                ConnectionType = connectionType,
                AdapterDescription = "Test Adapter",
                WifiSignalPercent = wifiSignalPercent is int w ? Measurement<int>.Of(w) : Measurement<int>.Unavailable("test"),
                PingCurrentMs = ToMeasurement(averagePingMs),
                PingAverageMs = ToMeasurement(averagePingMs),
                JitterMs = ToMeasurement(jitterMs),
                PacketLossPercent = ToMeasurement(packetLossPercent),
                DownloadKilobitsPerSecond = ToMeasurement(downloadKbps),
                UploadKilobitsPerSecond = ToMeasurement(uploadKbps),
                RegionalPings = Array.Empty<RegionalPingResult>(),
                SampledAtUtc = DateTimeOffset.UtcNow
            },
            Roblox = new RobloxStatus { IsRunning = true, ProcessName = "RobloxPlayerBeta", ProcessId = 1234 },
            DetectedGpuCount = detectedGpuCount,
            BackgroundApps = backgroundApps ?? Array.Empty<BackgroundAppInfo>(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static Measurement<double> ToMeasurement(double? value) =>
        value is double v ? Measurement<double>.Of(v) : Measurement<double>.Unavailable("test");
}
