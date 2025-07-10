using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace MeditationApp.Converters
{
    public class BreathingCircleScaleClampConverter : IValueConverter
    {
        // The maximum allowed scale so that the 220px circle never exceeds the 280px outer ring
        private const double MaxScale = 280.0 / 220.0; // ~1.2727
        private const double MinScale = 0.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double scale)
            {
                if (scale < MinScale) return MinScale;
                if (scale > MaxScale) return MaxScale;
                return scale;
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

