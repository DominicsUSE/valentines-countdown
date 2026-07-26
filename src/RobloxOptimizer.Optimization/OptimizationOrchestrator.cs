using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization;

/// <summary>
/// Applies/undoes the "Optimize for Roblox" bundle of <see cref="IOptimizationAction"/>s
/// (power plan, overlay toggle, GPU preference, process priority). These four are
/// treated as temporary, session-scoped changes: they're captured to the backup
/// snapshot before every Apply, exposed through "Undo Changes"/"Restore Defaults",
/// and automatically reverted the moment Roblox exits so nothing is left modified
/// after a play session even if the user forgets to click Undo.
///
/// Startup-app toggles and background-app closures are deliberately <b>not</b>
/// part of this bundle - see docs/ARCHITECTURE.md "Reversibility scope". Those are
/// persistent, per-item user choices with their own dedicated undo control
/// (re-enable the specific item), not something that should silently flip back
/// just because Roblox closed.
/// </summary>
public sealed class OptimizationOrchestrator : IDisposable
{
    private const string BackupKey = "state";

    private readonly IReadOnlyDictionary<string, IOptimizationAction> _actionsById;
    private readonly IBackupService _backupService;
    private readonly IAuditLogger _auditLogger;
    private readonly IRobloxDetector _robloxDetector;
    private readonly HashSet<string> _appliedActionIds = new();
    private bool _disposed;

    public OptimizationOrchestrator(
        IEnumerable<IOptimizationAction> actions,
        IBackupService backupService,
        IAuditLogger auditLogger,
        IRobloxDetector robloxDetector)
    {
        _actionsById = actions.ToDictionary(a => a.Id);
        _backupService = backupService;
        _auditLogger = auditLogger;
        _robloxDetector = robloxDetector;
        _robloxDetector.RobloxExited += OnRobloxExited;
    }

    public IReadOnlyCollection<string> AppliedActionIds => _appliedActionIds;

    public IReadOnlyCollection<IOptimizationAction> AvailableActions => _actionsById.Values.ToList();

    public async Task<ActionResult> ApplyAsync(string actionId, CancellationToken cancellationToken = default)
    {
        if (!_actionsById.TryGetValue(actionId, out var action))
        {
            return ActionResult.Fail($"Unknown optimization action '{actionId}'");
        }

        var previousState = await action.CaptureCurrentStateAsync(cancellationToken).ConfigureAwait(false);
        var result = await action.ApplyAsync(cancellationToken).ConfigureAwait(false);

        if (result.Success)
        {
            await _backupService.RecordEntryAsync(action.Id, BackupKey, previousState, cancellationToken).ConfigureAwait(false);
            _appliedActionIds.Add(action.Id);
        }

        await LogAsync(action, "Apply", previousState, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<ActionResult>> ApplyManyAsync(IEnumerable<string> actionIds, CancellationToken cancellationToken = default)
    {
        var results = new List<ActionResult>();
        foreach (var actionId in actionIds)
        {
            results.Add(await ApplyAsync(actionId, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<ActionResult> UndoAsync(string actionId, CancellationToken cancellationToken = default)
    {
        if (!_actionsById.TryGetValue(actionId, out var action))
        {
            return ActionResult.Fail($"Unknown optimization action '{actionId}'");
        }

        var entry = await _backupService.GetEntryAsync(action.Id, BackupKey, cancellationToken).ConfigureAwait(false);
        var result = await action.UndoAsync(entry?.PreviousValue, cancellationToken).ConfigureAwait(false);

        if (result.Success)
        {
            await _backupService.RemoveEntryAsync(action.Id, BackupKey, cancellationToken).ConfigureAwait(false);
            _appliedActionIds.Remove(action.Id);
        }

        await LogAsync(action, "Undo", entry?.PreviousValue, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>Undoes every currently-applied action in this bundle - the "Restore Defaults"/"Undo Changes" button.</summary>
    public async Task<IReadOnlyList<ActionResult>> UndoAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ActionResult>();
        foreach (var actionId in _appliedActionIds.ToList())
        {
            results.Add(await UndoAsync(actionId, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private async void OnRobloxExited(object? sender, EventArgs e)
    {
        // Best effort: automatically revert the temporary session-scoped changes
        // now that Roblox has closed, per docs/SAFETY_MODEL.md. If this fails for
        // any reason the user can still click "Undo Changes" manually.
        try
        {
            await UndoAllAsync().ConfigureAwait(false);
        }
        catch
        {
            // Swallow - this runs on an event handler with no caller to observe a fault.
        }
    }

    private Task LogAsync(IOptimizationAction action, string verb, string? previousValue, ActionResult result, CancellationToken cancellationToken)
    {
        return _auditLogger.LogAsync(new AuditLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            ActionId = action.Id,
            Description = $"{verb}: {action.DisplayName}",
            PreviousValue = previousValue,
            NewValue = result.Success ? (verb == "Undo" ? "restored" : "applied") : null,
            Success = result.Success,
            ErrorMessage = result.Success ? null : result.Message,
            RanElevated = action.RequiresAdmin
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _robloxDetector.RobloxExited -= OnRobloxExited;
    }
}
