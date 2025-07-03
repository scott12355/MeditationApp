using System.Globalization;

namespace MeditationApp.Converters
{
    /// <summary>
    /// Converter to determine if premium lock icon should be visible
    /// Shows lock icon only if technique is premium AND user doesn't have subscription
    /// </summary>
    public class PremiumLockVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return false;

            // values[0] should be IsPremium (bool)
            // values[1] should be HasPremiumSubscription (bool)
            
            if (values[0] is bool isPremium && values[1] is bool hasPremiumSubscription)
            {
                // Show lock icon only if technique is premium AND user doesn't have subscription
                return isPremium && !hasPremiumSubscription;
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
