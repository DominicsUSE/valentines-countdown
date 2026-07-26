using System.Diagnostics;
using System.Net.NetworkInformation;
using RobloxOptimizer.Core.Enums;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Network;

/// <summary>
/// Runs latency/jitter/packet-loss/throughput/Wi-Fi diagnostics using only
/// documented .NET and Windows APIs (ICMP ping, NetworkInterface statistics,
/// netsh). Never modifies TCP/IP stack settings, IPv6, firewall rules, or
/// port configuration.
/// </summary>
public sealed class NetworkDiagnostics : INetworkDiagnostics
{
    public async Task<NetworkSnapshot> RunDiagnosticsAsync(NetworkDiagnosticsOptions options, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var primaryInterface = GetPrimaryInterface();
        var connectionType = ClassifyConnectionType(primaryInterface);

        var beforeStats = TryGetStatistics(primaryInterface);

        var wifiSignalTask = connectionType == ConnectionType.WiFi
            ? WifiSignalReader.GetSignalPercentAsync(cancellationToken)
            : Task.FromResult(Measurement<int>.Unavailable("Not connected via Wi-Fi"));

        var (current, average, jitter, packetLoss) = await MeasurePrimaryPingSeriesAsync(options, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<RegionalPingResult> regionalPings = Array.Empty<RegionalPingResult>();
        if (options.IncludeRegionalComparison)
        {
            regionalPings = await RegionalEndpoints.MeasureAllAsync(options.PingTimeoutMs, cancellationToken).ConfigureAwait(false);
        }

        var wifiSignal = await wifiSignalTask.ConfigureAwait(false);

        var afterStats = TryGetStatistics(primaryInterface);
        stopwatch.Stop();

        var (downloadKbps, uploadKbps) = ComputeThroughput(beforeStats, afterStats, stopwatch.Elapsed);

        return new NetworkSnapshot
        {
            ConnectionType = connectionType,
            AdapterDescription = primaryInterface?.Description,
            WifiSignalPercent = wifiSignal,
            PingCurrentMs = current,
            PingAverageMs = average,
            JitterMs = jitter,
            PacketLossPercent = packetLoss,
            DownloadKilobitsPerSecond = downloadKbps,
            UploadKilobitsPerSecond = uploadKbps,
            RegionalPings = regionalPings,
            SampledAtUtc = DateTimeOffset.UtcNow
        };
    }

    public Task<ActionResult> FlushDnsCacheAsync(CancellationToken cancellationToken = default)
    {
        return RunProcessAsync("ipconfig", "/flushdns", cancellationToken);
    }

    private static async Task<(Measurement<double> current, Measurement<double> average, Measurement<double> jitter, Measurement<double> packetLoss)>
        MeasurePrimaryPingSeriesAsync(NetworkDiagnosticsOptions options, CancellationToken cancellationToken)
    {
        var samples = new List<double>();
        var attempted = 0;

        for (var i = 0; i < options.PingCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempted++;

            var result = await PingHelper.SingleShotAsync(options.PrimaryProbeHost, options.PingTimeoutMs).ConfigureAwait(false);
            if (result.IsAvailable)
            {
                samples.Add(result.Value);
            }

            if (i < options.PingCount - 1)
            {
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
        }

        var packetLossPercent = attempted > 0 ? (attempted - samples.Count) / (double)attempted * 100.0 : 0;
        var packetLoss = Measurement<double>.Of(Math.Round(packetLossPercent, 1));

        if (samples.Count == 0)
        {
            var reason = $"No successful replies from {options.PrimaryProbeHost}";
            return (Measurement<double>.Unavailable(reason), Measurement<double>.Unavailable(reason), Measurement<double>.Unavailable(reason), packetLoss);
        }

        var current = Measurement<double>.Of(samples[^1]);
        var average = Measurement<double>.Of(Math.Round(samples.Average(), 1));

        Measurement<double> jitter;
        if (samples.Count >= 2)
        {
            var diffs = new List<double>();
            for (var i = 1; i < samples.Count; i++)
            {
                diffs.Add(Math.Abs(samples[i] - samples[i - 1]));
            }

            jitter = Measurement<double>.Of(Math.Round(diffs.Average(), 1));
        }
        else
        {
            jitter = Measurement<double>.Unavailable("Need at least two successful replies to compute jitter");
        }

        return (current, average, jitter, packetLoss);
    }

    private static NetworkInterface? GetPrimaryInterface()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Where(nic => nic.GetIPProperties().GatewayAddresses.Any(g => g.Address.ToString() != "0.0.0.0"))
            .ToList();

        // Prefer a real Ethernet/Wi-Fi adapter over virtual adapters (VPN clients, Hyper-V, VMware, etc.).
        return candidates
            .OrderByDescending(nic => nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .ThenByDescending(nic => !LooksVirtual(nic.Description))
            .FirstOrDefault();
    }

    private static bool LooksVirtual(string description)
    {
        string[] virtualMarkers = { "virtual", "vpn", "hyper-v", "vmware", "loopback", "tunnel", "tap-" };
        return virtualMarkers.Any(marker => description.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static ConnectionType ClassifyConnectionType(NetworkInterface? nic)
    {
        if (nic is null)
        {
            return ConnectionType.Unknown;
        }

        return nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx => ConnectionType.Ethernet,
            NetworkInterfaceType.Wireless80211 => ConnectionType.WiFi,
            _ => ConnectionType.Other
        };
    }

    private static IPv4InterfaceStatistics? TryGetStatistics(NetworkInterface? nic)
    {
        try
        {
            return nic?.GetIPv4Statistics();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static (Measurement<double> downloadKbps, Measurement<double> uploadKbps) ComputeThroughput(
        IPv4InterfaceStatistics? before, IPv4InterfaceStatistics? after, TimeSpan elapsed)
    {
        if (before is null || after is null || elapsed.TotalSeconds <= 0)
        {
            const string reason = "Network adapter statistics unavailable";
            return (Measurement<double>.Unavailable(reason), Measurement<double>.Unavailable(reason));
        }

        try
        {
            var bytesReceived = after.BytesReceived - before.BytesReceived;
            var bytesSent = after.BytesSent - before.BytesSent;

            var downloadKbps = bytesReceived < 0 ? 0 : bytesReceived * 8.0 / 1000.0 / elapsed.TotalSeconds;
            var uploadKbps = bytesSent < 0 ? 0 : bytesSent * 8.0 / 1000.0 / elapsed.TotalSeconds;

            return (Measurement<double>.Of(Math.Round(downloadKbps, 1)), Measurement<double>.Of(Math.Round(uploadKbps, 1)));
        }
        catch (Exception ex)
        {
            var reason = "Failed to read adapter statistics: " + ex.Message;
            return (Measurement<double>.Unavailable(reason), Measurement<double>.Unavailable(reason));
        }
    }

    private static async Task<ActionResult> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return ActionResult.Fail($"Could not start {fileName}");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0
                ? ActionResult.Ok($"{fileName} {arguments} completed successfully")
                : ActionResult.Fail($"{fileName} {arguments} exited with code {process.ExitCode}");
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Failed to run {fileName} {arguments}", ex.Message);
        }
    }
}
