using System.Text.Json;
using RobloxOptimizer.Core;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Backup;

/// <summary>
/// Persists the JSON settings-backup snapshot at
/// <c>%LOCALAPPDATA%\RobloxOptimizer\backup.json</c>, which powers
/// Undo/Restore Defaults and survives an app restart.
/// </summary>
public sealed class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<BackupSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(BackupSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveUnlockedAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RecordEntryAsync(string actionId, string key, string? previousValue, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            snapshot.Entries.RemoveAll(e => e.ActionId == actionId && e.Key == key);
            snapshot.Entries.Add(new BackupEntry
            {
                ActionId = actionId,
                Key = key,
                PreviousValue = previousValue,
                CapturedAtUtc = DateTimeOffset.UtcNow
            });

            await SaveUnlockedAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<BackupEntry?> GetEntryAsync(string actionId, string key, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            return snapshot.Entries.FirstOrDefault(e => e.ActionId == actionId && e.Key == key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveEntryAsync(string actionId, string key, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            snapshot.Entries.RemoveAll(e => e.ActionId == actionId && e.Key == key);
            await SaveUnlockedAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<BackupSnapshot> LoadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.BackupFilePath))
        {
            return BackupSnapshot.CreateEmpty();
        }

        try
        {
            await using var stream = File.OpenRead(AppPaths.BackupFilePath);
            var snapshot = await JsonSerializer.DeserializeAsync<BackupSnapshot>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            return snapshot ?? BackupSnapshot.CreateEmpty();
        }
        catch (JsonException)
        {
            // A corrupted backup file must never crash the app or block Optimize/Undo - start fresh.
            return BackupSnapshot.CreateEmpty();
        }
    }

    private static async Task SaveUnlockedAsync(BackupSnapshot snapshot, CancellationToken cancellationToken)
    {
        AppPaths.EnsureRootFolderExists();
        var tempPath = AppPaths.BackupFilePath + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, AppPaths.BackupFilePath, overwrite: true);
    }
}
