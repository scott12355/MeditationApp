using System.Globalization;

namespace MeditationApp.Converters;

public class EqualityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Colors.Transparent;

        // Convert both values to strings for comparison
        string valueStr = value.ToString() ?? string.Empty;
        string parameterStr = parameter.ToString() ?? string.Empty;

        // Return a highlight color if they match, transparent if they don't
        return valueStr.Equals(parameterStr, StringComparison.OrdinalIgnoreCase) 
            ? Color.FromArgb("#E3F2FD") // Light blue background for selected
            : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
