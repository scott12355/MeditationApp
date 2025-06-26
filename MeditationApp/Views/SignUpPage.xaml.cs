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
        _viewModel = new SignUpViewModel(cognitoAuthService);
        BindingContext = _viewModel;

        // Subscribe to a sign-up success event
        _viewModel.SignUpSucceeded += async (s, e) =>
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Username", _viewModel.Email ?? string.Empty },
                { "Password", _viewModel.Password ?? string.Empty },
                { "Email", _viewModel.Email ?? string.Empty },
                { "FirstName", _viewModel.FirstName ?? string.Empty }
            };
            var verificationPage = new MeditationApp.Views.VerificationPage();
            verificationPage.ApplyQueryAttributes(navigationParameter);
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Microsoft.Maui.Controls.Application.Current.MainPage = verificationPage;
            });
        };

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
