using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

public sealed class NetworkDiagnosticsOptions
{
    public int PingCount { get; init; } = 10;
    public int PingTimeoutMs { get; init; } = 1500;
    public bool IncludeRegionalComparison { get; init; } = true;

    /// <summary>Host pinged for the primary current/average/jitter/packet-loss readings. Defaults to a well-known, highly-available public resolver.</summary>
    public string PrimaryProbeHost { get; init; } = "1.1.1.1";
}

/// <summary>Runs latency/jitter/packet-loss/throughput/Wi-Fi diagnostics. Never alters TCP/IP stack settings.</summary>
public interface INetworkDiagnostics
{
    Task<NetworkSnapshot> RunDiagnosticsAsync(NetworkDiagnosticsOptions options, CancellationToken cancellationToken = default);

    /// <summary>Flushes the local DNS resolver cache. Does not require admin rights on current Windows releases. Does not meaningfully reduce in-game ping - this is a troubleshooting step only.</summary>
    Task<ActionResult> FlushDnsCacheAsync(CancellationToken cancellationToken = default);
}
