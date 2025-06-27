using MeditationApp.ViewModels;
using MeditationApp.Services;
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

public partial class ForgotPasswordPage : UraniumContentPage
{
#if IOS
    NSObject? _keyboardShowObserver;
    NSObject? _keyboardHideObserver;
#endif
    public ForgotPasswordPage()
    {
        InitializeComponent();
        var hybridAuthService = ((App)Application.Current).Services.GetRequiredService<HybridAuthService>();
        BindingContext = new ForgotPasswordViewModel(hybridAuthService);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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

    private async void NewPasswordField_Focused(object sender, FocusEventArgs e)
    {
        // Wait a short moment to ensure keyboard is up
        await Task.Delay(100);
        await ForgotScrollView.ScrollToAsync(NewPasswordField, ScrollToPosition.Center, true);
    }
}