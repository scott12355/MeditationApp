using MeditationApp.Views;
using MeditationApp.Services;

namespace MeditationApp;

public partial class App : Application
{
    private readonly NotificationService _notificationService;

    public App(NotificationService notificationService)
    {
        InitializeComponent();
        _notificationService = notificationService;

        MainPage = new AppShell();
        RequestNotificationPermission();

        // Register routes
        Routing.RegisterRoute("LoginPage", typeof(LoginPage));
        Routing.RegisterRoute("SplashPage", typeof(SplashPage));
    }

    private async void RequestNotificationPermission()
    {
        try
        {
            // Request notification permission when app starts
            var granted = await _notificationService.RequestNotificationPermission();
            if (granted)
            {
                System.Diagnostics.Debug.WriteLine("Notification permission granted");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Notification permission denied");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error requesting notification permission: {ex.Message}");
        }
    }

    protected override void OnStart()
    {
        base.OnStart();
        // Authentication check is now handled by SplashPage
        // No need to navigate here - Shell will start with SplashPage by default
    }
}