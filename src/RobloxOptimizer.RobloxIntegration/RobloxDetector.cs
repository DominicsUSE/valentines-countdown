using System.Diagnostics;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.RobloxIntegration;

/// <summary>
/// Detects whether RobloxPlayerBeta.exe is running by polling
/// <see cref="Process.GetProcessesByName"/>. This is read-only process
/// enumeration - RPO never opens a handle into Roblox's memory or attaches
/// a debugger.
/// </summary>
public sealed class RobloxDetector : IRobloxDetector
{
    private const string RobloxProcessName = "RobloxPlayerBeta";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly object _lock = new();
    private CancellationTokenSource? _pollLoopCts;
    private Task? _pollLoopTask;
    private bool _wasRunning;
    private bool _disposed;

    public event EventHandler<RobloxStatus>? RobloxLaunched;
    public event EventHandler? RobloxExited;

    public RobloxStatus GetCurrentStatus()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(RobloxProcessName);
        }
        catch (Exception)
        {
            return RobloxStatus.NotRunning;
        }

        try
        {
            var process = processes.FirstOrDefault();
            if (process is null)
            {
                return RobloxStatus.NotRunning;
            }

            string? path = null;
            DateTimeOffset? startTime = null;

            try { path = process.MainModule?.FileName; }
            catch (Exception) { /* Access to another user's process module info can be denied; leave path null. */ }

            try { startTime = process.StartTime; }
            catch (Exception) { /* Best effort only. */ }

            return new RobloxStatus
            {
                IsRunning = true,
                ProcessName = process.ProcessName,
                ExecutablePath = path,
                ProcessId = process.Id,
                StartedAtUtc = startTime?.ToUniversalTime()
            };
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_pollLoopTask is not null)
            {
                return;
            }

            _pollLoopCts = new CancellationTokenSource();
            var token = _pollLoopCts.Token;
            _pollLoopTask = Task.Run(() => PollLoopAsync(token), CancellationToken.None);
        }
    }

    public void StopMonitoring()
    {
        lock (_lock)
        {
            _pollLoopCts?.Cancel();
            _pollLoopCts?.Dispose();
            _pollLoopCts = null;
            _pollLoopTask = null;
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var status = GetCurrentStatus();

                if (status.IsRunning && !_wasRunning)
                {
                    _wasRunning = true;
                    RobloxLaunched?.Invoke(this, status);
                }
                else if (!status.IsRunning && _wasRunning)
                {
                    _wasRunning = false;
                    RobloxExited?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                // A polling loop must never crash the app - just try again next tick.
            }

            try
            {
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopMonitoring();
    }
}
