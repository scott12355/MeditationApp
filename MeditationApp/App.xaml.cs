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
    private readonly NotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;

    public App(NotificationService notificationService, IServiceProvider serviceProvider)
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
            // Get services from the service provider
            var todayViewModel = _serviceProvider.GetRequiredService<TodayViewModel>();
            var databaseSyncService = _serviceProvider.GetRequiredService<DatabaseSyncService>();
            
            // Trigger sync when app becomes active (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    var syncResult = await databaseSyncService.TriggerSyncIfNeededAsync();
                    if (syncResult.IsSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App Resume] Sync completed: {syncResult.SessionsUpdated} sessions, {syncResult.InsightsUpdated} insights updated");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[App Resume] Sync skipped: {syncResult.Message}");
                    }
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[App Resume] Sync failed: {syncEx.Message}");
                }
            });
            
            // Check if we need to refresh (if the date has changed)
            if (todayViewModel.CurrentDate.Date != DateTime.Now.Date)
            {
                System.Diagnostics.Debug.WriteLine("Date changed while app was in background, refreshing data");
                await todayViewModel.LoadTodayData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing data on resume: {ex.Message}");
        }
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        System.Diagnostics.Debug.WriteLine("App entering sleep state");
    }
}