using System.Windows.Input;
using MeditationApp.Services;
using Microsoft.Maui.Controls;

namespace MeditationApp.ViewModels;

public class ForgotPasswordViewModel : BindableObject
{
    private readonly HybridAuthService _hybridAuthService;
    private string _email = string.Empty;
    private string _status = string.Empty;
    private string _code = string.Empty;
    private string _newPassword = string.Empty;
    private string _loadingText = string.Empty;
    private bool _isBusy;
    private bool _isCodeSent;

    public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string Code { get => _code; set { _code = value; OnPropertyChanged(); } }
    public string NewPassword { get => _newPassword; set { _newPassword = value; OnPropertyChanged(); } }
    public string LoadingText { get => _loadingText; set { _loadingText = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    public bool IsCodeSent { get => _isCodeSent; set { _isCodeSent = value; OnPropertyChanged(); } }

    public ICommand SendCodeCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand BackToLoginCommand { get; }

    public ForgotPasswordViewModel(HybridAuthService hybridAuthService)
    {
        _hybridAuthService = hybridAuthService;
        SendCodeCommand = new Command(async () => await OnSendCode());
        ConfirmCommand = new Command(async () => await OnConfirm());
        BackToLoginCommand = new Command(async () => await OnBackToLogin());
    }

    private async Task OnSendCode()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            LoadingText = "Sending reset code...";
            Status = string.Empty;
            var result = await _hybridAuthService.ForgotPasswordAsync(Email);
            if (result)
            {
                IsCodeSent = true;
                Status = "A reset code has been sent to your email.";
            }
            else
            {
                Status = "Failed to send reset code. Please check your email and try again.";
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

    private async Task OnConfirm()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            LoadingText = "Resetting password...";
            Status = string.Empty;
            var result = await _hybridAuthService.ConfirmForgotPasswordAsync(Email, Code, NewPassword);
            if (result)
            {
                Status = "Password reset successful. You can now log in.";
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert("Success", "Your password has been reset. Please log in.", "OK");
                    var appShell = new AppShell();
                    Application.Current.MainPage = appShell;
                    await appShell.GoToAsync("LoginPage");
                });
            }
            else
            {
                Status = "Failed to reset password. Please check the code and try again.";
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
        if (Application.Current != null)
        {
            var appShell = new AppShell();
            Application.Current.MainPage = appShell;
            await appShell.GoToAsync("LoginPage");
        }
    }
}