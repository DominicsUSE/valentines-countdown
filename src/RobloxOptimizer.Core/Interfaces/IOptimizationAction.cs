using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

/// <summary>
/// A single, reversible optimization step. Implementations must never change
/// anything in their constructor - only inside <see cref="ApplyAsync"/>, and
/// only after <see cref="CaptureCurrentStateAsync"/> has been recorded by the
/// caller so the change can be undone.
/// </summary>
public interface IOptimizationAction
{
    /// <summary>Stable identifier used in backups and the audit log. Never change once shipped.</summary>
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    bool RequiresAdmin { get; }

    /// <summary>Serializes the current state so it can be restored later. Returns null if there is nothing meaningful to restore (e.g. a one-shot action).</summary>
    Task<string?> CaptureCurrentStateAsync(CancellationToken cancellationToken = default);

    Task<ActionResult> ApplyAsync(CancellationToken cancellationToken = default);

    /// <summary>Restores the state previously returned by <see cref="CaptureCurrentStateAsync"/>.</summary>
    Task<ActionResult> UndoAsync(string? previousState, CancellationToken cancellationToken = default);
}
