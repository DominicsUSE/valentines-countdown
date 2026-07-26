using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

/// <summary>
/// Runs a single, narrowly-whitelisted action in a briefly-elevated helper
/// process (UAC prompt), used only for the handful of actions in
/// docs/SAFETY_MODEL.md that genuinely require administrator rights.
/// The main application window is never elevated.
/// </summary>
public interface IElevationService
{
    bool IsCurrentProcessElevated { get; }

    Task<ElevatedActionResult> RunElevatedActionAsync(string actionKey, string? argument, CancellationToken cancellationToken = default);
}
