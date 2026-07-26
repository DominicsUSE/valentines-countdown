using RobloxOptimizer.Core;
using RobloxOptimizer.Core.Enums;
using RobloxOptimizer.Core.Models;
using Xunit;

namespace RobloxOptimizer.Tests;

public class MetricStatusEvaluatorTests
{
    [Theory]
    [InlineData(90, StatusLevel.Good)]
    [InlineData(40, StatusLevel.Warning)]
    [InlineData(20, StatusLevel.Critical)]
    public void ForFps_ClassifiesAgainstThresholds(double fps, StatusLevel expected)
    {
        var result = MetricStatusEvaluator.ForFps(Measurement<double>.Of(fps));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ForFps_ReturnsUnknown_WhenUnavailable()
    {
        var result = MetricStatusEvaluator.ForFps(Measurement<double>.Unavailable("no admin rights"));

        Assert.Equal(StatusLevel.Unknown, result);
    }

    [Theory]
    [InlineData(20, StatusLevel.Good)]
    [InlineData(100, StatusLevel.Warning)]
    [InlineData(200, StatusLevel.Critical)]
    public void ForPing_ClassifiesAgainstThresholds(double ms, StatusLevel expected)
    {
        var result = MetricStatusEvaluator.ForPing(Measurement<double>.Of(ms));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ForNetworkOverall_ReturnsWorstOfPingJitterPacketLoss()
    {
        var network = new NetworkSnapshot
        {
            ConnectionType = ConnectionType.Ethernet,
            AdapterDescription = null,
            WifiSignalPercent = Measurement<int>.Unavailable("wired"),
            PingCurrentMs = Measurement<double>.Of(20),
            PingAverageMs = Measurement<double>.Of(20), // Good
            JitterMs = Measurement<double>.Of(50), // Critical
            PacketLossPercent = Measurement<double>.Of(0), // Good
            DownloadKilobitsPerSecond = Measurement<double>.Of(100),
            UploadKilobitsPerSecond = Measurement<double>.Of(100),
            RegionalPings = Array.Empty<RegionalPingResult>(),
            SampledAtUtc = DateTimeOffset.UtcNow
        };

        var result = MetricStatusEvaluator.ForNetworkOverall(network);

        Assert.Equal(StatusLevel.Critical, result);
    }

    [Fact]
    public void ForWifiSignal_WeakSignalIsCriticalBelowConfiguredThreshold()
    {
        var result = MetricStatusEvaluator.ForWifiSignal(Measurement<int>.Of(10));

        Assert.Equal(StatusLevel.Critical, result);
    }
}
