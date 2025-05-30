using System.Globalization;

namespace MeditationApp.Converters
{
    public class MoodToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int moodValue)
            {
                return moodValue switch
                {
                    1 => "😞",
                    2 => "😕", 
                    3 => "😐",
                    4 => "🙂",
                    5 => "😃",
                    _ => "😐" // Default to neutral
                };
            }
            return "😐"; // Default fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string emoji)
            {
                return emoji switch
                {
                    "😞" => 1,
                    "😕" => 2,
                    "😐" => 3,
                    "🙂" => 4,
                    "😃" => 5,
                    _ => 3 // Default to neutral
                };
            }
            return 3; // Default fallback
        }
    }
}
