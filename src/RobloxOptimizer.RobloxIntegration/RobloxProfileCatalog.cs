using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Enums;

namespace RobloxOptimizer.RobloxIntegration;

/// <summary>The three built-in guidance profiles offered on the Roblox tab.</summary>
public static class RobloxProfileCatalog
{
    public static IReadOnlyList<RobloxProfileDefinition> All { get; } = new List<RobloxProfileDefinition>
    {
        new()
        {
            Profile = OptimizationProfile.MaximumFps,
            Title = "Maximum FPS",
            Description = "Prioritizes frame rate and responsiveness over visual fidelity. Best when you're seeing low or unstable FPS.",
            RecommendedOsActionIds = new List<string>
            {
                OptimizationActionIds.PowerPlanHighPerformance,
                OptimizationActionIds.DisableOverlays,
                OptimizationActionIds.GpuPreferenceHighPerformance,
                OptimizationActionIds.ProcessPriorityAboveNormal
            },
            InRobloxInstructions = new List<string>
            {
                "Open Settings (Esc menu) in Roblox and drag the Graphics Quality slider all the way down, or turn off 'Graphics Quality: Automatic' and set a low fixed level.",
                "Turn off Roblox's built-in Camera/Motion effects if the experience exposes such a toggle.",
                "Close other 3D-heavy browser tabs or apps that share your GPU while you play."
            }
        },
        new()
        {
            Profile = OptimizationProfile.Balanced,
            Title = "Balanced",
            Description = "A middle ground: cleans up background overlays and idle-priority contention without pushing graphics to the minimum.",
            RecommendedOsActionIds = new List<string>
            {
                OptimizationActionIds.PowerPlanHighPerformance,
                OptimizationActionIds.DisableOverlays,
                OptimizationActionIds.GpuPreferenceHighPerformance
            },
            InRobloxInstructions = new List<string>
            {
                "In Settings, set Graphics Quality to Automatic, or a mid fixed level (around 5-7) if you prefer manual control.",
            }
        },
        new()
        {
            Profile = OptimizationProfile.VisualQuality,
            Title = "Visual Quality",
            Description = "Keeps visuals high. Only applies safe background/overlay cleanup - does not force a particular power plan or priority.",
            RecommendedOsActionIds = new List<string>
            {
                OptimizationActionIds.DisableOverlays
            },
            InRobloxInstructions = new List<string>
            {
                "In Settings, set Graphics Quality to a high fixed level, or turn on 'Graphics Quality: Automatic' if your system can sustain it.",
                "Performance still depends on the specific experience and its current server - some places are simply more demanding than others."
            }
        }
    };

    public static RobloxProfileDefinition Get(OptimizationProfile profile) =>
        All.First(p => p.Profile == profile);
}
