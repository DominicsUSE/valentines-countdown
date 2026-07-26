using System.Globalization;
using System.Windows.Data;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.App.Converters;

/// <summary>Formats a <c>Measurement&lt;double&gt;</c> for display, honoring "Unavailable" rather than showing a placeholder number. ConverterParameter is the unit suffix (e.g. " ms").</summary>
public sealed class MeasurementToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Measurement<double> measurement)
        {
            return "Unavailable";
        }

        if (!measurement.IsAvailable)
        {
            return "Unavailable";
        }

        var suffix = parameter as string ?? string.Empty;
        return measurement.Value!.Value.ToString("0.#", culture) + suffix;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
