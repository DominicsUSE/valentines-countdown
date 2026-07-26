using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

/// <summary>Persists the JSON settings-backup snapshot used by Undo/Restore Defaults.</summary>
public interface IBackupService
{
    Task<BackupSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(BackupSnapshot snapshot, CancellationToken cancellationToken = default);

    Task RecordEntryAsync(string actionId, string key, string? previousValue, CancellationToken cancellationToken = default);

    Task<BackupEntry?> GetEntryAsync(string actionId, string key, CancellationToken cancellationToken = default);

    Task RemoveEntryAsync(string actionId, string key, CancellationToken cancellationToken = default);
}
