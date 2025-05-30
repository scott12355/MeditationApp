namespace MeditationApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("LoginPage", typeof(Views.LoginPage));
        Routing.RegisterRoute("SignUpPage", typeof(Views.SignUpPage));
        Routing.RegisterRoute("VerificationPage", typeof(Views.VerificationPage));
        Routing.RegisterRoute("ProfilePage", typeof(Views.ProfilePage));
        Routing.RegisterRoute("SettingsPage", typeof(Views.SettingsPage));
        Routing.RegisterRoute("DayDetailPage", typeof(Views.DayDetailPage));

        Navigating += OnNavigating;
    }

    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        // Hide TabBar for login, signup, verification pages
        // Show it for other main app pages
        var targetRoute = e.Target.Location.OriginalString;
        bool isAuthFlowPage = targetRoute.Contains("LoginPage") || 
                              targetRoute.Contains("SignUpPage") || 
                              targetRoute.Contains("VerificationPage") ||
                              targetRoute.Contains("SplashPage");

        // MainTabBar.IsVisible = !isAuthFlowPage;
        if (Current.FindByName("MainTabs") is TabBar mainTabBar)
        {
            mainTabBar.IsVisible = !isAuthFlowPage;
        }
    }

    // Expose the TabBar so it can be named in XAML
    public TabBar MainTabBar => (TabBar)CurrentItem.FindByName("MainTabs");
}