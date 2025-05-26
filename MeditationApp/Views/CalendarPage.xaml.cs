using MeditationApp.ViewModels;
using MeditationApp.Services;
using MeditationApp.Controls;

namespace MeditationApp.Views;

public partial class CalendarPage : ContentPage
{
    public CalendarPage(MeditationSessionDatabase database)
    {
        InitializeComponent();
        BindingContext = new CalendarViewModel(database);
        
        // Create and configure the CalendarView with database dependency
        var calendarView = new CalendarView(database);
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
