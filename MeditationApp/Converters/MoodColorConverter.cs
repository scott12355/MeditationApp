using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace MeditationApp.Converters
{
    public class MoodColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int selectedMood && parameter is string moodStr && int.TryParse(moodStr, out int mood))
            {
                return selectedMood == mood ? Colors.Orange : Colors.Gray;
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
