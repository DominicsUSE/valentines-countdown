namespace RobloxOptimizer.Core.Enums;

/// <summary>
/// A named bundle of guidance/settings the user can pick. Profiles only ever
/// touch OS-level settings and present in-Roblox instructions - they never
/// modify Roblox's own files or FastFlags.
/// </summary>
public enum OptimizationProfile
{
    /// <summary>Prioritize frame rate above all else.</summary>
    MaximumFps,

    /// <summary>A middle ground between smoothness and visual quality.</summary>
    Balanced,

    /// <summary>Prioritize visual fidelity; only applies safe background/overlay cleanup.</summary>
    VisualQuality
}
