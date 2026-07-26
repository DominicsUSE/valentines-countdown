namespace RobloxOptimizer.Core.Models;

/// <summary>
/// A before/after comparison for a single metric, shown after "Apply
/// Recommended Fixes" re-runs diagnostics. Only ever built from two real
/// measurements - if either side is unavailable, the comparison is not shown.
/// </summary>
public sealed class BenchmarkResult
{
    public required string MetricName { get; init; }
    public required string Unit { get; init; }
    public required Measurement<double> Before { get; init; }
    public required Measurement<double> After { get; init; }

    public double? ChangeAmount => Before.IsAvailable && After.IsAvailable ? After.Value - Before.Value : null;
}
