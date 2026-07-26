using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.App.Helpers;

/// <summary>Turns a <see cref="Measurement{T}"/> into UI text - "Unavailable" is shown verbatim rather than any placeholder number.</summary>
public static class DisplayFormat
{
    public static string Format(Measurement<double> measurement, string suffix, string numberFormat = "0.#")
    {
        return measurement.IsAvailable ? measurement.Value!.Value.ToString(numberFormat) + suffix : "Unavailable";
    }

    public static string Format(Measurement<int> measurement, string suffix)
    {
        return measurement.IsAvailable ? measurement.Value!.Value + suffix : "Unavailable";
    }

    public static string Format(Measurement<bool> measurement)
    {
        if (!measurement.IsAvailable)
        {
            return "Unavailable";
        }

        return measurement.Value!.Value ? "Yes" : "No";
    }
}
