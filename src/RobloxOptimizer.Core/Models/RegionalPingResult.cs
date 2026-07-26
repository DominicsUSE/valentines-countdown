namespace RobloxOptimizer.Core.Models;

/// <summary>
/// Latency to one well-known, publicly reachable Internet endpoint. This is a
/// proxy for general route quality only - RPO has no way to know, and does not
/// claim to know, which physical server a Roblox session is matched to.
/// </summary>
public sealed class RegionalPingResult
{
    public required string EndpointName { get; init; }
    public required string HostOrAddress { get; init; }
    public required string ApproximateRegion { get; init; }
    public required Measurement<double> LatencyMs { get; init; }
}
