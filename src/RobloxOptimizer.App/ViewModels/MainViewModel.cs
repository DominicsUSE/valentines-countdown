using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxOptimizer.App.Helpers;
using RobloxOptimizer.Backup;
using RobloxOptimizer.Core;
using RobloxOptimizer.Core.Enums;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;
using RobloxOptimizer.Optimization;
using RobloxOptimizer.RobloxIntegration;

// These view-model properties are intentionally named after their model types (e.g. "RobloxStatus"
// property of type RobloxOptimizer.Core.Models.RobloxStatus). That shadows the bare type name for
// expression-context lookups within this class, so the two static factory members below are
// referenced through aliases instead of the bare type name.
using RobloxStatusModel = RobloxOptimizer.Core.Models.RobloxStatus;
using FpsSnapshotModel = RobloxOptimizer.Core.Models.FpsSnapshot;

namespace RobloxOptimizer.App.ViewModels;

/// <summary>
/// The application's single main view model. Owns all dashboard/network/
/// optimize/recommendations/logs state and the commands behind every button
/// described in the UI requirements (Analyze My PC, Apply Recommended
/// Fixes, Undo Changes, Optimize for Roblox).
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ISystemMonitor _systemMonitor;
    private readonly ITemperatureMonitor _temperatureMonitor;
    private readonly IFpsMonitor _fpsMonitor;
    private readonly INetworkDiagnostics _networkDiagnostics;
    private readonly IGpuInventory _gpuInventory;
    private readonly IRobloxDetector _robloxDetector;
    private readonly OptimizationOrchestrator _orchestrator;
    private readonly StartupAppManager _startupAppManager;
    private readonly BackgroundAppCloser _backgroundAppCloser;
    private readonly IAuditLogger _auditLogger;
    private readonly RestorePointService _restorePointService;

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _quickRefreshTimer;
    private DiagnosticsSnapshot? _lastFullSnapshot;
    private bool _disposed;

    public MainViewModel(
        ISystemMonitor systemMonitor,
        ITemperatureMonitor temperatureMonitor,
        IFpsMonitor fpsMonitor,
        INetworkDiagnostics networkDiagnostics,
        IGpuInventory gpuInventory,
        IRobloxDetector robloxDetector,
        OptimizationOrchestrator orchestrator,
        StartupAppManager startupAppManager,
        BackgroundAppCloser backgroundAppCloser,
        IAuditLogger auditLogger,
        RestorePointService restorePointService)
    {
        _systemMonitor = systemMonitor;
        _temperatureMonitor = temperatureMonitor;
        _fpsMonitor = fpsMonitor;
        _networkDiagnostics = networkDiagnostics;
        _gpuInventory = gpuInventory;
        _robloxDetector = robloxDetector;
        _orchestrator = orchestrator;
        _startupAppManager = startupAppManager;
        _backgroundAppCloser = backgroundAppCloser;
        _auditLogger = auditLogger;
        _restorePointService = restorePointService;

        RobloxStatus = _robloxDetector.GetCurrentStatus();

        _robloxDetector.RobloxLaunched += OnRobloxLaunched;
        _robloxDetector.RobloxExited += OnRobloxExited;
        _robloxDetector.StartMonitoring();

        _quickRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _quickRefreshTimer.Tick += async (_, _) => await QuickRefreshAsync().ConfigureAwait(true);
        _quickRefreshTimer.Start();

        _ = RefreshStartupAppsAsync();
        _ = RefreshAuditLogAsync();
    }

    // --- Bindable state ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuDisplay), nameof(RamDisplay), nameof(DiskDisplay), nameof(GpuDisplay),
        nameof(RamDetailDisplay), nameof(SystemStatus))]
    private SystemSnapshot? _systemSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuTempDisplay), nameof(GpuTempDisplay), nameof(ThrottleDisplay), nameof(TemperatureStatus))]
    private TemperatureSnapshot? _temperatureSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FpsCurrentDisplay), nameof(FpsAverageDisplay), nameof(FpsMinDisplay), nameof(FpsMaxDisplay), nameof(FpsStatus))]
    private FpsSnapshot? _fpsSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingCurrentDisplay), nameof(PingAverageDisplay), nameof(JitterDisplay), nameof(PacketLossDisplay),
        nameof(ConnectionTypeDisplay), nameof(WifiSignalDisplay), nameof(DownloadDisplay), nameof(UploadDisplay),
        nameof(NetworkStatus), nameof(WifiStatus), nameof(IsWifi))]
    private NetworkSnapshot? _networkSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RobloxStatusDisplay), nameof(IsRobloxRunning))]
    private RobloxStatus _robloxStatus = RobloxStatusModel.NotRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LagExplanation))]
    private DiagnosticsResult? _diagnosticsResult;

    [ObservableProperty]
    private bool _isAdvancedMode;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _isApplyingFixes;

    [ObservableProperty]
    private string _statusMessage = "Ready. Click \"Analyze My PC\" to check FPS, ping, and system health.";

    [ObservableProperty]
    private OptimizationProfile _selectedProfile = OptimizationProfile.Balanced;

    [ObservableProperty]
    private BenchmarkResult? _fpsBenchmark;

    [ObservableProperty]
    private BenchmarkResult? _pingBenchmark;

    [ObservableProperty]
    private bool _hasAppliedOptimizations;

    public ObservableCollection<RecommendationItemViewModel> Recommendations { get; } = new();
    public ObservableCollection<BackgroundAppItemViewModel> BackgroundApps { get; } = new();
    public ObservableCollection<StartupAppItemViewModel> StartupApps { get; } = new();
    public ObservableCollection<AuditLogEntry> AuditLogEntries { get; } = new();

    // --- Computed display strings (never fabricate a value - "Unavailable" when a measurement isn't available) ---

    public bool IsRobloxRunning => RobloxStatus.IsRunning;
    public string RobloxStatusDisplay => RobloxStatus.IsRunning
        ? $"Running ({RobloxStatus.ProcessName}, PID {RobloxStatus.ProcessId})"
        : "Not running";

    public string FpsCurrentDisplay => FpsSnapshot is null ? "Unavailable" : DisplayFormat.Format(FpsSnapshot.Current, " FPS");
    public string FpsAverageDisplay => FpsSnapshot is null ? "Unavailable" : DisplayFormat.Format(FpsSnapshot.Average, " FPS");
    public string FpsMinDisplay => FpsSnapshot is null ? "Unavailable" : DisplayFormat.Format(FpsSnapshot.Minimum, " FPS");
    public string FpsMaxDisplay => FpsSnapshot is null ? "Unavailable" : DisplayFormat.Format(FpsSnapshot.Maximum, " FPS");
    public StatusLevel FpsStatus => FpsSnapshot is null ? StatusLevel.Unknown : MetricStatusEvaluator.ForFps(FpsSnapshot.Average);

    public string PingCurrentDisplay => NetworkSnapshot is null ? "Unavailable" : DisplayFormat.Format(NetworkSnapshot.PingCurrentMs, " ms", "0");
    public string PingAverageDisplay => NetworkSnapshot is null ? "Unavailable" : DisplayFormat.Format(NetworkSnapshot.PingAverageMs, " ms", "0");
    public string JitterDisplay => NetworkSnapshot is null ? "Unavailable" : DisplayFormat.Format(NetworkSnapshot.JitterMs, " ms", "0");
    public string PacketLossDisplay => NetworkSnapshot is null ? "Unavailable" : DisplayFormat.Format(NetworkSnapshot.PacketLossPercent, "%", "0.#");
    public string DownloadDisplay => NetworkSnapshot is null ? "Unavailable" : DisplayFormat.Format(NetworkSnapshot.DownloadKilobitsPerSecond, " kbps", "0");
    public string UploadDisplay => NetworkSnapshot is null ? "Unavailable" : DisplayFormat.Format(NetworkSnapshot.UploadKilobitsPerSecond, " kbps", "0");
    public string ConnectionTypeDisplay => NetworkSnapshot?.ConnectionType.ToString() ?? "Unknown";
    public bool IsWifi => NetworkSnapshot?.ConnectionType == ConnectionType.WiFi;
    public string WifiSignalDisplay => NetworkSnapshot is null ? "Unavailable" : DisplayFormat.Format(NetworkSnapshot.WifiSignalPercent, "%");
    public StatusLevel NetworkStatus => NetworkSnapshot is null ? StatusLevel.Unknown : MetricStatusEvaluator.ForNetworkOverall(NetworkSnapshot);
    public StatusLevel WifiStatus => NetworkSnapshot is null ? StatusLevel.Unknown : MetricStatusEvaluator.ForWifiSignal(NetworkSnapshot.WifiSignalPercent);

    public string CpuDisplay => SystemSnapshot is null ? "Unavailable" : DisplayFormat.Format(SystemSnapshot.CpuUsagePercent, "%", "0");
    public string RamDisplay => SystemSnapshot is null ? "Unavailable" : DisplayFormat.Format(SystemSnapshot.RamUsagePercent, "%", "0");
    public string RamDetailDisplay => SystemSnapshot is null || !SystemSnapshot.RamUsedMegabytes.IsAvailable || !SystemSnapshot.RamTotalMegabytes.IsAvailable
        ? "Unavailable"
        : $"{SystemSnapshot.RamUsedMegabytes.Value / 1024.0:0.#} / {SystemSnapshot.RamTotalMegabytes.Value / 1024.0:0.#} GB";
    public string DiskDisplay => SystemSnapshot is null ? "Unavailable" : DisplayFormat.Format(SystemSnapshot.DiskUsagePercent, "%", "0");
    public string GpuDisplay => SystemSnapshot is null ? "Unavailable" : DisplayFormat.Format(SystemSnapshot.GpuUsagePercent, "%", "0");
    public StatusLevel SystemStatus => SystemSnapshot is null ? StatusLevel.Unknown : MetricStatusEvaluator.ForSystemOverall(SystemSnapshot);

    public string CpuTempDisplay => TemperatureSnapshot is null ? "Unavailable" : DisplayFormat.Format(TemperatureSnapshot.CpuTemperatureCelsius, " °C", "0");
    public string GpuTempDisplay => TemperatureSnapshot is null ? "Unavailable" : DisplayFormat.Format(TemperatureSnapshot.GpuTemperatureCelsius, " °C", "0");
    public string ThrottleDisplay => TemperatureSnapshot is null ? "Unavailable" : DisplayFormat.Format(TemperatureSnapshot.CpuThrottleDetected);
    public StatusLevel TemperatureStatus => TemperatureSnapshot is null ? StatusLevel.Unknown : MetricStatusEvaluator.ForTemperature(TemperatureSnapshot.CpuTemperatureCelsius);

    public string LagExplanation => DiagnosticsResult?.LagExplanation ?? "Click \"Analyze My PC\" to see whether any lag looks FPS-related or network-related.";

    // --- Commands ---

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (IsAnalyzing)
        {
            return;
        }

        IsAnalyzing = true;
        StatusMessage = "Analyzing your PC and connection - this takes a few seconds (network tests included)...";

        try
        {
            var snapshot = await RunFullDiagnosticsAsync().ConfigureAwait(true);
            _lastFullSnapshot = snapshot;

            var result = RecommendationEngine.Analyze(snapshot);
            DiagnosticsResult = result;

            Recommendations.Clear();
            foreach (var recommendation in result.Recommendations)
            {
                Recommendations.Add(new RecommendationItemViewModel(recommendation, ApplyRecommendationCoreAsync, UndoRecommendationCoreAsync));
            }

            StatusMessage = $"Analysis complete - {result.Recommendations.Count} recommendation(s) found.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Analysis failed: " + ex.Message;
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private async Task ApplyRecommendedFixesAsync()
    {
        if (IsApplyingFixes || Recommendations.Count == 0)
        {
            return;
        }

        IsApplyingFixes = true;
        StatusMessage = "Applying recommended fixes...";

        try
        {
            var beforeSnapshot = _lastFullSnapshot;

            foreach (var recommendation in Recommendations.Where(r => r.IsAutomatable && !r.IsApplied))
            {
                await recommendation.ApplyCommand.ExecuteAsync(null).ConfigureAwait(true);
            }

            HasAppliedOptimizations = true;

            // Re-measure so the before/after benchmark reflects real, freshly-measured numbers.
            var afterSnapshot = await RunFullDiagnosticsAsync().ConfigureAwait(true);
            _lastFullSnapshot = afterSnapshot;

            if (beforeSnapshot is not null)
            {
                FpsBenchmark = new BenchmarkResult
                {
                    MetricName = "Average FPS",
                    Unit = "FPS",
                    Before = beforeSnapshot.Fps.Average,
                    After = afterSnapshot.Fps.Average
                };

                PingBenchmark = new BenchmarkResult
                {
                    MetricName = "Average ping",
                    Unit = "ms",
                    Before = beforeSnapshot.Network.PingAverageMs,
                    After = afterSnapshot.Network.PingAverageMs
                };
            }

            StatusMessage = "Recommended fixes applied. See the before/after results below.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Applying fixes failed: " + ex.Message;
        }
        finally
        {
            IsApplyingFixes = false;
            await RefreshAuditLogAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task UndoChangesAsync()
    {
        StatusMessage = "Restoring your previous settings...";
        try
        {
            await _orchestrator.UndoAllAsync().ConfigureAwait(true);

            foreach (var recommendation in Recommendations)
            {
                if (recommendation.IsApplied)
                {
                    await recommendation.UndoCommand.ExecuteAsync(null).ConfigureAwait(true);
                }
            }

            HasAppliedOptimizations = false;
            FpsBenchmark = null;
            PingBenchmark = null;
            StatusMessage = "All RPO changes have been restored to their previous values.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Restore failed: " + ex.Message;
        }
        finally
        {
            await RefreshAuditLogAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OptimizeForRobloxAsync()
    {
        IsApplyingFixes = true;
        StatusMessage = $"Applying the '{SelectedProfile}' profile...";

        try
        {
            var profile = RobloxProfileCatalog.Get(SelectedProfile);
            await _orchestrator.ApplyManyAsync(profile.RecommendedOsActionIds).ConfigureAwait(true);
            HasAppliedOptimizations = true;
            StatusMessage = $"'{profile.Title}' profile applied. See the Roblox tab for in-game settings to match it.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Optimize failed: " + ex.Message;
        }
        finally
        {
            IsApplyingFixes = false;
            await RefreshAuditLogAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task RunNetworkTestAsync()
    {
        StatusMessage = "Running network diagnostics...";
        try
        {
            NetworkSnapshot = await _networkDiagnostics.RunDiagnosticsAsync(new NetworkDiagnosticsOptions()).ConfigureAwait(true);
            StatusMessage = "Network test complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Network test failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task FlushDnsAsync()
    {
        StatusMessage = "Flushing DNS cache (this rarely reduces in-game ping - it's a troubleshooting step only)...";
        var result = await _networkDiagnostics.FlushDnsCacheAsync().ConfigureAwait(true);
        StatusMessage = result.Message;
    }

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        StatusMessage = "Requesting administrator permission to create a System Restore point...";
        var result = await _restorePointService.TryCreateRestorePointAsync("Before Roblox Performance Optimizer changes").ConfigureAwait(true);
        StatusMessage = result.Message;
    }

    [RelayCommand]
    private async Task RefreshAuditLogAsync()
    {
        var entries = await _auditLogger.ReadRecentAsync(200).ConfigureAwait(true);
        AuditLogEntries.Clear();
        foreach (var entry in entries)
        {
            AuditLogEntries.Add(entry);
        }
    }

    [RelayCommand]
    private async Task RefreshStartupAppsAsync()
    {
        var apps = await Task.Run(StartupAppScanner.Scan).ConfigureAwait(true);
        StartupApps.Clear();
        foreach (var app in apps)
        {
            StartupApps.Add(new StartupAppItemViewModel(app, SetStartupAppEnabledAsync));
        }
    }

    [RelayCommand]
    private async Task RefreshBackgroundAppsAsync()
    {
        StatusMessage = "Scanning background applications (takes about a second)...";
        var apps = await BackgroundAppScanner.ScanAsync(TimeSpan.FromMilliseconds(600)).ConfigureAwait(true);
        BackgroundApps.Clear();
        foreach (var app in apps)
        {
            BackgroundApps.Add(new BackgroundAppItemViewModel(app, _backgroundAppCloser.CloseAsync));
        }

        StatusMessage = $"Found {apps.Count} background application(s) using notable CPU/RAM.";
    }

    private Task<ActionResult> SetStartupAppEnabledAsync(StartupAppInfo app, bool enable) =>
        _startupAppManager.SetEnabledAsync(app, enable);

    private Task<ActionResult> ApplyRecommendationCoreAsync(Recommendation recommendation) =>
        recommendation.ActionId is null
            ? Task.FromResult(ActionResult.Fail("This recommendation is guidance-only - there's nothing for RPO to apply automatically."))
            : _orchestrator.ApplyAsync(recommendation.ActionId);

    private Task<ActionResult> UndoRecommendationCoreAsync(Recommendation recommendation) =>
        recommendation.ActionId is null
            ? Task.FromResult(ActionResult.Fail("This recommendation is guidance-only - there's nothing to undo."))
            : _orchestrator.UndoAsync(recommendation.ActionId);

    private async Task<DiagnosticsSnapshot> RunFullDiagnosticsAsync()
    {
        var systemTask = _systemMonitor.GetSnapshotAsync();
        var temperatureTask = _temperatureMonitor.GetSnapshotAsync();
        var networkTask = _networkDiagnostics.RunDiagnosticsAsync(new NetworkDiagnosticsOptions());
        var gpuTask = _gpuInventory.GetDetectedGpuNamesAsync();
        var backgroundAppsTask = BackgroundAppScanner.ScanAsync(TimeSpan.FromMilliseconds(400));

        await Task.WhenAll(systemTask, temperatureTask, networkTask, gpuTask, backgroundAppsTask).ConfigureAwait(true);

        SystemSnapshot = systemTask.Result;
        TemperatureSnapshot = temperatureTask.Result;
        NetworkSnapshot = networkTask.Result;
        FpsSnapshot = RobloxStatus.IsRunning ? _fpsMonitor.GetSnapshot() : FpsSnapshotModel.Unavailable("Roblox is not currently running");

        BackgroundApps.Clear();
        foreach (var app in backgroundAppsTask.Result)
        {
            BackgroundApps.Add(new BackgroundAppItemViewModel(app, _backgroundAppCloser.CloseAsync));
        }

        return new DiagnosticsSnapshot
        {
            System = SystemSnapshot!,
            Temperature = TemperatureSnapshot!,
            Fps = FpsSnapshot!,
            Network = NetworkSnapshot!,
            Roblox = RobloxStatus,
            DetectedGpuCount = gpuTask.Result.Count,
            BackgroundApps = backgroundAppsTask.Result,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task QuickRefreshAsync()
    {
        try
        {
            SystemSnapshot = await _systemMonitor.GetSnapshotAsync().ConfigureAwait(true);
            RobloxStatus = _robloxDetector.GetCurrentStatus();

            if (RobloxStatus.IsRunning)
            {
                FpsSnapshot = _fpsMonitor.GetSnapshot();
            }
        }
        catch
        {
            // A quiet dashboard refresh must never surface an error dialog - the next tick will simply try again.
        }
    }

    private void OnRobloxLaunched(object? sender, RobloxStatus status)
    {
        // RobloxDetector raises this from its own background polling thread - WPF bindings
        // require property-changed notifications to be raised on the UI (dispatcher) thread.
        _dispatcher.BeginInvoke(() =>
        {
            RobloxStatus = status;
            if (status.ProcessId is int pid)
            {
                _fpsMonitor.StartTracking(pid);
            }
        });
    }

    private void OnRobloxExited(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        {
            RobloxStatus = RobloxStatusModel.NotRunning;
            _fpsMonitor.StopTracking();
            FpsSnapshot = null;
            HasAppliedOptimizations = false;

            // OptimizationOrchestrator auto-reverts the session-scoped actions the instant Roblox
            // exits (docs/ARCHITECTURE.md "Reversibility scope") - reflect that on any recommendation
            // cards still showing "Applied" so the UI never displays a stale, misleading state.
            foreach (var recommendation in Recommendations.Where(r => r.IsApplied))
            {
                recommendation.IsApplied = false;
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _quickRefreshTimer.Stop();
        _robloxDetector.RobloxLaunched -= OnRobloxLaunched;
        _robloxDetector.RobloxExited -= OnRobloxExited;
        _robloxDetector.StopMonitoring();
    }
}
