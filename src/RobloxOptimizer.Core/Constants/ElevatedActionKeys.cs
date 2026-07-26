namespace RobloxOptimizer.Core.Constants;

/// <summary>
/// The complete whitelist of actions the briefly-elevated helper process is
/// allowed to perform. See docs/ARCHITECTURE.md "Elevation model" - the
/// elevated instance dispatches on one of these keys and does nothing else.
/// </summary>
public static class ElevatedActionKeys
{
    public const string CreateRestorePoint = "CreateRestorePoint";
    public const string ToggleStartupEntry = "ToggleStartupEntry";
}
