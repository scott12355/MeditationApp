using MeditationApp.Services;

namespace MeditationApp.Views;

public partial class SplashPage : ContentPage
{
    private readonly HybridAuthService _hybridAuthService;
    private readonly PreloadService _preloadService;
    private bool _hasNavigated = false;

    public SplashPage(HybridAuthService hybridAuthService, PreloadService preloadService)
    {
        InitializeComponent();
        _hybridAuthService = hybridAuthService;
        _preloadService = preloadService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasNavigated)
            return;

        // Animate the splash content appearing
        await SplashContent.FadeTo(1, 500, Easing.CubicOut);

        await CheckAuthenticationAndNavigate();
    }

    private async Task CheckAuthenticationAndNavigate()
    {
        if (_hasNavigated)
            return;

        try
        {
            LoadingLabel.Text = "Checking authentication...";

            // First do a quick check to see if we can immediately proceed
            bool quickAuthResult = await _hybridAuthService.QuickAuthCheckAsync();
            
            if (quickAuthResult)
            {
                // Start preloading calendar data in background immediately
                LoadingLabel.Text = "Loading app data...";
                System.Diagnostics.Debug.WriteLine("SplashPage: Starting calendar preload...");
                _ = Task.Run(async () => await _preloadService.PreloadCalendarAsync());
                
                // Do a more thorough check in the background if needed
                LoadingLabel.Text = "Verifying session...";
                bool isLoggedIn = await _hybridAuthService.IsUserLoggedInAsync();
                
                if (isLoggedIn)
                {
                    LoadingLabel.Text = "Welcome back!";
                    await Task.Delay(300); // Brief success message
                    
                    System.Diagnostics.Debug.WriteLine("SplashPage: Navigating to MainTabs");
                    _hasNavigated = true;
                    // Navigate to main app
                    await Shell.Current.GoToAsync("//MainTabs");
                    return;
                }
            }

            // If we reach here, user needs to log in
            LoadingLabel.Text = "Please sign in";
            await Task.Delay(300);
            
            System.Diagnostics.Debug.WriteLine("SplashPage: Navigating to LoginPage");
            _hasNavigated = true;
            // Navigate to login
            await Shell.Current.GoToAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during splash authentication check: {ex.Message}");
            LoadingLabel.Text = "Loading error, redirecting...";
            await Task.Delay(500);
            
            System.Diagnostics.Debug.WriteLine("SplashPage: Error occurred, navigating to LoginPage");
            _hasNavigated = true;
            // Fallback to login page
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
