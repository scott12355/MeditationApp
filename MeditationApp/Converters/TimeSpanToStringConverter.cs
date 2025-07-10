using System.Globalization;

namespace MeditationApp.Converters
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan.TotalDays >= 1)
                {
                    return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
                }
                else if (timeSpan.TotalHours >= 1)
                {
                    return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
                }
                else
                {
                    return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
                }
            }
            return "0m";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
