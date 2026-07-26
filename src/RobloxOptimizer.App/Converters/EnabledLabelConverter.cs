using System.Globalization;
using System.Windows.Data;

namespace RobloxOptimizer.App.Converters;

public sealed class EnabledLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? "Enabled" : "Disabled";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
