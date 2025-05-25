using System.Windows.Input;
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

    public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); } }
    public string SecondName { get => _secondName; set { _secondName = value; OnPropertyChanged(); } }
    public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
    public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
    public string ConfirmPassword { get => _confirmPassword; set { _confirmPassword = value; OnPropertyChanged(); } }
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string LoadingText { get => _loadingText; set { _loadingText = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

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
            if (result)
            {
                LoadingText = "Redirecting to verification...";
                await Task.Delay(500); // Brief delay to show success state
                
                if (Navigation != null)
                {
                    var navigationParameter = new Dictionary<string, object> { { "Username", Email }, { "Password", Password } };
                    await Shell.Current.GoToAsync("VerificationPage", navigationParameter);
                }
            }
            else
            {
                Status = "Failed to create account. Please try again.";
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

    public void ClearFields()
    {
        FirstName = string.Empty;
        SecondName = string.Empty;
        Email = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        Status = string.Empty;
        LoadingText = string.Empty;
    }
}
