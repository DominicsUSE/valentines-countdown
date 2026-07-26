using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

/// <summary>Reads CPU, RAM, disk, and GPU utilization from the OS.</summary>
public interface ISystemMonitor
{
    Task<SystemSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
