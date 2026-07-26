namespace RobloxOptimizer.Core.Models;

/// <summary>
/// One line of the local, append-only audit log. Every setting RPO ever
/// changes produces exactly one of these, whether the change succeeded or not.
/// </summary>
public sealed class AuditLogEntry
{
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string ActionId { get; init; }
    public required string Description { get; init; }
    public string? PreviousValue { get; init; }
    public string? NewValue { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public required bool RanElevated { get; init; }
}
