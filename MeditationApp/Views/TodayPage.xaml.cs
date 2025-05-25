using MeditationApp.ViewModels;

namespace MeditationApp.Views;

public partial class TodayPage : ContentPage
{
    public TodayPage()
    {
        InitializeComponent();
        BindingContext = new TodayViewModel();
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("SettingsPage");
    }
}
