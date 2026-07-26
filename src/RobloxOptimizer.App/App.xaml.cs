using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using RobloxOptimizer.App.Elevation;
using RobloxOptimizer.App.ViewModels;
using RobloxOptimizer.App.Views;
using RobloxOptimizer.Backup;
using RobloxOptimizer.Core;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Logging;
using RobloxOptimizer.Monitoring;
using RobloxOptimizer.Network;
using RobloxOptimizer.Optimization;
using RobloxOptimizer.Optimization.Actions;
using RobloxOptimizer.RobloxIntegration;

namespace RobloxOptimizer.App;

/// <summary>
/// Composition root. Also intercepts the narrow <c>--elevated-action=</c>
/// relaunch (see docs/ARCHITECTURE.md "Elevation model") before any window
/// is created, so the briefly-elevated helper process never shows UI.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (TryRunElevatedActionMode(e.Args, out var exitCode))
        {
            Shutdown(exitCode);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        base.OnStartup(e);

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private static bool TryRunElevatedActionMode(string[] args, out int exitCode)
    {
        string? actionKey = null;
        string? argFilePath = null;
        string? resultFilePath = null;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--elevated-action=", StringComparison.Ordinal))
            {
                actionKey = arg["--elevated-action=".Length..];
            }
            else if (arg.StartsWith("--arg-file=", StringComparison.Ordinal))
            {
                argFilePath = arg["--arg-file=".Length..];
            }
            else if (arg.StartsWith("--result-file=", StringComparison.Ordinal))
            {
                resultFilePath = arg["--result-file=".Length..];
            }
        }

        if (actionKey is null || resultFilePath is null)
        {
            exitCode = 0;
            return false;
        }

        string? argument = null;
        if (argFilePath is not null && File.Exists(argFilePath))
        {
            try
            {
                argument = File.ReadAllText(argFilePath);
            }
            catch (Exception)
            {
                // Best effort - ElevatedActionRunner will report a clear failure if the argument is missing/invalid.
            }
        }

        exitCode = ElevatedActionRunner.RunAndWriteResult(actionKey, argument, resultFilePath);
        return true;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // --- Monitoring ---
        services.AddSingleton<ISystemMonitor, SystemMonitor>();
        services.AddSingleton<ITemperatureMonitor, TemperatureMonitor>();
        services.AddSingleton<IFpsMonitor, FpsMonitor>();
        services.AddSingleton<IGpuInventory, GpuInventory>();

        // --- Network ---
        services.AddSingleton<INetworkDiagnostics, NetworkDiagnostics>();

        // --- Roblox integration ---
        services.AddSingleton<IRobloxDetector, RobloxDetector>();
        services.AddSingleton<IRobloxExecutableLocator, RobloxExecutableLocator>();

        // --- Backup / logging / elevation ---
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<RestorePointService>();

        // --- Optimization actions (the "Optimize for Roblox" bundle) ---
        services.AddSingleton<IOptimizationAction, PowerPlanAction>();
        services.AddSingleton<IOptimizationAction, OverlayToggleAction>();
        services.AddSingleton<IOptimizationAction, GpuPreferenceAction>();
        services.AddSingleton<IOptimizationAction, ProcessPriorityAction>();
        services.AddSingleton<OptimizationOrchestrator>();

        // --- Startup apps / background apps ---
        services.AddSingleton<StartupAppManager>();
        services.AddSingleton<BackgroundAppCloser>();

        // --- UI ---
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatal(e.Exception);
        MessageBox.Show(
            "Roblox Performance Optimizer hit an unexpected error and needs to close this action:\n\n" + e.Exception.Message,
            "Roblox Performance Optimizer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogFatal(ex);
        }
    }

    private static void LogFatal(Exception ex)
    {
        try
        {
            AppPaths.EnsureRootFolderExists();
            var line = $"{DateTimeOffset.UtcNow:O} FATAL: {ex}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(AppPaths.RootFolder, "fatal.log"), line);
        }
        catch (Exception)
        {
            // Nothing more we can do if even local logging fails.
        }
    }
}
