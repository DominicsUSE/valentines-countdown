using System.Text.Json;
using RobloxOptimizer.Core;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Logging;

/// <summary>
/// Append-only audit log stored as one JSON object per line at
/// <c>%LOCALAPPDATA%\RobloxOptimizer\audit.log</c>. Never transmitted
/// anywhere - this is purely a local record the user (or the user's own
/// support request) can inspect to see exactly what RPO changed and when.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureRootFolderExists();
        var json = JsonSerializer.Serialize(entry, SerializerOptions);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(AppPaths.AuditLogFilePath, json + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(AppPaths.AuditLogFilePath))
        {
            return Array.Empty<AuditLogEntry>();
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(AppPaths.AuditLogFilePath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        var entries = new List<AuditLogEntry>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<AuditLogEntry>(line, SerializerOptions);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // Skip a corrupted line rather than fail the whole read.
            }
        }

        return entries
            .OrderByDescending(e => e.TimestampUtc)
            .Take(count)
            .ToList();
    }
}
