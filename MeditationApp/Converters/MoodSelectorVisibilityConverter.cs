using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace MeditationApp.Converters;

/// <summary>
/// Converter to control mood selector visibility based on expansion state and selected mood
/// </summary>
public class MoodSelectorVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.Length != 2)
            return false;

        // values[0] should be IsMoodSelectorExpanded (bool)
        // values[1] should be SelectedMood (int?)
        
        bool isMoodSelectorExpanded = values[0] is bool expanded && expanded;
        bool hasSelectedMood = values[1] != null;
        
        // Parameter indicates which state we're checking for
        string state = parameter?.ToString() ?? "";
        
        switch (state.ToLower())
        {
            case "expanded":
                // Show expanded selector when it's expanded OR when no mood is selected
                return isMoodSelectorExpanded || !hasSelectedMood;
                
            case "collapsed":
                // Show collapsed frame only when NOT expanded AND a mood is selected
                return !isMoodSelectorExpanded && hasSelectedMood;
                
            case "opacity":
                // Return opacity (1.0 or 0.0) instead of boolean for smooth transitions
                return (isMoodSelectorExpanded || !hasSelectedMood) ? 1.0 : 0.0;
                
            default:
                return false;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
