using RobloxOptimizer.Core.Enums;

namespace RobloxOptimizer.Core.Models;

/// <summary>Output of the recommendation engine for one diagnostic pass.</summary>
public sealed class DiagnosticsResult
{
    public required LagCause DetectedCauses { get; init; }

    /// <summary>Plain-language explanation of whether this looks like FPS lag, network lag, both, or neither.</summary>
    public required string LagExplanation { get; init; }

    public required IReadOnlyList<Recommendation> Recommendations { get; init; }
}
