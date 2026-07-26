namespace RobloxOptimizer.Core.Models;

/// <summary>
/// A single reversible change captured before an optimization action ran, so
/// it can be restored later even across app restarts.
/// </summary>
public sealed class BackupEntry
{
    public required string ActionId { get; init; }
    public required string Key { get; init; }
    public required string? PreviousValue { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
}

/// <summary>
/// The full set of backed-up settings RPO can restore. Persisted as JSON under
/// <c>%LOCALAPPDATA%\RobloxOptimizer\backup.json</c>. "Restore Defaults" replays
/// every entry in <see cref="Entries"/>.
/// </summary>
public sealed class BackupSnapshot
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required List<BackupEntry> Entries { get; init; } = new();

    public static BackupSnapshot CreateEmpty() => new()
    {
        Id = Guid.NewGuid(),
        CreatedUtc = DateTimeOffset.UtcNow,
        Entries = new List<BackupEntry>()
    };
}
