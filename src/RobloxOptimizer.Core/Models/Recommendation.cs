using RobloxOptimizer.Core.Enums;

namespace RobloxOptimizer.Core.Models;

/// <summary>
/// A single, prioritized suggestion produced by the recommendation engine after
/// a diagnostic pass. Every field required by the app's transparency rules is
/// mandatory here - there is no way to construct a recommendation that hides
/// side effects or the admin requirement.
/// </summary>
public sealed class Recommendation
{
    /// <summary>Stable key identifying which <c>IOptimizationAction</c> this recommendation maps to, or null if it is guidance-only (no automatable action).</summary>
    public string? ActionId { get; init; }

    public required string Title { get; init; }
    public required LagCause Cause { get; init; }
    public required string DetectedProblem { get; init; }
    public required string ProposedChange { get; init; }
    public required string ExpectedBenefit { get; init; }
    public required string PossibleSideEffects { get; init; }
    public required bool RequiresAdmin { get; init; }

    /// <summary>Lower number = higher priority.</summary>
    public required int Priority { get; init; }

    /// <summary>False for guidance the user must apply manually (e.g. "connect via Ethernet"), true if RPO can Apply/Undo it.</summary>
    public required bool IsAutomatable { get; init; }
}
