using System.Globalization;
using System.Windows.Data;

namespace Compass.App;

/// <summary>Maps a 0..1 fraction to a pixel width: fraction * trackWidth (passed as ConverterParameter).</summary>
public sealed class FractionToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double fraction = value is double d ? d : 0.0;
        double track =
            parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var t) ? t :
            parameter is double pd ? pd : 0.0;
        return Math.Clamp(fraction, 0, 1) * track;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
