using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RobloxOptimizer.App.Converters;

/// <summary>True -&gt; Collapsed, False -&gt; Visible (the opposite of the built-in BooleanToVisibilityConverter).</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
