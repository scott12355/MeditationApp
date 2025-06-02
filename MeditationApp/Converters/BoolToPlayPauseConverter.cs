using System.Globalization;

namespace MeditationApp.Converters;

public class BoolToPlayPauseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Handle different converter contexts based on parameter
        string? parameterString = parameter?.ToString();
        
        if (parameterString == "PlayPause")
        {
            // For play/pause state
            if (value is bool isPlaying)
                return isPlaying ? "⏸" : "▶";
            return "▶";
        }
        else
        {
            // Default: For download/play button based on IsDownloaded
            if (value is bool isDownloaded)
                return isDownloaded ? "▶" : "⬇";
            return "⬇";
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
