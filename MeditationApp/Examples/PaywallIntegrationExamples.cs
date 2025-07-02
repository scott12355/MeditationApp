/**
 * PAYWALL INTEGRATION EXAMPLES
 * 
 * This file contains examples of how to integrate the new PaywallModal into your pages.
 * The paywall system provides a clean, consistent way to handle premium feature access
 * throughout the app.
 */

using MeditationApp.Utils;
using MeditationApp.Services;

namespace MeditationApp.Examples;

public class PaywallIntegrationExamples
{
    /**
     * EXAMPLE 1: Simple premium feature check from any ContentPage
     * 
     * This is the easiest way to check if a user has premium access.
     * If they don't, the paywall will automatically be shown.
     */
    public async Task Example1_SimpleCheck(ContentPage page)
    {
        // Check if user has premium access, show paywall if not
        bool hasPremium = await page.CheckPremiumAccessAsync("Advanced Meditation Analytics");
        
        if (hasPremium)
        {
            // User has premium - proceed with premium feature
            await NavigateToAdvancedAnalytics();
        }
        else
        {
            // User cancelled paywall or purchase failed
            // No additional action needed - user stays on current page
        }
    }

    /**
     * EXAMPLE 2: Show premium locked message first, then paywall
     * 
     * This shows a more descriptive message about what the premium feature offers
     * before showing the paywall.
     */
    public async Task Example2_PremiumLockedMessage(ContentPage page)
    {
        bool upgraded = await page.ShowPremiumLockedAsync(
            "Offline Downloads", 
            "Download your favorite meditations and listen anywhere, even without internet connection."
        );
        
        if (upgraded)
        {
            // User upgraded - enable offline features
            await EnableOfflineDownloads();
        }
    }

    /**
     * EXAMPLE 3: Using the helper class directly
     * 
     * For use in ViewModels or other non-ContentPage classes
     */
    public async Task Example3_HelperClass()
    {
        bool hasPremium = await PremiumFeatureHelper.CheckPremiumAccessAsync("Premium Meditation Library");
        
        if (hasPremium)
        {
            // User has premium access
            LoadPremiumMeditations();
        }
    }

    /**
     * EXAMPLE 4: Using PaywallService directly for advanced scenarios
     * 
     * For custom handling of paywall results
     */
    public async Task Example4_DirectService()
    {
        var paywallService = new PaywallService();
        
        // Check current subscription status first
        bool hasSubscription = await paywallService.CheckSubscriptionStatusAsync();
        
        if (!hasSubscription)
        {
            // Show paywall
            var result = await paywallService.ShowPaywallAsync();
            
            if (result.WasPurchased)
            {
                // Handle new purchase
                await OnNewPurchase();
            }
            else if (result.IsRestore)
            {
                // Handle restored purchase
                await OnRestoredPurchase();
            }
            else if (result.WasCancelled)
            {
                // Handle cancellation
                await OnPaywallCancelled();
            }
        }
    }

    /**
     * EXAMPLE 5: Button click handler in code-behind
     * 
     * How to handle a premium button click in a page's code-behind
     */
    public async void OnPremiumFeatureButtonClicked(object sender, EventArgs e)
    {
        // var page = (ContentPage)((Button)sender).GetValue(ContentPage.ParentProperty);
        //
        // bool hasPremium = await page.CheckPremiumAccessAsync("Premium Breathing Exercises");
        //
        // if (hasPremium)
        // {
        //     // Navigate to premium breathing exercises
        //     await Shell.Current.GoToAsync("PremiumBreathingPage");
        // }
    }

    /**
     * EXAMPLE 6: ViewModel integration
     * 
     * How to integrate paywall checks in ViewModels using dependency injection
     */
    public class ExampleViewModel
    {
        private readonly IPaywallService _paywallService;

        public ExampleViewModel(IPaywallService paywallService)
        {
            _paywallService = paywallService;
        }

        public async Task<bool> AccessPremiumFeatureAsync()
        {
            // Check subscription status
            bool hasSubscription = await _paywallService.CheckSubscriptionStatusAsync();
            
            if (!hasSubscription)
            {
                // Show paywall
                var result = await _paywallService.ShowPaywallAsync();
                return result.WasPurchased || result.IsRestore;
            }
            
            return true;
        }
    }

    // Placeholder methods for examples
    private async Task NavigateToAdvancedAnalytics() { /* Implementation */ }
    private async Task EnableOfflineDownloads() { /* Implementation */ }
    private void LoadPremiumMeditations() { /* Implementation */ }
    private async Task OnNewPurchase() { /* Implementation */ }
    private async Task OnRestoredPurchase() { /* Implementation */ }
    private async Task OnPaywallCancelled() { /* Implementation */ }
}
