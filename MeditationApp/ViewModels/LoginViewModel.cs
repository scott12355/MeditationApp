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
                    await Task.Delay(50); // Brief delay to show success state
                }

                // Set up RevenueCat user after successful login (but don't block on it)
                if (!result.IsOfflineMode)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Store email in secure storage for later use during purchases
                            await SecureStorage.Default.SetAsync("user_email", Email);
                            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Stored user email: {Email}");
                            
                            // Get user profile and store basic info
                            var profile = await _hybridAuthService.GetUserProfileAsync();
                            if (profile != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Got user profile - Email: '{profile.Email ?? "NULL"}', FirstName: '{profile.FirstName ?? "NULL"}', LastName: '{profile.LastName ?? "NULL"}', Username: '{profile.Username ?? "NULL"}'");
                                
                                // Store user ID (username) - but check if it's actually populated
                                if (!string.IsNullOrEmpty(profile.Username))
                                {
                                    await SecureStorage.Default.SetAsync("user_id", profile.Username);
                                    System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Stored user ID: {profile.Username}");
                                    
                                    // Check if it looks like a UUID vs email
                                    if (profile.Username.Contains("@"))
                                    {
                                        System.Diagnostics.Debug.WriteLine("[LoginViewModel] WARNING: Username looks like an email, not a UUID! You may need to log out and log back in.");
                                    }
                                    else if (profile.Username.Contains("-") && profile.Username.Length > 30)
                                    {
                                        System.Diagnostics.Debug.WriteLine("[LoginViewModel] SUCCESS: Username looks like a UUID!");
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("[LoginViewModel] WARNING: profile.Username is null or empty! Not storing user_id.");
                                    // Try using email as user ID fallback
                                    if (!string.IsNullOrEmpty(profile.Email))
                                    {
                                        await SecureStorage.Default.SetAsync("user_id", profile.Email);
                                        System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Using email as user ID fallback: {profile.Email}");
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(profile.FirstName))
                                {
                                    Preferences.Set("user_first_name", profile.FirstName);
                                    System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Stored first name: {profile.FirstName}");
                                }
                                if (!string.IsNullOrEmpty(profile.LastName))
                                {
                                    Preferences.Set("user_last_name", profile.LastName);
                                    System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Stored last name: {profile.LastName}");
                                }
                                
                                System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Stored user info for RevenueCat: {profile.Email}, {profile.FirstName} {profile.LastName}, ID: {profile.Username}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[LoginViewModel] No user profile returned from GetUserProfileAsync()");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] RevenueCat user setup error: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Stack trace: {ex.StackTrace}");
                        }
                    });
                }

                // Ensure navigation happens on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // After successful login, reset TodayViewModel so splash screen reloads data
                        if (Application.Current is App app)
                        {
                            var todayViewModel = app.Services.GetRequiredService<ViewModels.TodayViewModel>();
                            todayViewModel.Reset();
                            var splashPage = app.Services.GetRequiredService<Views.SplashPage>();
                            Application.Current.MainPage = splashPage;
                        }
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
