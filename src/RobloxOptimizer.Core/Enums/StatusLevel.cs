namespace RobloxOptimizer.Core.Enums;

/// <summary>
/// Traffic-light health indicator shown next to every metric on the dashboard.
/// </summary>
public enum StatusLevel
{
    /// <summary>No measurement available yet, or the metric is not supported on this system.</summary>
    Unknown,

    /// <summary>Healthy - no action needed.</summary>
    Good,

    /// <summary>Borderline - worth keeping an eye on.</summary>
    Warning,

    /// <summary>Likely causing noticeable lag or instability.</summary>
    Critical
}
