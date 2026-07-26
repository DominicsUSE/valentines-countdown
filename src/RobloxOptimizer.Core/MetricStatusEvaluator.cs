using RobloxOptimizer.Core.Enums;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core;

/// <summary>
/// Single source of truth for turning a raw measurement into the
/// Green/Yellow/Red status shown on the dashboard. The same thresholds are
/// reused by <c>RobloxOptimizer.Optimization.RecommendationEngine</c> so the
/// "why is this recommended" reasoning always matches the color the user
/// sees. Thresholds are heuristic classification bands applied to a real
/// measurement - never a substitute for one.
/// </summary>
public static class MetricStatusEvaluator
{
    public const double LowFpsCriticalBelow = 30;
    public const double LowFpsWarningBelow = 45;

    public const double HighPingWarningAboveMs = 80;
    public const double HighPingCriticalAboveMs = 150;

    public const double HighJitterWarningAboveMs = 15;
    public const double HighJitterCriticalAboveMs = 30;

    public const double PacketLossWarningAbovePercent = 1.0;
    public const double PacketLossCriticalAbovePercent = 3.0;

    public const double HighUsageWarningAbovePercent = 80;
    public const double HighUsageCriticalAbovePercent = 92;

    public const double WeakWifiWarningBelowPercent = 50;
    public const double WeakWifiCriticalBelowPercent = 25;

    public const double HighTemperatureWarningAboveCelsius = 80;
    public const double HighTemperatureCriticalAboveCelsius = 90;

    public static StatusLevel ForFps(Measurement<double> averageFps)
    {
        if (!averageFps.IsAvailable)
        {
            return StatusLevel.Unknown;
        }

        return averageFps.Value switch
        {
            < LowFpsCriticalBelow => StatusLevel.Critical,
            < LowFpsWarningBelow => StatusLevel.Warning,
            _ => StatusLevel.Good
        };
    }

    public static StatusLevel ForPing(Measurement<double> averagePingMs)
    {
        if (!averagePingMs.IsAvailable)
        {
            return StatusLevel.Unknown;
        }

        return averagePingMs.Value switch
        {
            > HighPingCriticalAboveMs => StatusLevel.Critical,
            > HighPingWarningAboveMs => StatusLevel.Warning,
            _ => StatusLevel.Good
        };
    }

    public static StatusLevel ForJitter(Measurement<double> jitterMs)
    {
        if (!jitterMs.IsAvailable)
        {
            return StatusLevel.Unknown;
        }

        return jitterMs.Value switch
        {
            > HighJitterCriticalAboveMs => StatusLevel.Critical,
            > HighJitterWarningAboveMs => StatusLevel.Warning,
            _ => StatusLevel.Good
        };
    }

    public static StatusLevel ForPacketLoss(Measurement<double> packetLossPercent)
    {
        if (!packetLossPercent.IsAvailable)
        {
            return StatusLevel.Unknown;
        }

        return packetLossPercent.Value switch
        {
            > PacketLossCriticalAbovePercent => StatusLevel.Critical,
            > PacketLossWarningAbovePercent => StatusLevel.Warning,
            _ => StatusLevel.Good
        };
    }

    /// <summary>Combined network health across ping/jitter/packet loss - the worst of the three.</summary>
    public static StatusLevel ForNetworkOverall(NetworkSnapshot network)
    {
        var levels = new[]
        {
            ForPing(network.PingAverageMs),
            ForJitter(network.JitterMs),
            ForPacketLoss(network.PacketLossPercent)
        };

        if (levels.All(l => l == StatusLevel.Unknown))
        {
            return StatusLevel.Unknown;
        }

        if (levels.Contains(StatusLevel.Critical))
        {
            return StatusLevel.Critical;
        }

        return levels.Contains(StatusLevel.Warning) ? StatusLevel.Warning : StatusLevel.Good;
    }

    public static StatusLevel ForUsagePercent(Measurement<double> usagePercent)
    {
        if (!usagePercent.IsAvailable)
        {
            return StatusLevel.Unknown;
        }

        return usagePercent.Value switch
        {
            > HighUsageCriticalAbovePercent => StatusLevel.Critical,
            > HighUsageWarningAbovePercent => StatusLevel.Warning,
            _ => StatusLevel.Good
        };
    }

    /// <summary>Combined system health across CPU/RAM/Disk/GPU - the worst of the four.</summary>
    public static StatusLevel ForSystemOverall(SystemSnapshot system)
    {
        var levels = new[]
        {
            ForUsagePercent(system.CpuUsagePercent),
            ForUsagePercent(system.RamUsagePercent),
            ForUsagePercent(system.DiskUsagePercent),
            ForUsagePercent(system.GpuUsagePercent)
        };

        if (levels.All(l => l == StatusLevel.Unknown))
        {
            return StatusLevel.Unknown;
        }

        if (levels.Contains(StatusLevel.Critical))
        {
            return StatusLevel.Critical;
        }

        return levels.Contains(StatusLevel.Warning) ? StatusLevel.Warning : StatusLevel.Good;
    }

    public static StatusLevel ForWifiSignal(Measurement<int> signalPercent)
    {
        if (!signalPercent.IsAvailable)
        {
            return StatusLevel.Unknown;
        }

        return signalPercent.Value switch
        {
            < WeakWifiCriticalBelowPercent => StatusLevel.Critical,
            < WeakWifiWarningBelowPercent => StatusLevel.Warning,
            _ => StatusLevel.Good
        };
    }

    public static StatusLevel ForTemperature(Measurement<double> temperatureCelsius)
    {
        if (!temperatureCelsius.IsAvailable)
        {
            return StatusLevel.Unknown;
        }

        return temperatureCelsius.Value switch
        {
            > HighTemperatureCriticalAboveCelsius => StatusLevel.Critical,
            > HighTemperatureWarningAboveCelsius => StatusLevel.Warning,
            _ => StatusLevel.Good
        };
    }
}
