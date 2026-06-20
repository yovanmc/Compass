using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Compass.App;

/// <summary>
/// Converts a local file path string to a BitmapImage for display in WPF Image controls.
/// Returns null (no image) when the path is null, empty, or the file doesn't exist.
/// </summary>
[ValueConversion(typeof(string), typeof(BitmapImage))]
public sealed class PathToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption   = BitmapCacheOption.OnLoad;
            // Optional ConverterParameter = target decode width in pixels. Decoding small
            // thumbnails (library covers) at display size instead of full resolution keeps
            // bulk loads fast and cuts memory. Omit the parameter for full-res (detail view).
            if (parameter is not null &&
                int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int decodeWidth) &&
                decodeWidth > 0)
            {
                image.DecodePixelWidth = decodeWidth;
            }
            image.UriSource     = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
