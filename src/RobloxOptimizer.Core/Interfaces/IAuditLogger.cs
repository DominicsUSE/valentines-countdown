using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Core.Interfaces;

/// <summary>Append-only local audit trail of every setting RPO has changed.</summary>
public interface IAuditLogger
{
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken = default);
}
