namespace RobloxOptimizer.Core.Models;

/// <summary>
/// Everything gathered by a single "Analyze My PC" pass, bundled together so
/// the recommendation engine and the dashboard bind to one consistent object.
/// </summary>
public sealed class DiagnosticsSnapshot
{
    public required SystemSnapshot System { get; init; }
    public required TemperatureSnapshot Temperature { get; init; }
    public required FpsSnapshot Fps { get; init; }
    public required NetworkSnapshot Network { get; init; }
    public required RobloxStatus Roblox { get; init; }
    public required int DetectedGpuCount { get; init; }
    public required IReadOnlyList<BackgroundAppInfo> BackgroundApps { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
