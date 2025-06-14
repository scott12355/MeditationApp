using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using MeditationApp.Models;

namespace MeditationApp.Converters;

/// <summary>
/// Converter that ensures both session exists and has COMPLETED status
/// Only returns true when session is not null AND status is COMPLETED
/// </summary>
public class SessionCompletedVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length != 2)
            return false;

        // values[0] should be TodaySession (MeditationSession object)
        // values[1] should be TodaySession.Status (MeditationSessionStatus)
        
        bool hasSession = values[0] != null;
        bool isCompleted = values[1] is MeditationSessionStatus status && status == MeditationSessionStatus.COMPLETED;
        
        // Only show when both conditions are met
        return hasSession && isCompleted;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
