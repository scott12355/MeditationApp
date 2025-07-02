using MeditationApp.Views;
#if IOS
using RevenueCat;
using Tonestro.Maui.RevenueCat.iOS.Extensions;
#endif

namespace MeditationApp.Services;

public interface IPaywallService
{
    Task<PaywallResult> ShowPaywallAsync();
    Task<bool> CheckSubscriptionStatusAsync();
}

public class PaywallService : IPaywallService
{
    /// <summary>
    /// Shows the paywall modal and returns the result
    /// </summary>
    public async Task<PaywallResult> ShowPaywallAsync()
    {
        try
        {
            var paywallModal = new PaywallModal();
            return await paywallModal.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing paywall: {ex.Message}");
            return new PaywallResult { WasCancelled = true };
        }
    }

    /// <summary>
    /// Checks if the user has an active subscription
    /// </summary>
    public async Task<bool> CheckSubscriptionStatusAsync()
    {
        try
        {
#if IOS
            var customerInfo = await RCPurchases.SharedPurchases.GetCustomerInfoAsync();
            return customerInfo.Entitlements.Active.Count > 0;
#else 
            await Task.Delay(100); // Placeholder for other platforms
            return false;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking subscription status: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Extension methods to easily show paywall from any page
/// </summary>
public static class PaywallExtensions
{
    /// <summary>
    /// Show paywall from any ContentPage
    /// </summary>
    public static async Task<PaywallResult> ShowPaywallAsync(this ContentPage page)
    {
        var paywallService = new PaywallService();
        return await paywallService.ShowPaywallAsync();
    }

    /// <summary>
    /// Show paywall and handle common scenarios
    /// </summary>
    public static async Task<bool> ShowPaywallWithHandlingAsync(this ContentPage page, string featureName = "premium feature")
    {
        try
        {
            var result = await page.ShowPaywallAsync();
            
            if (result.WasPurchased)
            {
                await page.DisplayAlert("Welcome to Premium!", 
                    $"You now have access to all premium features. Enjoy your {featureName}!", 
                    "OK");
                return true;
            }
            else if (result.IsRestore)
            {
                await page.DisplayAlert("Purchases Restored", 
                    "Your previous purchases have been restored successfully!", 
                    "OK");
                return true;
            }
            
            // User cancelled or purchase failed
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in paywall handling: {ex.Message}");
            await page.DisplayAlert("Error", 
                "There was an error processing your request. Please try again.", 
                "OK");
            return false;
        }
    }
}
