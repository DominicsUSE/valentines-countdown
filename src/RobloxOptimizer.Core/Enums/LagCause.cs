namespace RobloxOptimizer.Core.Enums;

/// <summary>
/// The probable root cause of perceived lag, as inferred from measured metrics.
/// Multiple causes can be flagged simultaneously - lag is often multi-factor.
/// </summary>
[Flags]
public enum LagCause
{
    None = 0,

    /// <summary>Low or unstable frame rate - a rendering/CPU/GPU-side problem.</summary>
    LowFps = 1 << 0,

    /// <summary>High round-trip latency to the network - a connection/route problem.</summary>
    HighPing = 1 << 1,

    /// <summary>Dropped packets causing rubber-banding/teleporting.</summary>
    PacketLoss = 1 << 2,

    /// <summary>Ping is elevated but stable and consistent with physical distance to the server region.</summary>
    ServerDistance = 1 << 3,

    /// <summary>CPU or GPU is thermal throttling.</summary>
    Overheating = 1 << 4,

    /// <summary>Background applications are consuming CPU, GPU, RAM, or bandwidth.</summary>
    BackgroundApplications = 1 << 5,

    /// <summary>Wi-Fi signal is weak or unstable.</summary>
    WeakWifi = 1 << 6,

    /// <summary>Another device or process on the network is consuming most of the available bandwidth.</summary>
    BandwidthContention = 1 << 7
}
