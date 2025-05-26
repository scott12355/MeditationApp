using MeditationApp.ViewModels;
using MeditationApp.Services;
using MeditationApp.Controls;

namespace MeditationApp.Views;

public partial class CalendarPage : ContentPage
{
    private readonly PreloadService _preloadService;
    private readonly MeditationSessionDatabase _database;
    private readonly CalendarDataService _calendarDataService;

    public CalendarPage(MeditationSessionDatabase database, PreloadService preloadService, CalendarDataService calendarDataService)
    {
        InitializeComponent();
        _database = database;
        _preloadService = preloadService;
        _calendarDataService = calendarDataService;
        
        // Use preloaded calendar view model if available
        BindingContext = _preloadService.GetOrCreateCalendarViewModel();
        
        // Create and configure the CalendarView with database and calendar data service dependencies
        var calendarView = new CalendarView(database, calendarDataService);
        calendarView.HeightRequest = 400;
        
        // Find the placeholder in XAML and replace with our programmatically created control
        var calendarPlaceholder = this.FindByName<ContentView>("CalendarPlaceholder");
        if (calendarPlaceholder != null)
        {
            calendarPlaceholder.Content = calendarView;
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("SettingsPage");
    }
}
