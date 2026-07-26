namespace RobloxOptimizer.Core.Models;

/// <summary>
/// Whether Roblox is currently running, and which executable/process was found.
/// </summary>
public sealed class RobloxStatus
{
    public required bool IsRunning { get; init; }
    public string? ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public int? ProcessId { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }

    public static RobloxStatus NotRunning { get; } = new() { IsRunning = false };
}
