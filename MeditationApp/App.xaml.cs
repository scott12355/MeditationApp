using MeditationApp.Views;
using MeditationApp.Services;

namespace MeditationApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();

        // Set the initial route to the login page
        Routing.RegisterRoute("LoginPage", typeof(LoginPage));
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Check if the user is already logged in using hybrid authentication
        var hybridAuthService = Handler?.MauiContext?.Services.GetService<HybridAuthService>();
        if (hybridAuthService != null)
        {
            try
            {
                bool isLoggedIn = await hybridAuthService.IsUserLoggedInAsync();
                if (isLoggedIn)
                {
                    // User is logged in (either online or offline), go to main tabs
                    System.Diagnostics.Debug.WriteLine("User is already logged in, navigating to MainTabs");
                    await Shell.Current.GoToAsync("//MainTabs");
                }
                else
                {
                    // User is not logged in, go to login page
                    System.Diagnostics.Debug.WriteLine("User is not logged in, navigating to LoginPage");
                    await Shell.Current.GoToAsync("///LoginPage");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during startup authentication check: {ex.Message}");
                // Fallback to login page
                await Shell.Current.GoToAsync("///LoginPage");
            }
        }
        else
        {
            // Fallback if service is not available
            await Shell.Current.GoToAsync("///LoginPage");
        }
    }
}