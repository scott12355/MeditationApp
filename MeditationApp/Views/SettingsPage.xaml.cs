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

    private async void OnPrivacyPolicyClicked(object sender, EventArgs e)
    {
        var url = "https://lucen.uk/privacy-policy";
        await Navigation.PushAsync(new WebViewPage(url, "Privacy Policy"));
    }

    private async void OnTermsClicked(object sender, EventArgs e)
    {
        var url = "https://lucen.uk/terms-and-conditions";
        await Navigation.PushAsync(new WebViewPage(url, "Terms & Conditions"));
    }

    private void OnHamburgerClicked(object sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnSubscribeClicked(object sender, EventArgs e)
    {
        if (BindingContext is SettingsViewModel vm && vm.SubscribeCommand != null && vm.SubscribeCommand.CanExecute(null))
        {
            vm.SubscribeCommand.Execute(null);
        }
        else
        {
            await DisplayAlert("Error", "Subscription feature is not available.", "OK");
        }
    }
}
