using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Network;

/// <summary>
/// Well-known, publicly reachable Internet points of presence used to gauge
/// general route quality. RPO cannot discover which physical server a
/// Roblox game session is matched to and does not attempt to connect to
/// arbitrary game-server IPs - see docs/ARCHITECTURE.md. These pings only
/// tell you whether *your* connection to the wider Internet is fast and
/// stable; a game server in a distant region can still have higher ping
/// than any of these even when your connection is perfectly healthy.
/// </summary>
public static class RegionalEndpoints
{
    public static IReadOnlyList<(string Name, string Host, string ApproximateRegion)> Default { get; } = new List<(string, string, string)>
    {
        ("Cloudflare (global anycast)", "1.1.1.1", "Routes to your nearest Cloudflare point of presence"),
        ("Google Public DNS (global anycast)", "8.8.8.8", "Routes to your nearest Google point of presence"),
        ("Quad9 (global anycast)", "9.9.9.9", "Routes to your nearest Quad9 point of presence"),
        ("OpenDNS (global anycast)", "208.67.222.222", "Routes to your nearest OpenDNS point of presence")
    };

    public static async Task<IReadOnlyList<RegionalPingResult>> MeasureAllAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        var results = new List<RegionalPingResult>(Default.Count);

        foreach (var (name, host, region) in Default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var latency = await PingHelper.SingleShotAsync(host, timeoutMs).ConfigureAwait(false);
            results.Add(new RegionalPingResult
            {
                EndpointName = name,
                HostOrAddress = host,
                ApproximateRegion = region,
                LatencyMs = latency
            });
        }

        return results;
    }
}
