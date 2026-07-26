using System.IO;
using System.Text.Json;
using RobloxOptimizer.Backup;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Models;
using RobloxOptimizer.Optimization;

namespace RobloxOptimizer.App.Elevation;

/// <summary>
/// The complete whitelist dispatch table for actions allowed to run with an
/// elevated token - see docs/SAFETY_MODEL.md "Administrator rights - action
/// by action". Nothing outside this switch ever runs elevated.
/// </summary>
public static class ElevatedActionRunner
{
    public static ElevatedActionResult Execute(string actionKey, string? argument)
    {
        try
        {
            switch (actionKey)
            {
                case ElevatedActionKeys.CreateRestorePoint:
                    return RunCreateRestorePoint(argument);

                case ElevatedActionKeys.ToggleStartupEntry:
                    return RunToggleStartupEntry(argument);

                default:
                    return ElevatedActionResult.Fail($"Unknown elevated action '{actionKey}' - refusing to run anything not on the whitelist");
            }
        }
        catch (Exception ex)
        {
            return ElevatedActionResult.Fail("Elevated action failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Entry point used when this process was relaunched specifically to run
    /// one elevated action (see App.xaml.cs and ElevationService). Writes
    /// the result to <paramref name="resultFilePath"/> for the original,
    /// non-elevated process to read, and returns a process exit code.
    /// </summary>
    public static int RunAndWriteResult(string actionKey, string? argument, string resultFilePath)
    {
        var result = Execute(actionKey, argument);

        try
        {
            File.WriteAllText(resultFilePath, JsonSerializer.Serialize(result));
        }
        catch (Exception)
        {
            // If we can't even write the result file, the caller will time out waiting and report a generic failure.
        }

        return result.Success ? 0 : 1;
    }

    private static ElevatedActionResult RunCreateRestorePoint(string? argument)
    {
        var description = string.IsNullOrWhiteSpace(argument) ? "Roblox Performance Optimizer" : argument;
        var result = RestorePointService.CreateRestorePointDirect(description);
        return result.Success ? ElevatedActionResult.Ok(result.Message) : ElevatedActionResult.Fail(result.Message);
    }

    private static ElevatedActionResult RunToggleStartupEntry(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return ElevatedActionResult.Fail("Missing startup-entry argument");
        }

        var request = JsonSerializer.Deserialize<StartupToggleRequest>(argument);
        if (request is null)
        {
            return ElevatedActionResult.Fail("Invalid startup-entry argument");
        }

        var result = StartupEntryMutator.Apply(request);
        return result.Success ? ElevatedActionResult.Ok(result.Message) : ElevatedActionResult.Fail(result.Message);
    }
}
