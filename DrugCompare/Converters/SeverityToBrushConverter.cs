using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DrugCompare.Converters;

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var severity = value?.ToString();

        return severity switch
        {
            "Contraindicated" => new SolidColorBrush(Color.FromRgb(127, 29, 29)),
            "Major" => new SolidColorBrush(Color.FromRgb(185, 28, 28)),
            "Moderate" => new SolidColorBrush(Color.FromRgb(194, 120, 3)),
            "Minor" => new SolidColorBrush(Color.FromRgb(161, 98, 7)),
            "Unknown" => new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}