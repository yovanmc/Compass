using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Compass.App;

/// <summary>
/// Converts a value to Visibility: null → Collapsed, non-null → Visible.
/// Pass parameter "invert" to flip (null → Visible, non-null → Collapsed).
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        bool invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        bool visible = invert ? isNull : !isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
