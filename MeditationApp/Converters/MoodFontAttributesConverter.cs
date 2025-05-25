using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace MeditationApp.Converters
{
    public class MoodFontAttributesConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int selectedMood && parameter is string moodStr && int.TryParse(moodStr, out int mood))
            {
                return selectedMood == mood ? FontAttributes.Bold : FontAttributes.None;
            }
            return FontAttributes.None;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
