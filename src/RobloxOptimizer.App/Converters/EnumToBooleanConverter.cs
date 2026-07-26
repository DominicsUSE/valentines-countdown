using System.Globalization;
using System.Windows.Data;

namespace RobloxOptimizer.App.Converters;

/// <summary>
/// Binds a RadioButton's IsChecked to an enum-valued property. ConverterParameter is
/// the enum member's name as a literal string (e.g. ConverterParameter=Balanced) -
/// WPF does not support a live Binding as ConverterParameter, so this only works with
/// a static string parameter, which is exactly how it's used in the XAML views.
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string parameterString)
        {
            return false;
        }

        return string.Equals(value.ToString(), parameterString, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isChecked || !isChecked || parameter is not string parameterString)
        {
            return Binding.DoNothing;
        }

        return Enum.Parse(targetType, parameterString);
    }
}
