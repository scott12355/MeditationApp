using System;
using MeditationApp.Views;
using MeditationApp.Services;
using MeditationApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace MeditationApp;

public partial class App : Application
{
    private readonly MeditationApp.Services.INotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;

    public App(MeditationApp.Services.INotificationService notificationService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;

        // Check if first launch
        if (!Preferences.Get("HasLaunchedBefore", false))
        {
            // Set OnboardingPage for first launch
            MainPage = serviceProvider.GetRequiredService<Views.OnboardingPage1>();
        }
        else
        {
            // Always show SplashPage on subsequent app starts
            MainPage = serviceProvider.GetRequiredService<Views.SplashPage>();
        }
        RequestNotificationPermission();

        // Register routes
        Routing.RegisterRoute("LoginPage", typeof(LoginPage));
        Routing.RegisterRoute("SplashPage", typeof(SplashPage));
        Routing.RegisterRoute("OnboardingPage1", typeof(Views.OnboardingPage1));
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

    protected override async void OnResume()
    {
        base.OnResume();
        System.Diagnostics.Debug.WriteLine("App resumed from background");

        try
        {
            // Ensure session polling and data resume
            var todayViewModel = _serviceProvider.GetRequiredService<TodayViewModel>();
            await todayViewModel.EnsureDataLoaded();
            System.Diagnostics.Debug.WriteLine("Resumed TodayViewModel data and polling");
            // Resume session polling
            todayViewModel.ResumeSessionPolling();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resuming TodayViewModel: {ex.Message}");
        }
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        System.Diagnostics.Debug.WriteLine("App entering sleep state");
    }

    public IServiceProvider Services => _serviceProvider;
}