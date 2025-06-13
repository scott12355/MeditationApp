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
        Routing.RegisterRoute("PastSessionsPage", typeof(Views.CalendarPage));

        Navigating += OnNavigating;
    }

    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        // No longer need TabBar visibility logic since we removed tabs
        // Navigation is now handled through standard Shell navigation
    }
}