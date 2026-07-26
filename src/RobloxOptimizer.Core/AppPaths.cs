namespace RobloxOptimizer.Core;

/// <summary>Local-only storage locations. Nothing RPO writes ever leaves this folder or the device.</summary>
public static class AppPaths
{
    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxOptimizer");

    public static string BackupFilePath { get; } = Path.Combine(RootFolder, "backup.json");

    public static string AuditLogFilePath { get; } = Path.Combine(RootFolder, "audit.log");

    public static string SettingsFilePath { get; } = Path.Combine(RootFolder, "settings.json");

    public static void EnsureRootFolderExists() => Directory.CreateDirectory(RootFolder);
}
