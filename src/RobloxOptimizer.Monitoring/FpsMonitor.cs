using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Monitoring;

/// <summary>
/// Best-effort per-process frame rate estimator. Uses the same technique as
/// tools like PresentMon/RTSS: it subscribes to the OS's own
/// "Microsoft-Windows-DXGI" ETW trace provider and measures the time between
/// "Present" events for the target process ID. This never reads Roblox's
/// memory and never injects anything into it - it only listens to trace
/// events the OS graphics stack already emits.
///
/// Real-time ETW tracing requires an elevated process token. When that is
/// not available (or the trace session otherwise fails to start), every
/// value is reported as unavailable with a human-readable reason instead of
/// a guessed number - see docs/ARCHITECTURE.md.
/// </summary>
public sealed class FpsMonitor : IFpsMonitor
{
    private const string DxgiProviderName = "Microsoft-Windows-DXGI";
    private static readonly TimeSpan SampleWindow = TimeSpan.FromSeconds(2);

    private readonly object _lock = new();
    private readonly Queue<DateTime> _presentTimestamps = new();

    private TraceEventSession? _session;
    private Task? _processingTask;
    private int _targetProcessId;
    private string? _unavailableReason;
    private volatile bool _isTracing;
    private bool _disposed;

    public void StartTracking(int processId)
    {
        lock (_lock)
        {
            StopTrackingInternal();

            _targetProcessId = processId;
            _presentTimestamps.Clear();
            _unavailableReason = null;

            if (TraceEventSession.IsElevated() != true)
            {
                _unavailableReason = "Frame-rate tracing requires running Roblox Performance Optimizer as Administrator";
                return;
            }

            try
            {
                var sessionName = "RobloxOptimizer-Fps-" + Guid.NewGuid().ToString("N")[..8];
                var session = new TraceEventSession(sessionName);
                session.EnableProvider(DxgiProviderName);
                session.Source.Dynamic.All += OnDynamicEvent;

                _session = session;
                _isTracing = true;

                // Capture "session" (the local) rather than the "_session" field: StopTrackingInternal
                // nulls the field from another thread, and this background task must keep working with
                // the exact session instance it started, not risk reading the field mid-teardown.
                _processingTask = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        // Blocks until the session is stopped/disposed.
                        session.Source.Process();
                    }
                    catch (Exception ex)
                    {
                        lock (_lock)
                        {
                            _unavailableReason = "Frame-rate trace session ended unexpectedly: " + ex.Message;
                            _isTracing = false;
                        }
                    }
                }, TaskCreationOptions.LongRunning);
            }
            catch (Exception ex)
            {
                _unavailableReason = "Could not start frame-rate tracing: " + ex.Message;
                _isTracing = false;
                try { _session?.Dispose(); } catch { /* best effort cleanup */ }
                _session = null;
            }
        }
    }

    private void OnDynamicEvent(TraceEvent data)
    {
        try
        {
            if (data.ProcessID != _targetProcessId)
            {
                return;
            }

            if (!string.Equals(data.ProviderName, DxgiProviderName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (data.OpcodeName is null || !data.OpcodeName.Contains("Present", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (_lock)
            {
                var timestamp = data.TimeStamp;
                _presentTimestamps.Enqueue(timestamp);

                var cutoff = timestamp - SampleWindow;
                while (_presentTimestamps.Count > 0 && _presentTimestamps.Peek() < cutoff)
                {
                    _presentTimestamps.Dequeue();
                }
            }
        }
        catch
        {
            // Never let a malformed/unexpected trace event take down the trace thread.
        }
    }

    public FpsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            if (!_isTracing)
            {
                return FpsSnapshot.Unavailable(_unavailableReason ?? "Frame-rate tracing is not active");
            }

            if (_presentTimestamps.Count < 3)
            {
                return FpsSnapshot.Unavailable("Waiting for frame data from Roblox");
            }

            var timestamps = _presentTimestamps.ToArray();
            var totalSeconds = (timestamps[^1] - timestamps[0]).TotalSeconds;

            if (totalSeconds <= 0)
            {
                return FpsSnapshot.Unavailable("Insufficient frame data yet");
            }

            var frameGapsSeconds = new List<double>(timestamps.Length - 1);
            for (var i = 1; i < timestamps.Length; i++)
            {
                var gap = (timestamps[i] - timestamps[i - 1]).TotalSeconds;
                if (gap > 0)
                {
                    frameGapsSeconds.Add(gap);
                }
            }

            if (frameGapsSeconds.Count == 0)
            {
                return FpsSnapshot.Unavailable("Insufficient frame data yet");
            }

            var averageFps = frameGapsSeconds.Count / totalSeconds;
            var instantaneousFps = 1.0 / frameGapsSeconds[^1];
            var minFps = 1.0 / frameGapsSeconds.Max();
            var maxFps = 1.0 / frameGapsSeconds.Min();

            return new FpsSnapshot
            {
                Current = Measurement<double>.Of(Math.Round(instantaneousFps, 1)),
                Average = Measurement<double>.Of(Math.Round(averageFps, 1)),
                Minimum = Measurement<double>.Of(Math.Round(minFps, 1)),
                Maximum = Measurement<double>.Of(Math.Round(maxFps, 1)),
                SampledAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public void StopTracking()
    {
        lock (_lock)
        {
            StopTrackingInternal();
        }
    }

    private void StopTrackingInternal()
    {
        _isTracing = false;

        if (_session is not null)
        {
            try { _session.Source.Dynamic.All -= OnDynamicEvent; } catch { /* best effort */ }
            try { _session.Stop(); } catch { /* best effort */ }
            try { _session.Dispose(); } catch { /* best effort */ }
            _session = null;
        }

        _processingTask = null;
        _presentTimestamps.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopTracking();
    }
}
