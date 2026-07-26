using System.Diagnostics;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization;

/// <summary>
/// Closes a specific, user-selected background process. Tries a graceful
/// window-close first and only force-terminates if that doesn't work within
/// a short grace period. There is no "undo" for closing an application -
/// the user simply reopens it when they're done playing; every attempt is
/// still recorded in the audit log.
/// <see cref="ProtectedProcesses"/> is enforced here as a hard backstop
/// regardless of what the caller passes in.
/// </summary>
public sealed class BackgroundAppCloser
{
    private static readonly TimeSpan GracefulCloseTimeout = TimeSpan.FromSeconds(3);

    private readonly IAuditLogger _auditLogger;

    public BackgroundAppCloser(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public async Task<ActionResult> CloseAsync(BackgroundAppInfo app, CancellationToken cancellationToken = default)
    {
        if (ProtectedProcesses.IsProtected(app.ProcessName))
        {
            var blocked = ActionResult.Fail($"'{app.ProcessName}' is a protected system/security process and cannot be closed by RPO");
            await LogAsync(app, blocked, cancellationToken).ConfigureAwait(false);
            return blocked;
        }

        ActionResult result;

        try
        {
            using var process = Process.GetProcessById(app.ProcessId);

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                process.CloseMainWindow();
                var exitedGracefully = process.WaitForExit((int)GracefulCloseTimeout.TotalMilliseconds);

                if (!exitedGracefully)
                {
                    process.Kill();
                }
            }
            else
            {
                process.Kill();
            }

            result = ActionResult.Ok($"Closed '{app.ProcessName}' (PID {app.ProcessId}). Reopen it yourself if you need it again.");
        }
        catch (ArgumentException)
        {
            result = ActionResult.Ok($"'{app.ProcessName}' had already exited");
        }
        catch (Exception ex)
        {
            result = ActionResult.Fail($"Could not close '{app.ProcessName}'", ex.Message);
        }

        await LogAsync(app, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private Task LogAsync(BackgroundAppInfo app, ActionResult result, CancellationToken cancellationToken)
    {
        return _auditLogger.LogAsync(new AuditLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            ActionId = OptimizationActionIds.CloseBackgroundApps,
            Description = $"Close background app '{app.ProcessName}' (PID {app.ProcessId}) - not reversible, reopen manually",
            PreviousValue = "running",
            NewValue = result.Success ? "closed" : "running",
            Success = result.Success,
            ErrorMessage = result.Success ? null : result.Message,
            RanElevated = false
        }, cancellationToken);
    }
}
