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
    }
}