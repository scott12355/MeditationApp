using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MeditationApp.Services;

namespace MeditationApp.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly HybridAuthService _hybridAuthService;
    
    private string _userName = "John Doe";
    private string _email = "john.doe@example.com";
    private bool _notificationsEnabled = true;
    private TimeSpan _reminderTime = new TimeSpan(8, 0, 0);
    private int _defaultSessionDuration = 10;

    public string UserName
    {
        get => _userName;
        set
        {
            _userName = value;
            OnPropertyChanged();
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            OnPropertyChanged();
        }
    }

    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set
        {
            _notificationsEnabled = value;
            OnPropertyChanged();
        }
    }

    public TimeSpan ReminderTime
    {
        get => _reminderTime;
        set
        {
            _reminderTime = value;
            OnPropertyChanged();
        }
    }

    public int DefaultSessionDuration
    {
        get => _defaultSessionDuration;
        set
        {
            _defaultSessionDuration = value;
            OnPropertyChanged();
        }
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand LogoutCommand { get; }

    public SettingsViewModel(HybridAuthService hybridAuthService)
    {
        _hybridAuthService = hybridAuthService;
        SaveSettingsCommand = new Command(OnSaveSettings);
        LogoutCommand = new Command(OnLogout);
        LoadUserData();
    }

    private async void OnSaveSettings()
    {
        // TODO: Save settings to service
        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert("Settings", "Settings saved successfully!", "OK");
    }

    private async void OnLogout()
    {
        if (Application.Current?.MainPage != null)
        {
            var result = await Application.Current.MainPage.DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
            if (result)
            {
                // Properly clear all user session data using HybridAuthService
                await _hybridAuthService.SignOutAsync();
                
                // Navigate to login page
                await Shell.Current.GoToAsync("///LoginPage");
            }
        }
    }

    private void LoadUserData()
    {
        // TODO: Load actual user data from service
        // For now using sample data
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
