using System.Net.NetworkInformation;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Network;

internal static class PingHelper
{
    public static async Task<Measurement<double>> SingleShotAsync(string hostOrAddress, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(hostOrAddress, timeoutMs).ConfigureAwait(false);

            return reply.Status == IPStatus.Success
                ? Measurement<double>.Of(reply.RoundtripTime)
                : Measurement<double>.Unavailable($"No reply from {hostOrAddress} ({reply.Status})");
        }
        catch (Exception ex)
        {
            // Covers PingException (bad host, network unreachable, etc.) and any
            // other transient failure - a diagnostic probe must never crash the app.
            return Measurement<double>.Unavailable("Ping failed: " + ex.Message);
        }
    }
}
