using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;
using RobloxOptimizer.Core.Interfaces;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.App.Elevation;

/// <summary>
/// Implements the "briefly-elevated helper process" pattern described in
/// docs/ARCHITECTURE.md: relaunches this same executable with a single
/// narrow <c>--elevated-action=</c> argument and a UAC prompt (via
/// <c>runas</c>), waits for it to run exactly one whitelisted action and
/// exit, then reads its result. The main application window is never
/// elevated.
/// </summary>
public sealed class ElevationService : IElevationService
{
    public bool IsCurrentProcessElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public async Task<ElevatedActionResult> RunElevatedActionAsync(string actionKey, string? argument, CancellationToken cancellationToken = default)
    {
        if (IsCurrentProcessElevated)
        {
            // Unusual (the app doesn't normally run elevated) but handle it gracefully: just run the action in-process.
            return ElevatedActionRunner.Execute(actionKey, argument);
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return ElevatedActionResult.Fail("Could not determine the application's own executable path");
        }

        var argFilePath = Path.Combine(Path.GetTempPath(), $"rpo-elevated-arg-{Guid.NewGuid():N}.json");
        var resultFilePath = Path.Combine(Path.GetTempPath(), $"rpo-elevated-result-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(argFilePath, argument ?? string.Empty, cancellationToken).ConfigureAwait(false);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            startInfo.ArgumentList.Add("--elevated-action=" + actionKey);
            startInfo.ArgumentList.Add("--arg-file=" + argFilePath);
            startInfo.ArgumentList.Add("--result-file=" + resultFilePath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return ElevatedActionResult.Fail("Could not start the elevated helper process");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (!File.Exists(resultFilePath))
            {
                return ElevatedActionResult.Fail($"The elevated action did not report a result (exit code {process.ExitCode})");
            }

            var json = await File.ReadAllTextAsync(resultFilePath, cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<ElevatedActionResult>(json);
            return result ?? ElevatedActionResult.Fail("Could not parse the elevated action's result");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED - the user clicked "No" on the UAC prompt.
            return ElevatedActionResult.Fail("Administrator permission was not granted");
        }
        catch (Exception ex)
        {
            return ElevatedActionResult.Fail("Failed to run the elevated action: " + ex.Message);
        }
        finally
        {
            TryDelete(argFilePath);
            TryDelete(resultFilePath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Best effort - a leftover temp file is not worth surfacing to the user.
        }
    }
}
