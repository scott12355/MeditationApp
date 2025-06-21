using MeditationApp.Services;
using MeditationApp.ViewModels;
using UraniumUI.Pages;

namespace MeditationApp.Views;

public partial class LoginPage : UraniumContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

#if IOS
        System.Diagnostics.Debug.WriteLine($"AppleSignInButton is {(AppleSignInButton == null ? "null" : "not null")}");
        // Create Apple sign-in button manually
        if (AppleSignInButton != null)
        {
            AppleSignInButton.SignInCompleted += OnAppleSignInCompleted;
            
            // Create handlers manually to avoid registration issues
            AppleSignInButton.Handler = new MeditationApp.Platforms.iOS.Handlers.AppleSignInButtonHandler();
        }
#endif
    }

#if IOS
    private void OnAppleSignInCompleted(object? sender, string? idToken)
    {
        System.Diagnostics.Debug.WriteLine($"AppleSignInCompleted fired. idToken: {idToken}");
        if (!string.IsNullOrEmpty(idToken))
        {
            // Pass the Apple ID token to the ViewModel for authentication
            _ = _viewModel.SignInWithAppleAsync(idToken);
        }
    }
#endif

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.ClearFields();
    }
}
