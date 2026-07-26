using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

/// <summary>
/// Best-effort per-process frame rate tracing. See docs/ARCHITECTURE.md for why
/// this requires elevation and can be unavailable.
/// </summary>
public interface IFpsMonitor : IDisposable
{
    /// <summary>Begin tracing frame presentation for the given process. Safe to call even if tracing cannot be started - failures surface through <see cref="GetSnapshot"/>.</summary>
    void StartTracking(int processId);

    void StopTracking();

    FpsSnapshot GetSnapshot();
}
