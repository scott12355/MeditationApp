using Microsoft.Maui.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace MeditationApp.Views;

public partial class OnboardingPage2 : ContentPage
{
    public OnboardingPage2()
    {
        InitializeComponent();
    }

    private void OnSkipClicked(object sender, EventArgs e)
    {
        CompleteOnboarding();
    }

    private void OnGetStartedClicked(object sender, EventArgs e)
    {
        CompleteOnboarding();
    }

    private void CompleteOnboarding()
    {
        // Mark onboarding as complete
        Preferences.Set("HasLaunchedBefore", true);
        
        // Navigate to splash page to handle authentication
        if (Application.Current != null)
        {
            // Get SplashPage from DI and set as main page
            var serviceProvider = Application.Current.Handler?.MauiContext?.Services;
            if (serviceProvider != null)
            {
                var splashPage = serviceProvider.GetRequiredService<SplashPage>();
                Application.Current.MainPage = splashPage;
            }
        }
    }
}
