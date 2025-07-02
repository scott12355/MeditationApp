using System.Windows.Input;
using MeditationApp.Services;
using Microsoft.Maui.Controls;

namespace MeditationApp.ViewModels;

public class VerificationViewModel : BindableObject
{
    private readonly CognitoAuthService _cognitoAuthService;
    private readonly HybridAuthService _hybridAuthService;
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

    public VerificationViewModel(CognitoAuthService cognitoAuthService, HybridAuthService hybridAuthService)
    {
        _cognitoAuthService = cognitoAuthService;
        _hybridAuthService = hybridAuthService;
        VerifyCommand = new Command(async () => await OnVerify());
        ResendCodeCommand = new Command(async () => await OnResendCode());
        BackToLoginCommand = new Command(async () => {
            if (Application.Current != null)
            {
                var appShell = new AppShell();
                Application.Current.MainPage = appShell;
                await appShell.GoToAsync("LoginPage", animate: true);
            }
        });
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
                // Try to auto-login using HybridAuthService (this will properly store user profile with UUID)
                if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                {
                    LoadingText = "Logging you in...";
                    var authResult = await _hybridAuthService.SignInAsync(Username, Password);
                    if (authResult.IsSuccess)
                    {
                        LoadingText = "Setting up your profile...";
                        
                        // Store user info for RevenueCat (same pattern as LoginViewModel)
                        if (!authResult.IsOfflineMode)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    // Store email in secure storage for later use during purchases
                                    await SecureStorage.Default.SetAsync("user_email", Email);
                                    System.Diagnostics.Debug.WriteLine($"[VerificationViewModel] Stored user email: {Email}");
                                    
                                    // Get user profile and store basic info (this will have the UUID now)
                                    var profile = await _hybridAuthService.GetUserProfileAsync();
                                    if (profile != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[VerificationViewModel] Got user profile - Email: '{profile.Email ?? "NULL"}', FirstName: '{profile.FirstName ?? "NULL"}', LastName: '{profile.LastName ?? "NULL"}', Username: '{profile.Username ?? "NULL"}'");
                                        
                                        // Store user ID (username) - should be UUID now
                                        if (!string.IsNullOrEmpty(profile.Username))
                                        {
                                            await SecureStorage.Default.SetAsync("user_id", profile.Username);
                                            System.Diagnostics.Debug.WriteLine($"[VerificationViewModel] Stored user ID: {profile.Username}");
                                            
                                            // Check if it looks like a UUID vs email
                                            if (profile.Username.Contains("@"))
                                            {
                                                System.Diagnostics.Debug.WriteLine("[VerificationViewModel] WARNING: Username looks like an email, not a UUID!");
                                            }
                                            else if (profile.Username.Contains("-") && profile.Username.Length > 30)
                                            {
                                                System.Diagnostics.Debug.WriteLine("[VerificationViewModel] SUCCESS: Username looks like a UUID!");
                                            }
                                        }
                                        
                                        if (!string.IsNullOrEmpty(profile.FirstName))
                                        {
                                            Preferences.Set("user_first_name", profile.FirstName);
                                            System.Diagnostics.Debug.WriteLine($"[VerificationViewModel] Stored first name: {profile.FirstName}");
                                        }
                                        if (!string.IsNullOrEmpty(profile.LastName))
                                        {
                                            Preferences.Set("user_last_name", profile.LastName);
                                            System.Diagnostics.Debug.WriteLine($"[VerificationViewModel] Stored last name: {profile.LastName}");
                                        }
                                        
                                        System.Diagnostics.Debug.WriteLine($"[VerificationViewModel] Stored user info for RevenueCat: {profile.Email}, {profile.FirstName} {profile.LastName}, ID: {profile.Username}");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine("[VerificationViewModel] No user profile returned from GetUserProfileAsync()");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[VerificationViewModel] Error setting up user profile: {ex.Message}");
                                }
                            });
                        }

                        LoadingText = "Redirecting...";
                        await Task.Delay(500); // Brief delay to show success state

                        // Ensure navigation happens on main thread
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            try
                            {
                                if (Application.Current != null)
                                {
                                    Application.Current.MainPage = new AppShell();
                                }
                                System.Diagnostics.Debug.WriteLine("Navigation to main app completed successfully");
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
                        Status = authResult.Message ?? "Login failed after verification";
                    }
                }
                // Fallback: show alert and go to login
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Success", "Your account has been verified. You can now login.", "OK");
                }
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
