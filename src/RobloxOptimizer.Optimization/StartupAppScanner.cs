using Microsoft.Win32;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization;

/// <summary>
/// Read-only enumeration of applications configured to launch at sign-in.
/// The user chooses which of these to disable from the Optimize tab - RPO
/// never disables one on its own.
/// </summary>
public static class StartupAppScanner
{
    internal const string DisabledValuePrefix = "RPODisabled_";
    internal const string DisabledFileSuffix = ".disabled";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static IReadOnlyList<StartupAppInfo> Scan()
    {
        var results = new List<StartupAppInfo>();

        results.AddRange(ScanRegistryHive(Registry.CurrentUser, StartupLocation.CurrentUserRegistryRun));
        results.AddRange(ScanRegistryHive(Registry.LocalMachine, StartupLocation.AllUsersRegistryRun));
        results.AddRange(ScanStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupLocation.CurrentUserStartupFolder));
        results.AddRange(ScanStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupLocation.AllUsersStartupFolder));

        return results;
    }

    private static IEnumerable<StartupAppInfo> ScanRegistryHive(RegistryKey hive, StartupLocation location)
    {
        using var key = hive.OpenSubKey(RunKeyPath, writable: false);
        if (key is null)
        {
            yield break;
        }

        foreach (var valueName in key.GetValueNames())
        {
            if (key.GetValue(valueName) is not string commandLine)
            {
                continue;
            }

            var isDisabledByUs = valueName.StartsWith(DisabledValuePrefix, StringComparison.Ordinal);
            var displayName = isDisabledByUs ? valueName[DisabledValuePrefix.Length..] : valueName;

            yield return new StartupAppInfo
            {
                Name = displayName,
                CommandLine = commandLine,
                Location = location,
                IsCurrentlyEnabled = !isDisabledByUs
            };
        }
    }

    private static IEnumerable<StartupAppInfo> ScanStartupFolder(string folderPath, StartupLocation location)
    {
        if (!Directory.Exists(folderPath))
        {
            yield break;
        }

        foreach (var filePath in Directory.EnumerateFiles(folderPath))
        {
            var fileName = Path.GetFileName(filePath);
            var isDisabledByUs = fileName.EndsWith(DisabledFileSuffix, StringComparison.OrdinalIgnoreCase);
            var displayName = isDisabledByUs
                ? Path.GetFileNameWithoutExtension(fileName[..^DisabledFileSuffix.Length])
                : Path.GetFileNameWithoutExtension(fileName);

            yield return new StartupAppInfo
            {
                Name = displayName,
                CommandLine = filePath,
                Location = location,
                IsCurrentlyEnabled = !isDisabledByUs
            };
        }
    }
}
