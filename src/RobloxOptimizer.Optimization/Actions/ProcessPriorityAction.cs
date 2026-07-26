using System.Diagnostics;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization.Actions;

/// <summary>
/// Raises RobloxPlayerBeta.exe's process priority to Above Normal (never
/// Realtime - Realtime priority can starve mouse/keyboard/network drivers
/// and destabilize the whole system). Priority is inherently temporary:
/// Windows resets a process back to Normal priority automatically the
/// moment it exits, so there is nothing left to restore once Roblox closes.
/// </summary>
public sealed class ProcessPriorityAction : IOptimizationAction
{
    private const string RobloxProcessName = "RobloxPlayerBeta";

    public string Id => OptimizationActionIds.ProcessPriorityAboveNormal;

    public string DisplayName => "Raise Roblox's process priority";

    public string Description =>
        "Raises RobloxPlayerBeta.exe's CPU scheduling priority from Normal to Above Normal, so Windows favors it slightly over other " +
        "background processes when the CPU is busy. Never set to Realtime, which can destabilize the whole system.";

    public bool RequiresAdmin => false;

    public Task<string?> CaptureCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        // Priority does not persist across process restarts, so there is
        // nothing meaningful to snapshot beyond "was not yet raised by us".
        return Task.FromResult<string?>(null);
    }

    public Task<ActionResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WithRobloxProcess(process =>
        {
            process.PriorityClass = ProcessPriorityClass.AboveNormal;
            return ActionResult.Ok("Roblox's process priority raised to Above Normal");
        }));
    }

    public Task<ActionResult> UndoAsync(string? previousState, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WithRobloxProcess(
            process =>
            {
                process.PriorityClass = ProcessPriorityClass.Normal;
                return ActionResult.Ok("Roblox's process priority restored to Normal");
            },
            notRunningResult: ActionResult.Ok("Roblox has already closed - its priority was reset automatically by Windows")));
    }

    private static ActionResult WithRobloxProcess(Func<Process, ActionResult> action, ActionResult? notRunningResult = null)
    {
        var processes = Process.GetProcessesByName(RobloxProcessName);
        try
        {
            var process = processes.FirstOrDefault();
            if (process is null)
            {
                return notRunningResult ?? ActionResult.Fail("Roblox is not currently running");
            }

            return action(process);
        }
        catch (Exception ex)
        {
            return ActionResult.Fail("Could not change Roblox's process priority", ex.Message);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
