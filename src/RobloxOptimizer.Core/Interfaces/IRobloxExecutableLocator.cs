namespace RobloxOptimizer.Core.Interfaces;

/// <summary>
/// Finds the path to RobloxPlayerBeta.exe, either from the currently running
/// process or, if Roblox isn't running, by looking at the standard per-user
/// install location. Read-only filesystem/process lookup - never modifies
/// anything under the Roblox install folder.
/// </summary>
public interface IRobloxExecutableLocator
{
    string? FindExecutablePath();
}
