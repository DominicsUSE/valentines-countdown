using System.Management;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Backup;

/// <summary>
/// Best-effort Windows System Restore point creation, used once before the
/// first optimization is ever applied. Creating a restore point requires
/// administrator rights (a Windows/WMI restriction, not RPO's choice), so
/// this goes through the briefly-elevated helper process pattern described
/// in docs/ARCHITECTURE.md rather than requiring the whole app to run
/// elevated. Windows also limits automatic restore points to roughly one
/// per 24 hours and restore points can be disabled entirely on a system -
/// both are surfaced to the user rather than treated as a hard failure,
/// since the JSON settings backup (<see cref="BackupService"/>) is the
/// primary, always-available reversibility mechanism.
/// </summary>
public sealed class RestorePointService
{
    private readonly IElevationService _elevationService;

    public RestorePointService(IElevationService elevationService)
    {
        _elevationService = elevationService;
    }

    public async Task<ActionResult> TryCreateRestorePointAsync(string description, CancellationToken cancellationToken = default)
    {
        if (_elevationService.IsCurrentProcessElevated)
        {
            return CreateRestorePointDirect(description);
        }

        var elevatedResult = await _elevationService
            .RunElevatedActionAsync(ElevatedActionKeys.CreateRestorePoint, description, cancellationToken)
            .ConfigureAwait(false);

        return elevatedResult.Success
            ? ActionResult.Ok(elevatedResult.Output ?? "System Restore point created")
            : ActionResult.Fail(elevatedResult.Error ?? "Could not create a System Restore point");
    }

    /// <summary>
    /// The actual WMI call. Public so the briefly-elevated helper process can
    /// invoke it directly once it already has an elevated token - see
    /// docs/ARCHITECTURE.md "Elevation model".
    /// </summary>
    public static ActionResult CreateRestorePointDirect(string description)
    {
        try
        {
            using var restoreClass = new ManagementClass(@"root\default", "SystemRestore", null);
            using var inParams = restoreClass.GetMethodParameters("CreateRestorePoint");

            inParams["Description"] = description;
            inParams["RestorePointType"] = 12; // APPLICATION_INSTALL - closest documented type for a utility making settings changes.
            inParams["EventType"] = 100; // BEGIN_SYSTEM_CHANGE

            using var outParams = restoreClass.InvokeMethod("CreateRestorePoint", inParams, null);
            var returnValue = outParams is null ? -1 : Convert.ToInt32(outParams["ReturnValue"]);

            return returnValue == 0
                ? ActionResult.Ok("System Restore point created")
                : ActionResult.Fail(
                    $"System Restore point creation returned code {returnValue}. Windows only allows one automatic restore point " +
                    "per 24 hours, or System Restore may be turned off for this drive - your settings are still protected by RPO's own backup file.");
        }
        catch (Exception ex)
        {
            return ActionResult.Fail(
                "Could not create a System Restore point - your settings are still protected by RPO's own backup file.",
                ex.Message);
        }
    }
}
