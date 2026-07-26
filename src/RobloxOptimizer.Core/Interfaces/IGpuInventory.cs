namespace RobloxOptimizer.Core.Interfaces;

/// <summary>Enumerates display adapters so the app can tell when a system has more than one GPU (e.g. laptop integrated + dedicated).</summary>
public interface IGpuInventory
{
    Task<IReadOnlyList<string>> GetDetectedGpuNamesAsync(CancellationToken cancellationToken = default);
}
