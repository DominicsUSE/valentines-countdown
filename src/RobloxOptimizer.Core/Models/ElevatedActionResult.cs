namespace RobloxOptimizer.Core.Models;

/// <summary>Result of a narrow, whitelisted action run in a briefly-elevated helper process.</summary>
public sealed class ElevatedActionResult
{
    public required bool Success { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }

    public static ElevatedActionResult Ok(string? output = null) => new() { Success = true, Output = output };
    public static ElevatedActionResult Fail(string error) => new() { Success = false, Error = error };
}
