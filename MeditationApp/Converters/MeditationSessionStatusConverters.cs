using System.Globalization;
using MeditationApp.Models;

namespace MeditationApp.Converters;

public class StatusToDisplayStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MeditationSessionStatus status)
        {
            return status.ToDisplayString();
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MeditationSessionStatus status)
        {
            return status.ToStatusColor();
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToBackgroundColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MeditationSessionStatus status)
        {
            return status switch
            {
                MeditationSessionStatus.REQUESTED => Colors.Orange.WithAlpha(0.2f),
                MeditationSessionStatus.FAILED => Colors.Red.WithAlpha(0.2f),
                MeditationSessionStatus.COMPLETED => Colors.Green.WithAlpha(0.2f),
                _ => Colors.Gray.WithAlpha(0.2f)
            };
        }
        return Colors.Gray.WithAlpha(0.2f);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MeditationSessionStatus status)
        {
            return status switch
            {
                MeditationSessionStatus.REQUESTED => Color.FromArgb("#E65100"), // Dark orange
                MeditationSessionStatus.FAILED => Color.FromArgb("#C62828"),   // Dark red
                MeditationSessionStatus.COMPLETED => Color.FromArgb("#2E7D32"), // Dark green
                _ => Colors.Black
            };
        }
        return Colors.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MeditationSessionStatus status && parameter is string expectedStatus)
        {
            return status switch
            {
                MeditationSessionStatus.REQUESTED => expectedStatus == "REQUESTED",
                MeditationSessionStatus.FAILED => expectedStatus == "FAILED",
                MeditationSessionStatus.COMPLETED => expectedStatus == "COMPLETED",
                _ => false
            };
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
} 