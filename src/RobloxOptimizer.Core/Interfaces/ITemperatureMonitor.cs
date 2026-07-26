using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

/// <summary>Reads CPU/GPU temperature and detects thermal throttling, when the hardware/firmware exposes it.</summary>
public interface ITemperatureMonitor
{
    Task<TemperatureSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
