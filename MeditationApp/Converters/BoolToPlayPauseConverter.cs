using System.Globalization;

namespace MeditationApp.Converters;

public class BoolToPlayPauseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying)
            return isPlaying ? "⏸" : "▶";
        return "▶";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
