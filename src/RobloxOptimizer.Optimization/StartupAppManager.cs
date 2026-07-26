using System.Text.Json;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization;

/// <summary>
/// Enables/disables a specific startup entry the user selected. Entries
/// owned by the current user are changed directly (no admin rights needed);
/// all-users entries are changed via a single briefly-elevated action - see
/// docs/SAFETY_MODEL.md.
/// </summary>
public sealed class StartupAppManager
{
    private readonly IElevationService _elevationService;
    private readonly IAuditLogger _auditLogger;

    public StartupAppManager(IElevationService elevationService, IAuditLogger auditLogger)
    {
        _elevationService = elevationService;
        _auditLogger = auditLogger;
    }

    public async Task<ActionResult> SetEnabledAsync(StartupAppInfo app, bool enable, CancellationToken cancellationToken = default)
    {
        var request = new StartupToggleRequest
        {
            Location = app.Location,
            Name = app.Name,
            CommandLine = app.CommandLine,
            Enable = enable
        };

        ActionResult result;
        bool ranElevated;

        if (app.RequiresAdminToDisable)
        {
            ranElevated = true;
            var argument = JsonSerializer.Serialize(request);
            var elevatedResult = await _elevationService.RunElevatedActionAsync(ElevatedActionKeys.ToggleStartupEntry, argument, cancellationToken).ConfigureAwait(false);

            result = elevatedResult.Success
                ? ActionResult.Ok(elevatedResult.Output ?? "Startup entry updated")
                : ActionResult.Fail(elevatedResult.Error ?? "Elevated action failed");
        }
        else
        {
            ranElevated = false;
            result = StartupEntryMutator.Apply(request);
        }

        await _auditLogger.LogAsync(new AuditLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            ActionId = OptimizationActionIds.DisableStartupApp,
            Description = $"{(enable ? "Enable" : "Disable")} startup entry '{app.Name}' ({app.Location})",
            PreviousValue = enable ? "disabled" : "enabled",
            NewValue = enable ? "enabled" : "disabled",
            Success = result.Success,
            ErrorMessage = result.Success ? null : result.Message,
            RanElevated = ranElevated
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }
}
