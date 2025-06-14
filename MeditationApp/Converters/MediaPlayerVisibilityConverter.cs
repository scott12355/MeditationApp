using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using MeditationApp.Models;

namespace MeditationApp.Converters;

/// <summary>
/// Converter to control media player visibility based on session status and playing state
/// Shows the media player only when session is COMPLETED and is currently playing
/// </summary>
public class MediaPlayerVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length != 2)
            return false;

        // values[0] should be TodaySession.Status (MeditationSessionStatus)
        // values[1] should be IsPlaying (bool)
        
        bool isSessionCompleted = values[0] is MeditationSessionStatus status && status == MeditationSessionStatus.COMPLETED;
        bool isPlaying = values[1] is bool playing && playing;
        
        // Show media player only when session is completed AND is playing
        return isSessionCompleted && isPlaying;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
