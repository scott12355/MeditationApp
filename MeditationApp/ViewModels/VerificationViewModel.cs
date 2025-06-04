using System.Windows.Input;
using MeditationApp.Services;
using Microsoft.Maui.Controls;

namespace MeditationApp.ViewModels;

public class VerificationViewModel : BindableObject
{
    private readonly CognitoAuthService _cognitoAuthService;
    private string _username = string.Empty;
    private string _code = string.Empty;
    private string _status = string.Empty;
    private string _loadingText = string.Empty;
    private bool _isBusy;
    private string _password = string.Empty;
    private string _email = string.Empty;
    private string _firstName = string.Empty;

    public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
    public string Code { get => _code; set { _code = value; OnPropertyChanged(); } }
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string LoadingText { get => _loadingText; set { _loadingText = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
    public string Email { get => _email; set { _email = value; OnPropertyChanged(); OnPropertyChanged(nameof(EmailMessage)); } }
    public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); OnPropertyChanged(nameof(WelcomeMessage)); } }

    public string WelcomeMessage => string.IsNullOrEmpty(FirstName) ? "Verify Your Account" : $"Welcome, {FirstName}!";
    public string EmailMessage => string.IsNullOrEmpty(Email) ? "" : $"We've sent a verification code to {Email}";

    public ICommand VerifyCommand { get; }
    public ICommand ResendCodeCommand { get; }
    public ICommand BackToLoginCommand { get; }

    public VerificationViewModel(CognitoAuthService cognitoAuthService)
    {
        _cognitoAuthService = cognitoAuthService;
        VerifyCommand = new Command(async () => await OnVerify());
        ResendCodeCommand = new Command(async () => await OnResendCode());
        BackToLoginCommand = new Command(async () => await Shell.Current.GoToAsync("..", animate: true));
    }

    private async Task OnVerify()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (string.IsNullOrEmpty(Code))
            {
                Status = "Please enter the verification code";
                return;
            }
            
            LoadingText = "Verifying your account...";
            Status = string.Empty;
            
            var result = await _cognitoAuthService.ConfirmSignUpAsync(Username, Code);
            if (result)
            {
                // Try to auto-login
                if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                {
                    LoadingText = "Logging you in...";
                    var response = await _cognitoAuthService.SignInAsync(Username, Password);
                    if (response.IsSuccess)
                    {
                        await SecureStorage.Default.SetAsync("access_token", response.AccessToken ?? string.Empty);
                        await SecureStorage.Default.SetAsync("id_token", response.IdToken ?? string.Empty);
                        await SecureStorage.Default.SetAsync("refresh_token", response.RefreshToken ?? string.Empty);
                        
                        LoadingText = "Redirecting...";
                        await Task.Delay(500); // Brief delay to show success state
                        
                        // Ensure navigation happens on main thread
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            try
                            {
                                await Shell.Current.GoToAsync("//SplashPage", animate: true);
                                System.Diagnostics.Debug.WriteLine("Navigation to Splash completed successfully");
                            }
                            catch (Exception navEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Navigation error: {navEx.Message}");
                            }
                        });
                        return;
                    }
                    else
                    {
                        Status = response.ErrorMessage ?? "Login failed after verification";
                    }
                }
                // Fallback: show alert and go to login
                await Application.Current?.MainPage?.DisplayAlert("Success", "Your account has been verified. You can now login.", "OK");
                await Shell.Current.GoToAsync("..", animate: true);
            }
            else
            {
                Status = "Failed to verify account. Please try again.";
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

    private async Task OnResendCode()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            LoadingText = "Resending verification code...";
            Status = string.Empty;
            
            var result = await _cognitoAuthService.ResendConfirmationCodeAsync(Username);
            if (result)
            {
                Status = "A new verification code has been sent to your email.";
            }
            else
            {
                Status = "Failed to resend verification code. Please try again.";
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
}
