namespace MeditationApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("SplashPage", typeof(Views.SplashPage));
        Routing.RegisterRoute("LoginPage", typeof(Views.LoginPage));
        Routing.RegisterRoute("SignUpPage", typeof(Views.SignUpPage));
        Routing.RegisterRoute("VerificationPage", typeof(Views.VerificationPage));
        Routing.RegisterRoute("ProfilePage", typeof(Views.ProfilePage));
        Routing.RegisterRoute("DayDetailPage", typeof(Views.DayDetailPage));
        Routing.RegisterRoute("OnboardingPage1", typeof(Views.OnboardingPage1));
        Routing.RegisterRoute("OnboardingPage2", typeof(Views.OnboardingPage2));
        Routing.RegisterRoute("BreathingStatsPage", typeof(Views.BreathingStatsPage));

        Navigating += OnNavigating;
    }

    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        // Enable flyout for main app pages, disable for splash/login/onboarding
        var targetRoute = e.Target.Location.OriginalString;
        
        if (targetRoute.Contains("TodayPage") || 
            targetRoute.Contains("CalendarPage") ||
            targetRoute.Contains("BreathingExercisePage") ||
            targetRoute.Contains("BreathingStatsPage") || 
            targetRoute.Contains("SettingsPage"))
        {
            // Enable flyout for main app areas
            FlyoutBehavior = FlyoutBehavior.Flyout;
        }
        else if (targetRoute.Contains("SplashPage") || 
                 targetRoute.Contains("LoginPage") || 
                 targetRoute.Contains("SignUpPage") ||
                 targetRoute.Contains("VerificationPage") ||
                 targetRoute.Contains("OnboardingPage"))
        {
            // Disable flyout for splash, login, and onboarding flows
            FlyoutBehavior = FlyoutBehavior.Disabled;
        }
    }
}