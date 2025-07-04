using System.Windows.Input;
using MeditationApp.Services;
using Microsoft.Maui.Controls;

namespace MeditationApp.ViewModels;

public class LoginViewModel : BindableObject
{
    private readonly HybridAuthService _hybridAuthService;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _status = string.Empty;
    private string _loadingText = string.Empty;
    private bool _isBusy;
    private bool _isOfflineMode;

    public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
    public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string LoadingText { get => _loadingText; set { _loadingText = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    public bool IsOfflineMode { get => _isOfflineMode; set { _isOfflineMode = value; OnPropertyChanged(); } }

    public ICommand LoginCommand { get; }
    public ICommand SignUpCommand { get; }
    public ICommand ForgotPasswordCommand { get; }

    public LoginViewModel(HybridAuthService hybridAuthService)
    {
        _hybridAuthService = hybridAuthService;
        LoginCommand = new Command(async () => await OnLogin());
        SignUpCommand = new Command(async () => await OnSignUp());
        ForgotPasswordCommand = new Command(async () => await OnForgotPassword());

        // Check offline mode on initialization
        _ = Task.Run(async () => IsOfflineMode = await _hybridAuthService.IsOfflineModeAsync());
    }

    private async Task OnLogin()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                Status = "Please enter both email and password";
                return;
            }

            LoadingText = "Signing you in...";
            Status = string.Empty;

            var result = await _hybridAuthService.SignInAsync(Email, Password);
            if (result.IsSuccess)
            {
                LoadingText = "Redirecting...";

                if (result.IsOfflineMode)
                {
                    Status = "Signed in offline - some features may be limited";
                    await Task.Delay(1000); // Show offline message longer
                }
                else
                {
                    await Task.Delay(500); // Brief delay to show success state
                }

                // Ensure navigation happens on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // After successful login, reset TodayViewModel so splash screen reloads data
                        var todayViewModel = ((App)Application.Current).Services.GetRequiredService<ViewModels.TodayViewModel>();
                        todayViewModel.Reset();
                        var splashPage = ((App)Application.Current).Services.GetRequiredService<Views.SplashPage>();
                        Application.Current.MainPage = splashPage;
                        System.Diagnostics.Debug.WriteLine("Navigation to splash screen completed successfully");
                    }
                    catch (Exception navEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Navigation error: {navEx.Message}");
                        Status = $"Navigation error: {navEx.Message}";
                    }
                });
            }
            else
            {
                Status = result.Message;
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            LoadingText = string.Empty;
        }
    }

    private async Task OnSignUp()
    {
        // Set SignUpPage as the root to prevent swipe-back
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current != null)
            {
                var signUpPage = ((App)Application.Current).Services.GetRequiredService<Views.SignUpPage>();
                Application.Current.MainPage = signUpPage;
            }
        });
    }

    private async Task OnForgotPassword()
    {
        // Navigate to ForgotPasswordPage
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current != null)
            {
                var forgotPasswordPage = ((App)Application.Current).Services.GetRequiredService<Views.ForgotPasswordPage>();
                Application.Current.MainPage = forgotPasswordPage;
            }
        });
    }

    public void ClearFields()
    {
        Email = string.Empty;
        Password = string.Empty;
        Status = string.Empty;
    }
}
