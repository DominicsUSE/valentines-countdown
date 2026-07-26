using RobloxOptimizer.Core.Enums;

namespace RobloxOptimizer.Core.Models;

/// <summary>
/// A full network diagnostic pass: connection medium, signal, latency to a
/// default probe target, jitter, packet loss, current throughput, and
/// optional regional comparison pings.
/// </summary>
public sealed class NetworkSnapshot
{
    public required ConnectionType ConnectionType { get; init; }
    public required string? AdapterDescription { get; init; }
    public required Measurement<int> WifiSignalPercent { get; init; }
    public required Measurement<double> PingCurrentMs { get; init; }
    public required Measurement<double> PingAverageMs { get; init; }
    public required Measurement<double> JitterMs { get; init; }
    public required Measurement<double> PacketLossPercent { get; init; }
    public required Measurement<double> DownloadKilobitsPerSecond { get; init; }
    public required Measurement<double> UploadKilobitsPerSecond { get; init; }
    public required IReadOnlyList<RegionalPingResult> RegionalPings { get; init; }
    public required DateTimeOffset SampledAtUtc { get; init; }
}
