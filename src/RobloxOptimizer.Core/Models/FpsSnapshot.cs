namespace RobloxOptimizer.Core.Models;

/// <summary>
/// Frame-rate readings for the currently-monitored Roblox process.
/// See docs/ARCHITECTURE.md "FPS measurement" for why this can be unavailable.
/// </summary>
public sealed class FpsSnapshot
{
    public required Measurement<double> Current { get; init; }
    public required Measurement<double> Average { get; init; }
    public required Measurement<double> Minimum { get; init; }
    public required Measurement<double> Maximum { get; init; }
    public required DateTimeOffset SampledAtUtc { get; init; }

    public static FpsSnapshot Unavailable(string reason) => new()
    {
        Current = Measurement<double>.Unavailable(reason),
        Average = Measurement<double>.Unavailable(reason),
        Minimum = Measurement<double>.Unavailable(reason),
        Maximum = Measurement<double>.Unavailable(reason),
        SampledAtUtc = DateTimeOffset.UtcNow
    };
}
