namespace RobloxOptimizer.Core.Models;

/// <summary>
/// Outcome of applying, undoing, or capturing the state of an optimization action.
/// </summary>
public sealed class ActionResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public bool ElevationRequiredButMissing { get; init; }
    public string? ErrorDetail { get; init; }

    public static ActionResult Ok(string message) => new() { Success = true, Message = message };

    public static ActionResult Fail(string message, string? errorDetail = null) =>
        new() { Success = false, Message = message, ErrorDetail = errorDetail };

    public static ActionResult NeedsElevation(string message) =>
        new() { Success = false, Message = message, ElevationRequiredButMissing = true };
}
