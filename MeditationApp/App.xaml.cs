using MeditationApp.Views;
using MeditationApp.Services;

namespace MeditationApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();

        // Register routes
        Routing.RegisterRoute("LoginPage", typeof(LoginPage));
        Routing.RegisterRoute("SplashPage", typeof(SplashPage));
    }

    protected override void OnStart()
    {
        base.OnStart();
        // Authentication check is now handled by SplashPage
        // No need to navigate here - Shell will start with SplashPage by default
    }
}