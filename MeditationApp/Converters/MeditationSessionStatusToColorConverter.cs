using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace MeditationApp.Converters
{
    public class MeditationSessionStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Replace with your actual status logic
            if (value == null)
                return Colors.Gray;

            string status = value.ToString();
            return status switch
            {
                "Completed" => Colors.Green,
                "Missed" => Colors.Red,
                "Pending" => Colors.Orange,
                _ => Colors.Gray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
