using MeditationApp.Services;
using MeditationApp.ViewModels;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UraniumUI.Pages;

namespace MeditationApp.Views;

public partial class SignUpPage : UraniumContentPage
{
    private readonly SignUpViewModel _viewModel;

    public SignUpPage(CognitoAuthService cognitoAuthService)
    {
        InitializeComponent();
        _viewModel = new SignUpViewModel(cognitoAuthService) { Navigation = Navigation };
        BindingContext = _viewModel;

        // Set iOS specific padding to avoid content going under the status bar
        On<Microsoft.Maui.Controls.PlatformConfiguration.iOS>().SetUseSafeArea(false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.ClearFields();
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
