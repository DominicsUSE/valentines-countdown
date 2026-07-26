using System.Diagnostics;
using System.Text.RegularExpressions;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization.Actions;

/// <summary>
/// Switches the active Windows power plan to a high-performance scheme while
/// gaming, and restores the plan that was active beforehand. Selecting an
/// existing power scheme does not require administrator rights.
/// </summary>
public sealed class PowerPlanAction : IOptimizationAction
{
    private static readonly Regex SchemeLineRegex = new(
        @"Power Scheme GUID:\s*([0-9a-fA-F-]{36})\s*\((.+?)\)(\s*\*)?",
        RegexOptions.Compiled);

    public string Id => OptimizationActionIds.PowerPlanHighPerformance;

    public string DisplayName => "Switch to a high-performance power plan";

    public string Description =>
        "Switches Windows' active power plan to 'High performance' (or a similar plan already available on your PC) while you play, " +
        "then restores whatever plan was active before. This stops Windows from deliberately slowing your CPU down to save battery/power.";

    public bool RequiresAdmin => false;

    public async Task<string?> CaptureCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var schemes = await ListSchemesAsync(cancellationToken).ConfigureAwait(false);
        var active = schemes.FirstOrDefault(s => s.IsActive);
        return active.Guid; // null/empty if we couldn't determine it - Undo will then no-op safely.
    }

    public async Task<ActionResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var schemes = await ListSchemesAsync(cancellationToken).ConfigureAwait(false);

        var highPerformance = schemes.FirstOrDefault(s => s.Name.Contains("high performance", StringComparison.OrdinalIgnoreCase))
            ?? schemes.FirstOrDefault(s => s.Name.Contains("ultimate performance", StringComparison.OrdinalIgnoreCase));

        if (highPerformance is null)
        {
            return ActionResult.Fail(
                "No high-performance power plan is available on this PC. Some laptop manufacturers replace it with their own plan - " +
                "you can create one manually via Control Panel > Power Options > Create a power plan.");
        }

        var result = await RunPowercfgAsync($"/setactive {highPerformance.Guid}", cancellationToken).ConfigureAwait(false);

        return result.Success
            ? ActionResult.Ok($"Switched to '{highPerformance.Name}'")
            : result;
    }

    public async Task<ActionResult> UndoAsync(string? previousState, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previousState))
        {
            return ActionResult.Ok("No previous power plan was recorded - nothing to restore");
        }

        return await RunPowercfgAsync($"/setactive {previousState}", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<(string Guid, string Name, bool IsActive)>> ListSchemesAsync(CancellationToken cancellationToken)
    {
        var output = await RunPowercfgForOutputAsync("/list", cancellationToken).ConfigureAwait(false);
        if (output is null)
        {
            return Array.Empty<(string, string, bool)>();
        }

        var schemes = new List<(string, string, bool)>();
        foreach (Match match in SchemeLineRegex.Matches(output))
        {
            var guid = match.Groups[1].Value;
            var name = match.Groups[2].Value.Trim();
            var isActive = match.Groups[3].Success;
            schemes.Add((guid, name, isActive));
        }

        return schemes;
    }

    private static async Task<string?> RunPowercfgForOutputAsync(string arguments, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return output;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<ActionResult> RunPowercfgAsync(string arguments, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return ActionResult.Fail("Could not start powercfg");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0
                ? ActionResult.Ok("powercfg " + arguments + " succeeded")
                : ActionResult.Fail("powercfg " + arguments + " failed with exit code " + process.ExitCode);
        }
        catch (Exception ex)
        {
            return ActionResult.Fail("Failed to run powercfg", ex.Message);
        }
    }
}
