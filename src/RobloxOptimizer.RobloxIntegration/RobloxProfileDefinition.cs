using RobloxOptimizer.Core.Enums;

namespace RobloxOptimizer.RobloxIntegration;

/// <summary>
/// Describes one of the three guidance profiles. A profile is just a bundle
/// of (a) safe OS-level action IDs the orchestrator can apply/undo and
/// (b) plain-language instructions for settings the user must change inside
/// Roblox itself - RPO cannot and does not change Roblox's own graphics
/// settings, FastFlags, or files.
/// </summary>
public sealed class RobloxProfileDefinition
{
    public required OptimizationProfile Profile { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> RecommendedOsActionIds { get; init; }
    public required IReadOnlyList<string> InRobloxInstructions { get; init; }
}
