using MeditationApp.Services;

namespace MeditationApp.Utils;

/// <summary>
/// Utility class for handling premium features and paywall display
/// </summary>
public static class PremiumFeatureHelper
{
    /// <summary>
    /// Checks if user has premium access, shows paywall if not
    /// </summary>
    /// <param name="featureName">Name of the feature being accessed</param>
    /// <returns>True if user has premium access or just purchased, false otherwise</returns>
    public static async Task<bool> CheckPremiumAccessAsync(string featureName = "premium feature")
    {
        try
        {
            var paywallService = new PaywallService();
            
            // Check if user already has subscription
            bool hasSubscription = await paywallService.CheckSubscriptionStatusAsync();
            if (hasSubscription)
            {
                return true;
            }
            
            // Show paywall if no subscription
            var result = await paywallService.ShowPaywallAsync();
            
            if (result.WasPurchased || result.IsRestore)
            {
                // User purchased or restored subscription
                if (Application.Current?.MainPage != null)
                {
                    string message = result.IsRestore 
                        ? "Your subscription has been restored! Enjoy all premium features."
                        : $"Welcome to Premium! You now have access to {featureName} and all other premium features.";
                        
                    await Application.Current.MainPage.DisplayAlert("Success", message, "OK");
                }
                return true;
            }
            
            // User cancelled or purchase failed
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking premium access: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Shows a premium feature locked message with option to upgrade
    /// </summary>
    /// <param name="featureName">Name of the locked feature</param>
    /// <param name="description">Description of what the feature offers</param>
    /// <returns>True if user upgraded, false if cancelled</returns>
    public static async Task<bool> ShowPremiumFeatureLockedAsync(string featureName, string description = "")
    {
        try
        {
            if (Application.Current?.MainPage == null) return false;
            
            string message = string.IsNullOrEmpty(description) 
                ? $"{featureName} is a premium feature."
                : $"{featureName} is a premium feature. {description}";
                
            bool shouldShowPaywall = await Application.Current.MainPage.DisplayAlert(
                "Premium Feature",
                $"{message}\n\nWould you like to upgrade to premium?",
                "Upgrade Now",
                "Maybe Later"
            );
            
            if (shouldShowPaywall)
            {
                return await CheckPremiumAccessAsync(featureName);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing premium feature locked message: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Extension methods to easily check premium access from ContentPages
/// </summary>
public static class ContentPagePremiumExtensions
{
    /// <summary>
    /// Check premium access with automatic paywall display
    /// </summary>
    public static async Task<bool> CheckPremiumAccessAsync(this ContentPage page, string featureName = "premium feature")
    {
        return await PremiumFeatureHelper.CheckPremiumAccessAsync(featureName);
    }
    
    /// <summary>
    /// Show premium feature locked dialog with upgrade option
    /// </summary>
    public static async Task<bool> ShowPremiumLockedAsync(this ContentPage page, string featureName, string description = "")
    {
        return await PremiumFeatureHelper.ShowPremiumFeatureLockedAsync(featureName, description);
    }
}
