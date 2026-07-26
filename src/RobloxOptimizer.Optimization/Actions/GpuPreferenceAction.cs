using Microsoft.Win32;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization.Actions;

/// <summary>
/// Sets Roblox to prefer the high-performance (dedicated) GPU on systems
/// with more than one graphics adapter, using Windows' own documented
/// per-application GPU preference feature (the same storage location that
/// Settings > System > Display > Graphics writes to). No admin rights are
/// required - it is a per-user (HKCU) setting.
///
/// Because Roblox auto-updates into a new "Versions\&lt;hash&gt;" folder, this
/// preference is tied to the executable path it was applied to and may need
/// to be re-applied (just click Optimize again) after Roblox updates itself.
/// </summary>
public sealed class GpuPreferenceAction : IOptimizationAction
{
    private const string KeyPath = @"SOFTWARE\Microsoft\DirectX\UserGpuPreferences";
    private const string HighPerformanceValue = "GpuPreference=2;";

    private readonly IRobloxExecutableLocator _executableLocator;

    public GpuPreferenceAction(IRobloxExecutableLocator executableLocator)
    {
        _executableLocator = executableLocator;
    }

    public string Id => OptimizationActionIds.GpuPreferenceHighPerformance;

    public string DisplayName => "Use the dedicated GPU for Roblox";

    public string Description =>
        "On laptops or PCs with both an integrated and a dedicated graphics card, tells Windows to always run RobloxPlayerBeta.exe on the " +
        "dedicated (higher-performance) GPU instead of letting Windows choose. No effect on single-GPU systems.";

    public bool RequiresAdmin => false;

    public Task<string?> CaptureCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var exePath = _executableLocator.FindExecutablePath();
        if (exePath is null)
        {
            return Task.FromResult<string?>(null);
        }

        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var existing = key?.GetValue(exePath) as string;

        // Encode both the path we captured for and its previous value, so Undo
        // still works correctly even if Roblox updates to a new path between Apply and Undo.
        return Task.FromResult<string?>(exePath + " " + (existing ?? string.Empty));
    }

    public Task<ActionResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var exePath = _executableLocator.FindExecutablePath();
        if (exePath is null)
        {
            return Task.FromResult(ActionResult.Fail(
                "Could not locate RobloxPlayerBeta.exe. Launch Roblox at least once, then try Optimize again."));
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            key.SetValue(exePath, HighPerformanceValue, RegistryValueKind.String);
            return Task.FromResult(ActionResult.Ok($"Roblox will now use the dedicated GPU ({exePath})"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionResult.Fail("Could not set the GPU preference", ex.Message));
        }
    }

    public Task<ActionResult> UndoAsync(string? previousState, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previousState))
        {
            return Task.FromResult(ActionResult.Ok("No previous GPU preference was recorded - nothing to restore"));
        }

        var parts = previousState.Split(' ', 2);
        var exePath = parts[0];
        var previousValue = parts.Length > 1 ? parts[1] : string.Empty;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);

            if (string.IsNullOrEmpty(previousValue))
            {
                key.DeleteValue(exePath, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(exePath, previousValue, RegistryValueKind.String);
            }

            return Task.FromResult(ActionResult.Ok("GPU preference restored"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionResult.Fail("Could not restore the GPU preference", ex.Message));
        }
    }
}
