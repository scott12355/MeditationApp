using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Views;

public partial class CalendarPage : ContentPage
{
    public CalendarPage(SwipeCalendarViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("SettingsPage");
    }
}
