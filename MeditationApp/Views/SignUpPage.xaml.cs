using MeditationApp.Services;
using MeditationApp.ViewModels;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UraniumUI.Pages;
using Microsoft.Maui.Platform;
#if ANDROID
using Android.Views;
#endif
#if IOS
using UIKit;
using Foundation;
#endif

namespace MeditationApp.Views;

public partial class SignUpPage : UraniumContentPage
{
    private readonly SignUpViewModel _viewModel;
#if IOS
    NSObject? _keyboardShowObserver;
    NSObject? _keyboardHideObserver;
#endif
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
            // Use DI to get the VerificationPage
            var verificationPage = ((App)Microsoft.Maui.Controls.Application.Current).Services.GetRequiredService<Views.VerificationPage>();
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

#if IOS
        _keyboardShowObserver = UIKeyboard.Notifications.ObserveWillShow((sender, args) =>
        {
            var keyboardHeight = args.FrameEnd.Height;
            this.Padding = new Thickness(0, 0, 0, keyboardHeight);
        });
        _keyboardHideObserver = UIKeyboard.Notifications.ObserveWillHide((sender, args) =>
        {
            this.Padding = new Thickness(0);
        });
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if IOS
        _keyboardShowObserver?.Dispose();
        _keyboardHideObserver?.Dispose();
#endif
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnTermsOfServiceTapped(object sender, EventArgs e)
    {
        // Navigate to Terms of Service page or open in browser
        try
        {
            await Browser.OpenAsync("https://lucen.uk/terms-and-conditions", BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            // Handle error - could show an alert or log the error
            await DisplayAlert("Error", "Unable to open Terms of Service", "OK");
        }
    }

    private async void OnPrivacyPolicyTapped(object sender, EventArgs e)
    {
        // Navigate to Privacy Policy page or open in browser
        try
        {
            await Browser.OpenAsync("https://lucen.uk/privacy-policy", BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            // Handle error - could show an alert or log the error
            await DisplayAlert("Error", "Unable to open Privacy Policy", "OK");
        }
    }
}
