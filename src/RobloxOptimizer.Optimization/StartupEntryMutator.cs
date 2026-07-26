using Microsoft.Win32;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization;

/// <summary>
/// The actual enable/disable mechanics for one startup entry, shared by the
/// non-elevated path (current-user entries) and the briefly-elevated helper
/// process (all-users entries - see docs/ARCHITECTURE.md). Disabling never
/// deletes data: registry values are renamed with a recognizable prefix and
/// files get a ".disabled" suffix, so re-enabling always restores the exact
/// original entry.
/// </summary>
public static class StartupEntryMutator
{
    public static ActionResult Apply(StartupToggleRequest request)
    {
        try
        {
            return request.Location switch
            {
                StartupLocation.CurrentUserRegistryRun => ApplyRegistry(Registry.CurrentUser, request),
                StartupLocation.AllUsersRegistryRun => ApplyRegistry(Registry.LocalMachine, request),
                StartupLocation.CurrentUserStartupFolder => ApplyFile(request),
                StartupLocation.AllUsersStartupFolder => ApplyFile(request),
                _ => ActionResult.Fail("Unknown startup entry location")
            };
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Could not {(request.Enable ? "enable" : "disable")} '{request.Name}'", ex.Message);
        }
    }

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static ActionResult ApplyRegistry(RegistryKey hive, StartupToggleRequest request)
    {
        using var key = hive.CreateSubKey(RunKeyPath, writable: true);

        if (request.Enable)
        {
            var disabledName = StartupAppScanner.DisabledValuePrefix + request.Name;
            var data = key.GetValue(disabledName) as string ?? request.CommandLine;
            key.SetValue(request.Name, data, RegistryValueKind.String);
            key.DeleteValue(disabledName, throwOnMissingValue: false);
            return ActionResult.Ok($"'{request.Name}' re-enabled at startup");
        }
        else
        {
            var disabledName = StartupAppScanner.DisabledValuePrefix + request.Name;
            var data = key.GetValue(request.Name) as string ?? request.CommandLine;
            key.SetValue(disabledName, data, RegistryValueKind.String);
            key.DeleteValue(request.Name, throwOnMissingValue: false);
            return ActionResult.Ok($"'{request.Name}' disabled at startup");
        }
    }

    private static ActionResult ApplyFile(StartupToggleRequest request)
    {
        // For file-based entries, request.CommandLine carries the *current* full path on disk
        // (either the plain path or the ".disabled"-suffixed one) as scanned by StartupAppScanner.
        var currentPath = request.CommandLine;

        if (request.Enable)
        {
            if (!currentPath.EndsWith(StartupAppScanner.DisabledFileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return ActionResult.Ok($"'{request.Name}' is already enabled");
            }

            var restoredPath = currentPath[..^StartupAppScanner.DisabledFileSuffix.Length];
            File.Move(currentPath, restoredPath, overwrite: false);
            return ActionResult.Ok($"'{request.Name}' re-enabled at startup");
        }
        else
        {
            if (currentPath.EndsWith(StartupAppScanner.DisabledFileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return ActionResult.Ok($"'{request.Name}' is already disabled");
            }

            var disabledPath = currentPath + StartupAppScanner.DisabledFileSuffix;
            File.Move(currentPath, disabledPath, overwrite: false);
            return ActionResult.Ok($"'{request.Name}' disabled at startup");
        }
    }
}
