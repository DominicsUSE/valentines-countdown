namespace RobloxOptimizer.Optimization;

/// <summary>
/// Process names RPO will never offer to close, pause, or terminate, no
/// matter what the user selects in the UI. This is a hard backstop enforced
/// in code, independent of what the UI shows - see docs/SAFETY_MODEL.md.
/// </summary>
public static class ProtectedProcesses
{
    private static readonly HashSet<string> ExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core OS / kernel / shell.
        "System", "Registry", "Idle", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "svchost", "dwm", "explorer", "fontdrvhost",
        "sihost", "ctfmon", "taskhostw", "RuntimeBroker", "conhost", "spoolsv",
        "LogonUI", "WUDFHost",

        // Windows Update / servicing / security.
        "MsMpEng", "NisSrv", "SecurityHealthService", "SecurityHealthSystray",
        "MpDefenderCoreService", "wuauclt", "UsoClient", "TiWorker",
        "TrustedInstaller", "WaaSMedicAgent", "SgrmBroker",

        // RPO itself.
        "RobloxOptimizer"
    };

    private static readonly string[] VendorKeywords =
    {
        "defender", "avast", "avg", "norton", "mcafee", "kaspersky",
        "bitdefender", "eset", "sophos", "malwarebytes", "webroot", "avira",
        "trendmicro", "totalav", "windowsdefender", "firewall"
    };

    public static bool IsProtected(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return true;
        }

        if (ExactNames.Contains(processName))
        {
            return true;
        }

        return VendorKeywords.Any(keyword => processName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
