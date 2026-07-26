namespace RobloxOptimizer.Core.Models;

/// <summary>
/// CPU/GPU temperature readings. Many OEM systems do not expose these to
/// software at all (BIOS/firmware locked); when that's the case the values are
/// reported as unavailable rather than guessed.
/// </summary>
public sealed class TemperatureSnapshot
{
    public required Measurement<double> CpuTemperatureCelsius { get; init; }
    public required Measurement<double> GpuTemperatureCelsius { get; init; }

    /// <summary>True if the CPU's reported clock speed is significantly below its rated maximum, a sign of thermal throttling.</summary>
    public required Measurement<bool> CpuThrottleDetected { get; init; }

    public required DateTimeOffset SampledAtUtc { get; init; }
}
