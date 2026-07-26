namespace RobloxOptimizer.Core.Models;

/// <summary>
/// A running, user-visible or resource-heavy process the user can optionally
/// close from the Optimize tab. RPO never selects or closes these on its own.
/// </summary>
public sealed class BackgroundAppInfo
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string? WindowTitle { get; init; }
    public required Measurement<double> CpuPercent { get; init; }
    public required Measurement<double> MemoryMegabytes { get; init; }

    /// <summary>
    /// True when the process is on RPO's protected list (antivirus, Windows
    /// security services, updaters, essential system processes) and must not
    /// be offered as closable, even if the user selects it elsewhere.
    /// </summary>
    public required bool IsProtected { get; init; }
}
