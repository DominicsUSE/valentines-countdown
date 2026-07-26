using System.Text.Json;
using Microsoft.Win32;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization.Actions;

/// <summary>
/// Disables the Xbox Game Bar / Game DVR background capture overlay, which
/// can cost frame rate and briefly spike CPU/GPU usage when triggered
/// mid-game. Uses the two documented, per-user (HKCU) registry values behind
/// Settings > Gaming > Xbox Game Bar - the same switch that Settings UI
/// itself flips, so no admin rights are needed and nothing undocumented is
/// touched. Third-party overlays (Discord, GPU vendor overlays) are not
/// modified here - those live in each vendor's own app settings and are
/// surfaced as guidance instead (see RecommendationEngine).
/// </summary>
public sealed class OverlayToggleAction : IOptimizationAction
{
    private const string GameDvrPolicyKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR";
    private const string GameDvrPolicyValueName = "AppCaptureEnabled";

    private const string GameConfigStoreKeyPath = @"System\GameConfigStore";
    private const string GameConfigStoreValueName = "GameDVR_Enabled";

    public string Id => OptimizationActionIds.DisableOverlays;

    public string DisplayName => "Disable Xbox Game Bar / Game DVR overlay";

    public string Description =>
        "Turns off Windows' built-in Game Bar/Game DVR background recording overlay for your account, which can momentarily use CPU/GPU/RAM " +
        "while you play. This is the same setting as Settings > Gaming > Xbox Game Bar - it only affects your Windows user account.";

    public bool RequiresAdmin => false;

    public Task<string?> CaptureCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var state = new OverlayState
        {
            AppCaptureEnabled = ReadDwordOrNull(GameDvrPolicyKeyPath, GameDvrPolicyValueName),
            GameDvrEnabled = ReadDwordOrNull(GameConfigStoreKeyPath, GameConfigStoreValueName)
        };

        return Task.FromResult<string?>(JsonSerializer.Serialize(state));
    }

    public Task<ActionResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            WriteDword(GameDvrPolicyKeyPath, GameDvrPolicyValueName, 0);
            WriteDword(GameConfigStoreKeyPath, GameConfigStoreValueName, 0);
            return Task.FromResult(ActionResult.Ok("Xbox Game Bar / Game DVR overlay disabled for this Windows account"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionResult.Fail("Could not disable the Game Bar overlay", ex.Message));
        }
    }

    public Task<ActionResult> UndoAsync(string? previousState, CancellationToken cancellationToken = default)
    {
        try
        {
            var state = string.IsNullOrWhiteSpace(previousState)
                ? new OverlayState()
                : JsonSerializer.Deserialize<OverlayState>(previousState) ?? new OverlayState();

            RestoreDwordOrRemove(GameDvrPolicyKeyPath, GameDvrPolicyValueName, state.AppCaptureEnabled);
            RestoreDwordOrRemove(GameConfigStoreKeyPath, GameConfigStoreValueName, state.GameDvrEnabled);

            return Task.FromResult(ActionResult.Ok("Xbox Game Bar / Game DVR overlay setting restored"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionResult.Fail("Could not restore the Game Bar overlay setting", ex.Message));
        }
    }

    private static int? ReadDwordOrNull(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        var raw = key?.GetValue(valueName);
        return raw is int intValue ? intValue : null;
    }

    private static void WriteDword(string keyPath, string valueName, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }

    private static void RestoreDwordOrRemove(string keyPath, string valueName, int? previousValue)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);

        if (previousValue is int value)
        {
            key.SetValue(valueName, value, RegistryValueKind.DWord);
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private sealed class OverlayState
    {
        public int? AppCaptureEnabled { get; set; }
        public int? GameDvrEnabled { get; set; }
    }
}
