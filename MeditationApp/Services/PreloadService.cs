using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Services;

public class PreloadService
{
    private readonly MeditationSessionDatabase _database;
    private readonly CalendarDataService _calendarDataService;
    private CalendarViewModel? _preloadedCalendarViewModel;
    private bool _isCalendarPreloading;
    private bool _isCalendarPreloaded;

    public PreloadService(MeditationSessionDatabase database, CalendarDataService calendarDataService)
    {
        _database = database;
        _calendarDataService = calendarDataService;
    }

    /// <summary>
    /// Start preloading calendar data in the background
    /// </summary>
    public async Task PreloadCalendarAsync()
    {
        System.Diagnostics.Debug.WriteLine("PreloadService: Starting calendar preload...");
        
        if (_isCalendarPreloading || _isCalendarPreloaded)
        {
            System.Diagnostics.Debug.WriteLine($"PreloadService: Skipping preload - already preloading: {_isCalendarPreloading}, already preloaded: {_isCalendarPreloaded}");
            return;
        }

        _isCalendarPreloading = true;

        try
        {
            System.Diagnostics.Debug.WriteLine("PreloadService: Preloading calendar data via CalendarDataService...");
            
            // Use the shared calendar data service for preloading
            await Task.Run(async () =>
            {
                await _calendarDataService.PreloadCalendarDataAsync();
                
                // Create calendar view model - it will use the preloaded data
                _preloadedCalendarViewModel = new CalendarViewModel(_database, _calendarDataService);
            });

            _isCalendarPreloaded = true;
            System.Diagnostics.Debug.WriteLine("PreloadService: Calendar preload completed successfully!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PreloadService: Calendar preload error: {ex.Message}");
        }
        finally
        {
            _isCalendarPreloading = false;
        }
    }

    /// <summary>
    /// Get the preloaded calendar view model, or create a new one if not available
    /// </summary>
    public CalendarViewModel GetOrCreateCalendarViewModel()
    {
        if (_isCalendarPreloaded && _preloadedCalendarViewModel != null)
        {
            System.Diagnostics.Debug.WriteLine("PreloadService: Using preloaded calendar view model!");
            var viewModel = _preloadedCalendarViewModel;
            
            // Reset for next time
            _preloadedCalendarViewModel = null;
            _isCalendarPreloaded = false;
            
            return viewModel;
        }

        // Fallback to creating new instance
        System.Diagnostics.Debug.WriteLine("PreloadService: Creating new calendar view model (preload not available)");
        return new CalendarViewModel(_database, _calendarDataService);
    }

    /// <summary>
    /// Check if calendar data is preloaded and ready
    /// </summary>
    public bool IsCalendarPreloaded => _isCalendarPreloaded && _preloadedCalendarViewModel != null;

    /// <summary>
    /// Clear preloaded data to free memory
    /// </summary>
    public void ClearPreloadedData()
    {
        _preloadedCalendarViewModel = null;
        _isCalendarPreloaded = false;
        _isCalendarPreloading = false;
        
        // Also clear the shared calendar data service cache
        _calendarDataService.ClearCache();
    }
}
