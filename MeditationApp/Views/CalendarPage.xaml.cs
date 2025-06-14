using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Views;

public partial class CalendarPage : ContentPage
{
    private readonly SimpleCalendarViewModel _viewModel;
    private static DateTime _lastLoadTime = DateTime.MinValue;
    private const int REFRESH_COOLDOWN_SECONDS = 30; // Shorter cooldown to allow more frequent refreshes
    private bool _isLoadingData = false; // Prevent concurrent loading

    public CalendarPage(SimpleCalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        
        // Wire up the refresh command
        CalendarRefreshView.Command = new Command(async () => await RefreshCalendarData());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Prevent concurrent loading
        if (_isLoadingData) return;
        
        // Check if we need to load data
        var timeSinceLastLoad = DateTime.Now - _lastLoadTime;
        var shouldLoad = timeSinceLastLoad.TotalSeconds > REFRESH_COOLDOWN_SECONDS || 
                        _viewModel.DaysWithSessions.Count == 0; // Always load if ViewModel is empty
        
        if (shouldLoad)
        {
            await LoadCalendarData();
        }
    }

    /// <summary>
    /// Loads the calendar data and marks it as loaded globally
    /// </summary>
    private async Task LoadCalendarData()
    {
        if (_isLoadingData) return; // Prevent concurrent loading
        
        _isLoadingData = true;
        try
        {
            // Reset the ViewModel to ensure fresh data for the current user
            _viewModel.Reset();
            
            // Load session days
            await _viewModel.LoadSessionDaysCommand.ExecuteAsync(null);
            
            _lastLoadTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading calendar data: {ex.Message}");
        }
        finally
        {
            _isLoadingData = false;
        }
    }

    /// <summary>
    /// Resets the global loaded flag to force a refresh on next appearance
    /// Call this when user logs out/in or when data needs to be refreshed
    /// </summary>
    public static void ResetLoadedFlag()
    {
        _lastLoadTime = DateTime.MinValue;
    }

    /// <summary>
    /// Manually refreshes the calendar data
    /// Call this when new sessions are created or when user wants to refresh
    /// </summary>
    public async Task RefreshCalendarData()
    {
        // Force refresh by resetting the time
        _lastLoadTime = DateTime.MinValue;
        
        await LoadCalendarData();
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("SettingsPage");
    }

    private async void OnBackToTodayClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
