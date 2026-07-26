using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

/// <summary>Detects RobloxPlayerBeta.exe and raises launch/exit events. Read-only process inspection - never touches Roblox's memory or files.</summary>
public interface IRobloxDetector : IDisposable
{
    RobloxStatus GetCurrentStatus();

    void StartMonitoring();

    void StopMonitoring();

    event EventHandler<RobloxStatus>? RobloxLaunched;

    event EventHandler? RobloxExited;
}
