using System.Windows.Input;
using System.Linq;
using MeditationApp.Services;
using Microsoft.Maui.Controls;

namespace MeditationApp.ViewModels;

public class SignUpViewModel : BindableObject
{
    private readonly CognitoAuthService _cognitoAuthService;
    private string _firstName = string.Empty;
    private string _secondName = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _status = string.Empty;
    private string _loadingText = string.Empty;
    private bool _isBusy;
    private bool _hasPasswordError = false;
    private bool _hasEmailError = false;
    private bool _hasConfirmPasswordError = false;
    private string _passwordErrorMessage = string.Empty;

    public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); } }
    public string SecondName { get => _secondName; set { _secondName = value; OnPropertyChanged(); } }
    public string Email { get => _email; set { _email = value; OnPropertyChanged(); ValidateEmail(); } }
    public string Password { get => _password; set { _password = value; OnPropertyChanged(); ValidatePassword(); } }
    public string ConfirmPassword { get => _confirmPassword; set { _confirmPassword = value; OnPropertyChanged(); ValidateConfirmPassword(); } }
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string LoadingText { get => _loadingText; set { _loadingText = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    public bool HasPasswordError { get => _hasPasswordError; set { _hasPasswordError = value; OnPropertyChanged(); } }
    public bool HasEmailError { get => _hasEmailError; set { _hasEmailError = value; OnPropertyChanged(); } }
    public bool HasConfirmPasswordError { get => _hasConfirmPasswordError; set { _hasConfirmPasswordError = value; OnPropertyChanged(); } }
    public string PasswordErrorMessage { get => _passwordErrorMessage; set { _passwordErrorMessage = value; OnPropertyChanged(); } }

    public ICommand SignUpCommand { get; }
    public ICommand BackToLoginCommand { get; }

    public INavigation? Navigation { get; set; }

    public SignUpViewModel(CognitoAuthService cognitoAuthService)
    {
        _cognitoAuthService = cognitoAuthService;
        SignUpCommand = new Command(async () => await OnSignUp());
        BackToLoginCommand = new Command(async () => await OnBackToLogin());
    }

    private async Task OnSignUp()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (string.IsNullOrEmpty(FirstName) ||
                string.IsNullOrEmpty(SecondName) ||
                string.IsNullOrEmpty(Email) ||
                string.IsNullOrEmpty(Password) ||
                string.IsNullOrEmpty(ConfirmPassword))
            {
                Status = "Please fill in all fields";
                return;
            }
            if (Password != ConfirmPassword)
            {
                Status = "Passwords do not match";
                return;
            }
            
            LoadingText = "Creating your account...";
            Status = string.Empty;
            
            var result = await _cognitoAuthService.SignUpAsync(
                Email, Email, Password, FirstName, SecondName);
            if (result.IsSuccess)
            {
                LoadingText = "Redirecting to verification...";
                await Task.Delay(500); // Brief delay to show success state
                
                if (Navigation != null)
                {
                    var navigationParameter = new Dictionary<string, object> 
                    { 
                        { "Username", Email }, 
                        { "Password", Password },
                        { "Email", Email },
                        { "FirstName", FirstName }
                    };
                    await Shell.Current.GoToAsync("VerificationPage", navigationParameter);
                }
            }
            else
            {
                Status = result.ErrorMessage;
                
                // Set field-specific error states based on error type
                if (result.ErrorCode == "InvalidPasswordException")
                {
                    HasPasswordError = true;
                    PasswordErrorMessage = result.ErrorMessage;
                }
                else if (result.ErrorCode == "UsernameExistsException")
                {
                    HasEmailError = true;
                }
                else if (result.ErrorCode == "InvalidParameterException")
                {
                    HasEmailError = true;
                }
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

    private async Task OnBackToLogin()
    {
        if (Navigation != null)
            await Shell.Current.GoToAsync("..", animate: true);
    }

    private void ValidateEmail()
    {
        HasEmailError = !string.IsNullOrEmpty(Email) && !IsValidEmail(Email);
    }

    private void ValidatePassword()
    {
        if (string.IsNullOrEmpty(Password))
        {
            HasPasswordError = false;
            PasswordErrorMessage = string.Empty;
        }
        else if (!IsValidPassword(Password))
        {
            HasPasswordError = true;
            if (Password.Length < 8)
            {
                PasswordErrorMessage = "Password must be at least 8 characters long";
            }
            else
            {
                PasswordErrorMessage = "Password must contain at least one special character";
            }
        }
        else
        {
            HasPasswordError = false;
            PasswordErrorMessage = string.Empty;
        }
        
        // Also validate confirm password when password changes
        ValidateConfirmPassword();
    }

    private void ValidateConfirmPassword()
    {
        HasConfirmPasswordError = !string.IsNullOrEmpty(ConfirmPassword) && Password != ConfirmPassword;
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidPassword(string password)
    {
        // Password must be at least 8 characters and contain at least one special character
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return false;
        
        // Check for at least one special character
        var specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
        return password.Any(c => specialChars.Contains(c));
    }

    public void ClearFields()
    {
        FirstName = string.Empty;
        SecondName = string.Empty;
        Email = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        Status = string.Empty;
        LoadingText = string.Empty;
        HasPasswordError = false;
        HasEmailError = false;
        HasConfirmPasswordError = false;
        PasswordErrorMessage = string.Empty;
    }
}
