using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace MeditationApp.Converters
{
    public class ProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentCycle && parameter is int totalCycles && totalCycles > 0)
            {
                return (double)currentCycle / totalCycles;
            }
            
            // Try to get total from binding context if parameter not provided
            if (value is int current)
            {
                // Default to showing progress as a fraction of 10
                return (double)current / 10.0;
            }
            
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string parameterString = parameter?.ToString() ?? "Default|Alternative";
            string[] options = parameterString.Split('|');
            
            if (value == null)
            {
                return options.Length > 1 ? options[1] : options[0];
            }
            
            return options[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BreathingTimeSpanToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan.TotalHours >= 1)
                {
                    return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
                }
                else if (timeSpan.TotalMinutes >= 1)
                {
                    return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
                }
                else
                {
                    return $"{timeSpan.Seconds}s";
                }
            }
            return "0s";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PhaseToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.BreathingPhase phase)
            {
                return phase switch
                {
                    Models.BreathingPhase.Inhale => Colors.LightBlue,
                    Models.BreathingPhase.InhaleHold => Colors.Blue,
                    Models.BreathingPhase.Exhale => Colors.LightGreen,
                    Models.BreathingPhase.ExhaleHold => Colors.Green,
                    Models.BreathingPhase.Rest => Colors.LightYellow,
                    Models.BreathingPhase.Completed => Colors.Gold,
                    _ => Colors.LightGray
                };
            }
            return Colors.LightGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string colorString && !string.IsNullOrEmpty(colorString))
            {
                try
                {
                    return Color.FromArgb(colorString);
                }
                catch
                {
                    return Colors.Gray; // Fallback color
                }
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return color.ToArgbHex();
            }
            return "#808080";
        }
    }
}
