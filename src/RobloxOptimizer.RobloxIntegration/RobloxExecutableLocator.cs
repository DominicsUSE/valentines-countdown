using RobloxOptimizer.Core.Interfaces;

namespace RobloxOptimizer.RobloxIntegration;

/// <summary>
/// Finds RobloxPlayerBeta.exe. Prefers the currently running process (most
/// accurate); if Roblox isn't running, falls back to scanning the standard
/// per-user install location and picking the most recently updated version
/// folder. Read-only - never writes under the Roblox install folder.
/// </summary>
public sealed class RobloxExecutableLocator : IRobloxExecutableLocator
{
    private readonly IRobloxDetector _detector;

    public RobloxExecutableLocator(IRobloxDetector detector)
    {
        _detector = detector;
    }

    public string? FindExecutablePath()
    {
        var status = _detector.GetCurrentStatus();
        if (status.IsRunning && !string.IsNullOrWhiteSpace(status.ExecutablePath) && File.Exists(status.ExecutablePath))
        {
            return status.ExecutablePath;
        }

        return FindNewestInstalledExecutable();
    }

    private static string? FindNewestInstalledExecutable()
    {
        try
        {
            var versionsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Roblox", "Versions");

            if (!Directory.Exists(versionsRoot))
            {
                return null;
            }

            return Directory.EnumerateDirectories(versionsRoot)
                .Select(dir => Path.Combine(dir, "RobloxPlayerBeta.exe"))
                .Where(File.Exists)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
