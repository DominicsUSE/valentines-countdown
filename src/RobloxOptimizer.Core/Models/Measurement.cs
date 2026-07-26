namespace RobloxOptimizer.Core.Models;

/// <summary>
/// Wraps a single measured value together with an explicit availability flag.
/// RPO never fabricates a number: if <see cref="IsAvailable"/> is false, the UI
/// must show "Unavailable" and <see cref="UnavailableReason"/> instead of a value.
/// </summary>
public readonly struct Measurement<T>
{
    public bool IsAvailable { get; }
    public T? Value { get; }
    public string? UnavailableReason { get; }

    private Measurement(bool isAvailable, T? value, string? unavailableReason)
    {
        IsAvailable = isAvailable;
        Value = value;
        UnavailableReason = unavailableReason;
    }

    public static Measurement<T> Of(T value) => new(true, value, null);

    public static Measurement<T> Unavailable(string reason) => new(false, default, reason);

    public override string ToString() => IsAvailable ? Value?.ToString() ?? string.Empty : $"Unavailable ({UnavailableReason})";
}
