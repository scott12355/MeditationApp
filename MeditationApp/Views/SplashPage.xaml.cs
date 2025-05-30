using MeditationApp.Services;
using MeditationApp.ViewModels;

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
        if (_hasNavigated) return;

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

            // Load today's session
            LoadingLabel.Text = "Loading today's session...";
            await _todayViewModel.EnsureDataLoaded();
            
            // Navigate to main app
            LoadingLabel.Text = "Welcome back!";
            await Task.Delay(600);
            
            _hasNavigated = true;
            await Shell.Current.GoToAsync("//MainTabs", true);
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
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
