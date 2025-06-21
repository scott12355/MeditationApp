using MeditationApp.Services;
using MeditationApp.ViewModels;
using System;

namespace MeditationApp.Views;

public partial class SplashPage : ContentPage
{
    private readonly HybridAuthService _hybridAuthService;
    private readonly PreloadService _preloadService;
    private readonly TodayViewModel _todayViewModel;
    private bool _hasNavigated = false;

    public SplashPage(HybridAuthService hybridAuthService, PreloadService preloadService, TodayViewModel todayViewModel)
    {
        InitializeComponent();
        _hybridAuthService = hybridAuthService;
        _preloadService = preloadService;
        _todayViewModel = todayViewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Reset navigation state to handle logout/login cycles
        _hasNavigated = false;
        
        await SplashContent.FadeTo(1, 500, Easing.CubicOut);
        await CheckAuthenticationAndNavigate();
    }

    private async Task CheckAuthenticationAndNavigate()
    {
        if (_hasNavigated) return;

        try
        {
            // Quick auth check
            LoadingLabel.Text = "Checking authentication...";
            if (!await _hybridAuthService.QuickAuthCheckAsync())
            {
                await NavigateToLogin();
                return;
            }

            // Start background tasks
            LoadingLabel.Text = "Loading app data...";
            
            // Verify login status
            if (!await _hybridAuthService.IsUserLoggedInAsync())
            {
                await NavigateToLogin();
                return;
            }

            // Load today's session with timeout
            LoadingLabel.Text = "Loading today's session...";
            
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30))) // 30 second timeout
            {
                try
                {
                    Console.WriteLine("SplashPage: Starting EnsureDataLoaded with 30s timeout");
                    await _todayViewModel.EnsureDataLoaded().WaitAsync(cts.Token);
                    Console.WriteLine("SplashPage: EnsureDataLoaded completed successfully");
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("SplashPage: Data loading timed out after 30 seconds, proceeding anyway");
                    LoadingLabel.Text = "Loading took longer than expected, continuing...";
                    await Task.Delay(1000);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("SplashPage: Data loading was cancelled due to timeout, proceeding anyway");
                    LoadingLabel.Text = "Loading took longer than expected, continuing...";
                    await Task.Delay(1000);
                }
            }
            
            // Navigate to main app
            LoadingLabel.Text = "Welcome back!";
            await Task.Delay(600);
            _hasNavigated = true;
            if (Application.Current != null)
            {
                Application.Current.MainPage = new AppShell();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during splash authentication check: {ex.Message}");
            await NavigateToLogin("Loading error, redirecting...");
        }
    }

    private async Task NavigateToLogin(string? message = null)
    {
        if (message != null)
        {
            LoadingLabel.Text = message;
            await Task.Delay(500);
        }
        else
        {
            LoadingLabel.Text = "Please sign in";
            await Task.Delay(300);
        }
        
        _hasNavigated = true;
        
        // Set LoginPage as the root MainPage to prevent swipe-back
        if (Application.Current != null)
        {
            var loginPage = ((App)Application.Current).Services.GetRequiredService<Views.LoginPage>();
            Application.Current.MainPage = loginPage;
        }
    }
}
