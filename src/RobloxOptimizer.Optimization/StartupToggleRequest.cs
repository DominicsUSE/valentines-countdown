using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization;

/// <summary>Serialized as the argument to the <c>ToggleStartupEntry</c> elevated action.</summary>
public sealed class StartupToggleRequest
{
    public required StartupLocation Location { get; init; }
    public required string Name { get; init; }
    public required string CommandLine { get; init; }
    public required bool Enable { get; init; }
}
