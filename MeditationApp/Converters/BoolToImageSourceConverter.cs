using System.Globalization;

namespace MeditationApp.Converters;

public class BoolToImageSourceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? parameterString = parameter?.ToString();
        
        if (parameterString == "PlayPause")
        {
            // For play/pause state - used with IsCurrentlyPlaying
            if (value is bool isPlaying)
                return isPlaying ? "pause.svg" : "play.svg";
            return "play.svg";
        }
        else
        {
            // Default: Always show play icon for single play button
            return "play.svg";
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
