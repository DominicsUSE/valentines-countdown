using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RobloxOptimizer;

/// <summary>
/// The whole app in one window. Everything "Optimize for Roblox" changes is
/// stored under the current user's own account (HKCU registry + the active
/// power plan), so nothing here ever needs an administrator prompt, and
/// nothing here ever touches antivirus, the firewall, or Windows Update.
///
/// Reversibility is handled two ways: (1) clicking Undo, or closing the
/// window normally, reverts everything immediately, and (2) the previous
/// values are also written to a tiny backup file so that even if the app is
/// force-closed or crashes, the next time you open it you can still click
/// Undo to get back to exactly how things were.
/// </summary>
public partial class MainWindow : Window
{
    private const string RobloxProcessName = "RobloxPlayerBeta";

    // Windows' own built-in "High performance" power plan - the same GUID on every Windows 10/11 PC that has it.
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    // Xbox Game Bar / Game DVR - the same two HKCU values Settings > Gaming > Xbox Game Bar itself flips.
    private const string GameDvrPolicyKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR";
    private const string GameDvrPolicyValueName = "AppCaptureEnabled";
    private const string GameConfigStoreKeyPath = @"System\GameConfigStore";
    private const string GameConfigStoreValueName = "GameDVR_Enabled";

    // Per-app GPU preference - the same HKCU value Settings > System > Display > Graphics writes to.
    private const string GpuPreferenceKeyPath = @"SOFTWARE\Microsoft\DirectX\UserGpuPreferences";

    private static string BackupFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxOptimizer", "backup.txt");

    private readonly DispatcherTimer _refreshTimer;
    private readonly PerformanceCounter? _cpuCounter;
    private bool _isRefreshingStatus;

    private bool _isApplied;
    private bool _isBusy;
    private string? _previousPowerSchemeGuid;
    private bool _hadAppCaptureValue;
    private int? _previousAppCaptureEnabled;
    private bool _hadGameDvrValue;
    private int? _previousGameDvrEnabled;

    // Live path for the dashboard's "Roblox: running" detection - refreshed every 2 seconds regardless of Optimize/Undo.
    private string? _robloxExePath;

    // Separately, the exact path GPU-preference/priority were actually changed for, captured at Apply time.
    // Undo must act on THIS, not on _robloxExePath - otherwise, launching Roblox after Optimize (skipped GPU/priority
    // because Roblox wasn't open yet) would make Undo think it needs to revert a change that was never made.
    private string? _appliedRobloxExePath;
    private bool _hadGpuPreferenceValue;
    private string? _previousGpuPreferenceValue;

    public MainWindow()
    {
        InitializeComponent();

        _cpuCounter = TryCreateCpuCounter();
        _cpuCounter?.NextValue(); // Prime it - the first reading right after creation is always meaningless.

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _refreshTimer.Start();
        _ = RefreshStatusAsync();

        Closing += MainWindow_Closing;

        TryRestorePreviousSessionState();
        PopulateOutputDevices();
    }

    // ----- Live dashboard -----

    private async Task RefreshStatusAsync()
    {
        if (_isRefreshingStatus)
        {
            return;
        }

        _isRefreshingStatus = true;
        try
        {
            RefreshRobloxStatus();
            RefreshCpu();
            RefreshRam();
            await RefreshPingAsync().ConfigureAwait(true);
        }
        finally
        {
            _isRefreshingStatus = false;
        }
    }

    private void RefreshRobloxStatus()
    {
        try
        {
            var processes = Process.GetProcessesByName(RobloxProcessName);
            try
            {
                var isRunning = processes.Length > 0;
                if (isRunning)
                {
                    try { _robloxExePath = processes[0].MainModule?.FileName; }
                    catch (Exception) { /* Can be denied across user sessions - keep the last known path. */ }
                }

                RobloxStatusText.Text = isRunning ? "Roblox: running" : "Roblox: not running";
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception)
        {
            RobloxStatusText.Text = "Roblox: unknown";
        }
    }

    private void RefreshCpu()
    {
        try
        {
            CpuText.Text = _cpuCounter is not null
                ? $"CPU: {Math.Clamp(_cpuCounter.NextValue(), 0, 100):0}%"
                : "CPU: unavailable";
        }
        catch (Exception)
        {
            CpuText.Text = "CPU: unavailable";
        }
    }

    private void RefreshRam()
    {
        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            RamText.Text = GlobalMemoryStatusEx(ref status)
                ? $"RAM: {status.dwMemoryLoad}% in use"
                : "RAM: unavailable";
        }
        catch (Exception)
        {
            RamText.Text = "RAM: unavailable";
        }
    }

    private const int PingSamplesPerRefresh = 3;
    private const int PingTimeoutMs = 800;

    // A few well-known, low-latency public resolvers. Different ISPs peer with each of these at
    // different quality, so instead of hardcoding one, the app periodically checks which one is
    // currently fastest from this PC's network and reads from that one - a real, honest way to
    // get the lowest available reading, not a faked number (no software can lower your actual
    // latency to Roblox's own game servers).
    private static readonly string[] PingCandidateHosts = { "1.1.1.1", "8.8.8.8", "9.9.9.9" };
    private static readonly TimeSpan PingHostReselectInterval = TimeSpan.FromMinutes(2);
    private string _pingHost = PingCandidateHosts[0];
    private DateTime _pingHostChosenAtUtc = DateTime.MinValue;

    private async Task PickFastestPingHostAsync()
    {
        if (DateTime.UtcNow - _pingHostChosenAtUtc < PingHostReselectInterval)
        {
            return;
        }

        try
        {
            using var ping = new Ping();
            string? bestHost = null;
            var bestRoundTrip = long.MaxValue;

            foreach (var host in PingCandidateHosts)
            {
                try
                {
                    var reply = await ping.SendPingAsync(host, PingTimeoutMs).ConfigureAwait(true);
                    if (reply.Status == IPStatus.Success && reply.RoundtripTime < bestRoundTrip)
                    {
                        bestRoundTrip = reply.RoundtripTime;
                        bestHost = host;
                    }
                }
                catch (Exception)
                {
                    // Try the next candidate - one unreachable resolver shouldn't block the others.
                }
            }

            if (bestHost is not null)
            {
                _pingHost = bestHost;
            }
        }
        catch (Exception)
        {
            // Keep using whichever host was already selected.
        }
        finally
        {
            _pingHostChosenAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Sends a few quick pings each refresh instead of just one, so the dashboard can show
    /// jitter (ping variability) and packet loss too - not just a single current reading.
    /// This is still general Internet route quality to a public server, not the exact Roblox
    /// game-server ping (there's no way to discover that).
    /// </summary>
    private async Task RefreshPingAsync()
    {
        await PickFastestPingHostAsync().ConfigureAwait(true);

        var roundTripTimes = new List<long>();

        try
        {
            using var ping = new Ping();

            for (var i = 0; i < PingSamplesPerRefresh; i++)
            {
                try
                {
                    var reply = await ping.SendPingAsync(_pingHost, PingTimeoutMs).ConfigureAwait(true);
                    if (reply.Status == IPStatus.Success)
                    {
                        roundTripTimes.Add(reply.RoundtripTime);
                    }
                }
                catch (Exception)
                {
                    // Counts as a lost sample below - a single failed ping shouldn't abort the whole read.
                }
            }
        }
        catch (Exception)
        {
            PingText.Text = "Ping: unavailable";
            return;
        }

        if (roundTripTimes.Count == 0)
        {
            PingText.Text = "Ping: unavailable";
            return;
        }

        var current = roundTripTimes[^1];
        var lossPercent = (PingSamplesPerRefresh - roundTripTimes.Count) * 100 / PingSamplesPerRefresh;

        if (roundTripTimes.Count < 2)
        {
            PingText.Text = $"Ping: {current} ms";
            return;
        }

        long jitterTotal = 0;
        for (var i = 1; i < roundTripTimes.Count; i++)
        {
            jitterTotal += Math.Abs(roundTripTimes[i] - roundTripTimes[i - 1]);
        }

        var jitter = jitterTotal / (roundTripTimes.Count - 1);

        PingText.Text = lossPercent > 0
            ? $"Ping: {current} ms (jitter {jitter} ms, {lossPercent}% loss)"
            : $"Ping: {current} ms (jitter {jitter} ms)";
    }

    private static PerformanceCounter? TryCreateCpuCounter()
    {
        try
        {
            return PerformanceCounterCategory.Exists("Processor")
                ? new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ----- Optimize / Undo -----

    private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplied || _isBusy)
        {
            return;
        }

        _isBusy = true;
        OptimizeButton.IsEnabled = false;
        Log("Applying optimizations...");

        await Task.Run(() =>
        {
            ApplyPowerPlan();
            ApplyGameBarOverlayOff();
            ApplyRobloxGpuAndPriority();
            SaveBackupFile();
        }).ConfigureAwait(true);

        _isApplied = true;
        _isBusy = false;
        UndoButton.IsEnabled = true;
        Log("Done. Click \"Undo Changes\" any time to put everything back.");
    }

    private async void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isApplied || _isBusy)
        {
            return;
        }

        _isBusy = true;
        UndoButton.IsEnabled = false;
        Log("Undoing changes...");

        await Task.Run(UndoAll).ConfigureAwait(true);

        _isApplied = false;
        _isBusy = false;
        OptimizeButton.IsEnabled = true;
        Log("Everything has been restored.");
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        StopVoiceActivity();

        // If an Optimize/Undo is already running in the background, don't run a second, concurrent
        // Undo on top of it - the in-flight one will finish (or, worst case, the backup file written
        // by Optimize lets the user click Undo again next time the app is opened).
        if (!_isApplied || _isBusy)
        {
            return;
        }

        try
        {
            UndoAll();
        }
        catch (Exception)
        {
            // Best effort on the way out - the backup file (if writing it succeeded earlier)
            // still lets the user restore manually next time they open the app.
        }
    }

    private void ApplyPowerPlan()
    {
        try
        {
            var currentSchemeOutput = RunPowercfgForOutput("/getactivescheme");
            _previousPowerSchemeGuid = currentSchemeOutput is not null ? ExtractGuid(currentSchemeOutput) : null;

            var switched = RunPowercfg($"/setactive {HighPerformanceGuid}");
            Log(switched
                ? "Switched to the High performance power plan."
                : "Couldn't switch power plan - this PC may not have a 'High performance' plan available.");
        }
        catch (Exception ex)
        {
            Log("Power plan change failed: " + ex.Message);
        }
    }

    private void ApplyGameBarOverlayOff()
    {
        try
        {
            _previousAppCaptureEnabled = ReadDword(GameDvrPolicyKeyPath, GameDvrPolicyValueName, out _hadAppCaptureValue);
            WriteDword(GameDvrPolicyKeyPath, GameDvrPolicyValueName, 0);

            _previousGameDvrEnabled = ReadDword(GameConfigStoreKeyPath, GameConfigStoreValueName, out _hadGameDvrValue);
            WriteDword(GameConfigStoreKeyPath, GameConfigStoreValueName, 0);

            Log("Turned off the Xbox Game Bar overlay.");
        }
        catch (Exception ex)
        {
            Log("Couldn't change the Game Bar overlay setting: " + ex.Message);
        }
    }

    private void ApplyRobloxGpuAndPriority()
    {
        if (string.IsNullOrEmpty(_robloxExePath))
        {
            Log("Tip: launch Roblox, then click Optimize again to also set its GPU preference and priority.");
            return;
        }

        _appliedRobloxExePath = _robloxExePath;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(GpuPreferenceKeyPath, writable: true);
            _previousGpuPreferenceValue = key.GetValue(_appliedRobloxExePath) as string;
            _hadGpuPreferenceValue = _previousGpuPreferenceValue is not null;
            key.SetValue(_appliedRobloxExePath, "GpuPreference=2;", RegistryValueKind.String);
            Log("Set Roblox to use your dedicated graphics card.");
        }
        catch (Exception ex)
        {
            Log("Couldn't set the GPU preference: " + ex.Message);
        }

        try
        {
            // "High" is the strongest priority bump Task Manager itself offers next to Realtime -
            // Realtime is intentionally never used here since it can starve the mouse/keyboard/rest
            // of Windows and make the whole PC feel frozen.
            var raised = SetRobloxPriority(ProcessPriorityClass.High);
            if (raised)
            {
                Log("Raised Roblox's process priority (High).");
            }
        }
        catch (Exception ex)
        {
            Log("Couldn't change Roblox's process priority: " + ex.Message);
        }
    }

    private void UndoAll()
    {
        try
        {
            if (!string.IsNullOrEmpty(_previousPowerSchemeGuid) && RunPowercfg($"/setactive {_previousPowerSchemeGuid}"))
            {
                Log("Restored your previous power plan.");
            }
        }
        catch (Exception ex)
        {
            Log("Couldn't restore the power plan: " + ex.Message);
        }

        try
        {
            RestoreDwordOrRemove(GameDvrPolicyKeyPath, GameDvrPolicyValueName, _hadAppCaptureValue, _previousAppCaptureEnabled);
            RestoreDwordOrRemove(GameConfigStoreKeyPath, GameConfigStoreValueName, _hadGameDvrValue, _previousGameDvrEnabled);
            Log("Restored the Game Bar overlay setting.");
        }
        catch (Exception ex)
        {
            Log("Couldn't restore the Game Bar overlay setting: " + ex.Message);
        }

        if (!string.IsNullOrEmpty(_appliedRobloxExePath))
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(GpuPreferenceKeyPath, writable: true);
                if (_hadGpuPreferenceValue && _previousGpuPreferenceValue is not null)
                {
                    key.SetValue(_appliedRobloxExePath, _previousGpuPreferenceValue, RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(_appliedRobloxExePath, throwOnMissingValue: false);
                }

                Log("Restored the GPU preference.");
            }
            catch (Exception ex)
            {
                Log("Couldn't restore the GPU preference: " + ex.Message);
            }

            try
            {
                SetRobloxPriority(ProcessPriorityClass.Normal);
            }
            catch (Exception)
            {
                // Roblox may have already closed - priority resets by itself when a process exits anyway.
            }
        }

        _previousPowerSchemeGuid = null;
        _hadAppCaptureValue = false;
        _previousAppCaptureEnabled = null;
        _hadGameDvrValue = false;
        _previousGameDvrEnabled = null;
        _appliedRobloxExePath = null;
        _hadGpuPreferenceValue = false;
        _previousGpuPreferenceValue = null;

        DeleteBackupFile();
    }

    private static bool SetRobloxPriority(ProcessPriorityClass priority)
    {
        var processes = Process.GetProcessesByName(RobloxProcessName);
        try
        {
            foreach (var process in processes)
            {
                process.PriorityClass = priority;
            }

            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    // ----- Crash-resilient backup: a tiny local text file, not a whole database -----

    private void SaveBackupFile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BackupFilePath)!);

            var lines = new[]
            {
                $"PowerScheme={_previousPowerSchemeGuid}",
                $"AppCaptureEnabled={(_hadAppCaptureValue ? _previousAppCaptureEnabled : null)}",
                $"GameDvrEnabled={(_hadGameDvrValue ? _previousGameDvrEnabled : null)}",
                $"AppliedRobloxExePath={_appliedRobloxExePath}",
                $"GpuPreference={(_hadGpuPreferenceValue ? _previousGpuPreferenceValue : null)}"
            };

            File.WriteAllLines(BackupFilePath, lines);
        }
        catch (Exception)
        {
            // Best effort - Undo still works for the rest of this session even if we can't persist to disk.
        }
    }

    private static void DeleteBackupFile()
    {
        try
        {
            if (File.Exists(BackupFilePath))
            {
                File.Delete(BackupFilePath);
            }
        }
        catch (Exception)
        {
            // Best effort.
        }
    }

    private void TryRestorePreviousSessionState()
    {
        try
        {
            if (!File.Exists(BackupFilePath))
            {
                return;
            }

            var values = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(BackupFilePath))
            {
                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                values[line[..separatorIndex]] = line[(separatorIndex + 1)..];
            }

            _previousPowerSchemeGuid = NullIfEmpty(values.GetValueOrDefault("PowerScheme"));

            _hadAppCaptureValue = int.TryParse(values.GetValueOrDefault("AppCaptureEnabled"), out var appCapture);
            _previousAppCaptureEnabled = _hadAppCaptureValue ? appCapture : null;

            _hadGameDvrValue = int.TryParse(values.GetValueOrDefault("GameDvrEnabled"), out var gameDvr);
            _previousGameDvrEnabled = _hadGameDvrValue ? gameDvr : null;

            _appliedRobloxExePath = NullIfEmpty(values.GetValueOrDefault("AppliedRobloxExePath"));

            _previousGpuPreferenceValue = NullIfEmpty(values.GetValueOrDefault("GpuPreference"));
            _hadGpuPreferenceValue = _previousGpuPreferenceValue is not null;

            _isApplied = true;
            OptimizeButton.IsEnabled = false;
            UndoButton.IsEnabled = true;
            Log("Found optimizations left applied from an earlier session. Click \"Undo Changes\" to restore your original settings.");
        }
        catch (Exception ex)
        {
            Log("Couldn't read the previous session's backup: " + ex.Message);
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // ----- Small helpers -----

    private void Log(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Log(message));
            return;
        }

        LogText.Text += Environment.NewLine + message;
        LogScrollViewer.ScrollToBottom();
    }

    private static int? ReadDword(string keyPath, string valueName, out bool hadValue)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        var raw = key?.GetValue(valueName);
        hadValue = raw is int;
        return raw is int intValue ? intValue : null;
    }

    private static void WriteDword(string keyPath, string valueName, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }

    private static void RestoreDwordOrRemove(string keyPath, string valueName, bool hadValue, int? previousValue)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        if (hadValue && previousValue is int value)
        {
            key.SetValue(valueName, value, RegistryValueKind.DWord);
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static bool RunPowercfg(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? RunPowercfgForOutput(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ExtractGuid(string powercfgOutput)
    {
        var match = Regex.Match(powercfgOutput, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return match.Success ? match.Value : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
