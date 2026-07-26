namespace RobloxOptimizer.Core.Models;

public enum StartupLocation
{
    CurrentUserRegistryRun,
    AllUsersRegistryRun,
    CurrentUserStartupFolder,
    AllUsersStartupFolder
}

/// <summary>
/// An application configured to launch at sign-in, discovered by
/// <c>StartupAppScanner</c>. The user chooses which of these to disable -
/// RPO never disables one automatically.
/// </summary>
public sealed class StartupAppInfo
{
    public required string Name { get; init; }
    public required string CommandLine { get; init; }
    public required StartupLocation Location { get; init; }
    public required bool IsCurrentlyEnabled { get; init; }

    /// <summary>True for <see cref="StartupLocation.AllUsersRegistryRun"/> and <see cref="StartupLocation.AllUsersStartupFolder"/>.</summary>
    public bool RequiresAdminToDisable => Location is StartupLocation.AllUsersRegistryRun or StartupLocation.AllUsersStartupFolder;
}
