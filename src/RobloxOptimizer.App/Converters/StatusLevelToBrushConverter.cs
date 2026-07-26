using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using RobloxOptimizer.Core.Enums;

namespace RobloxOptimizer.App.Converters;

/// <summary>Maps a <see cref="StatusLevel"/> to the dashboard's Green/Yellow/Red/Gray indicator color.</summary>
public sealed class StatusLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Good = new(Color.FromRgb(0x3D, 0xBE, 0x6C));
    private static readonly SolidColorBrush Warning = new(Color.FromRgb(0xE6, 0xB9, 0x3D));
    private static readonly SolidColorBrush Critical = new(Color.FromRgb(0xE5, 0x53, 0x4B));
    private static readonly SolidColorBrush Unknown = new(Color.FromRgb(0x6B, 0x72, 0x80));

    static StatusLevelToBrushConverter()
    {
        Good.Freeze();
        Warning.Freeze();
        Critical.Freeze();
        Unknown.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            StatusLevel.Good => Good,
            StatusLevel.Warning => Warning,
            StatusLevel.Critical => Critical,
            _ => Unknown
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
