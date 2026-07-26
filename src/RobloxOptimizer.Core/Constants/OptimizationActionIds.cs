namespace RobloxOptimizer.Core.Constants;

/// <summary>
/// Stable string identifiers for each <c>IOptimizationAction</c> implementation.
/// Shared between <c>RobloxOptimizer.Optimization</c> (which implements the
/// actions) and <c>RobloxOptimizer.RobloxIntegration</c> (which references
/// them by ID from guidance profiles) so the two projects don't need a
/// circular reference just to agree on a key name.
/// </summary>
public static class OptimizationActionIds
{
    public const string PowerPlanHighPerformance = "power-plan-high-performance";
    public const string DisableOverlays = "disable-overlays";
    public const string GpuPreferenceHighPerformance = "gpu-preference-high-performance";
    public const string ProcessPriorityAboveNormal = "process-priority-above-normal";
    public const string CloseBackgroundApps = "close-background-apps";
    public const string DisableStartupApp = "disable-startup-app";
}
