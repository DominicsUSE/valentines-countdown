using System.Linq;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Enums;
using RobloxOptimizer.Optimization;
using Xunit;

namespace RobloxOptimizer.Tests;

public class RecommendationEngineTests
{
    [Fact]
    public void HealthySnapshot_ProducesNoLagCauses()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy();

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.Equal(LagCause.None, result.DetectedCauses & (LagCause.LowFps | LagCause.HighPing | LagCause.PacketLoss | LagCause.ServerDistance));
        Assert.Contains("No significant FPS or network problems", result.LagExplanation);
    }

    [Fact]
    public void LowFpsWithHealthyNetwork_ExplainsFpsLagNotNetworkLag()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(averageFps: 20, averagePingMs: 25, jitterMs: 3, packetLossPercent: 0);

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.True(result.DetectedCauses.HasFlag(LagCause.LowFps));
        Assert.False(result.DetectedCauses.HasFlag(LagCause.HighPing));
        Assert.Contains("FPS lag, not network lag", result.LagExplanation);
        Assert.Contains(result.Recommendations, r => r.Title == "Lower Roblox's graphics quality");
    }

    [Fact]
    public void HighUnstablePing_ExplainsNetworkLagNotFpsLag_AndDoesNotBlameServerDistance()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(averageFps: 90, averagePingMs: 200, jitterMs: 60, packetLossPercent: 4);

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.False(result.DetectedCauses.HasFlag(LagCause.LowFps));
        Assert.True(result.DetectedCauses.HasFlag(LagCause.HighPing));
        Assert.True(result.DetectedCauses.HasFlag(LagCause.PacketLoss));
        Assert.False(result.DetectedCauses.HasFlag(LagCause.ServerDistance));
        Assert.Contains("network lag, not FPS lag", result.LagExplanation);
        Assert.Contains(result.Recommendations, r => r.Title == "Reduce network congestion");
    }

    [Fact]
    public void HighButStablePing_IsAttributedToServerDistance_NotCongestion()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(averagePingMs: 180, jitterMs: 4, packetLossPercent: 0);

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.True(result.DetectedCauses.HasFlag(LagCause.ServerDistance));
        Assert.False(result.DetectedCauses.HasFlag(LagCause.HighPing));
        Assert.Contains(result.Recommendations, r => r.Title.Contains("likely server distance"));
    }

    [Fact]
    public void WeakWifiSignal_RecommendsEthernet_AndFlagsWeakWifiCause()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(connectionType: ConnectionType.WiFi, wifiSignalPercent: 10);

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.True(result.DetectedCauses.HasFlag(LagCause.WeakWifi));
        Assert.Contains(result.Recommendations, r => r.Title == "Connect through Ethernet");
    }

    [Fact]
    public void EthernetConnection_DoesNotRecommendEthernet()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(connectionType: ConnectionType.Ethernet);

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.DoesNotContain(result.Recommendations, r => r.Title == "Connect through Ethernet");
    }

    [Fact]
    public void MultipleGpus_RecommendsDedicatedGpuWithCorrectActionId()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(detectedGpuCount: 2);

        var result = RecommendationEngine.Analyze(snapshot);

        var recommendation = Assert.Single(result.Recommendations, r => r.ActionId == OptimizationActionIds.GpuPreferenceHighPerformance);
        Assert.True(recommendation.IsAutomatable);
    }

    [Fact]
    public void SingleGpu_DoesNotRecommendGpuPreference()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(detectedGpuCount: 1);

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.DoesNotContain(result.Recommendations, r => r.ActionId == OptimizationActionIds.GpuPreferenceHighPerformance);
    }

    [Fact]
    public void CpuThrottling_RecommendsAddressingOverheating()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(cpuThrottling: true);

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.True(result.DetectedCauses.HasFlag(LagCause.Overheating));
        Assert.Contains(result.Recommendations, r => r.Title.Contains("overheating", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryRecommendation_HasAllTransparencyFieldsPopulated()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(averageFps: 15, averagePingMs: 250, jitterMs: 80, packetLossPercent: 5, cpuThrottling: true, detectedGpuCount: 2);

        var result = RecommendationEngine.Analyze(snapshot);

        Assert.NotEmpty(result.Recommendations);
        foreach (var recommendation in result.Recommendations)
        {
            Assert.False(string.IsNullOrWhiteSpace(recommendation.Title));
            Assert.False(string.IsNullOrWhiteSpace(recommendation.DetectedProblem));
            Assert.False(string.IsNullOrWhiteSpace(recommendation.ProposedChange));
            Assert.False(string.IsNullOrWhiteSpace(recommendation.ExpectedBenefit));
            Assert.False(string.IsNullOrWhiteSpace(recommendation.PossibleSideEffects));
        }
    }

    [Fact]
    public void RecommendationsAreOrderedByPriority()
    {
        var snapshot = TestSnapshotFactory.CreateHealthy(averageFps: 15, averagePingMs: 250, jitterMs: 80, packetLossPercent: 5, cpuThrottling: true, detectedGpuCount: 2);

        var result = RecommendationEngine.Analyze(snapshot);

        var priorities = result.Recommendations.Select(r => r.Priority).ToList();
        var sorted = priorities.OrderBy(p => p).ToList();
        Assert.Equal(sorted, priorities);
    }
}
