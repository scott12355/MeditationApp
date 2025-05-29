using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnViewProfileClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("ProfilePage");
    }
}
