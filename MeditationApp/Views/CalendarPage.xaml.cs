using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Views;

public partial class CalendarPage : ContentPage
{
    private readonly SwipeCalendarViewModel _viewModel;

    public CalendarPage(SwipeCalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Force clear any static data that might be contaminated from previous users
        SwipeCalendarViewModel.SelectedDayData = null;
        
        // Reset and reload the ViewModel to ensure fresh data for the current user
        _viewModel.Reset();
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("SettingsPage");
    }
}
