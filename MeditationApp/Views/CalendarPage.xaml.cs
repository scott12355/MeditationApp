using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Views;

public partial class CalendarPage : ContentPage
{
    private readonly SimpleCalendarViewModel _viewModel;

    public CalendarPage(SimpleCalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Reset the ViewModel to ensure fresh data for the current user
        _viewModel.Reset();
        
        // Load session days
        await _viewModel.LoadSessionDaysCommand.ExecuteAsync(null);
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
