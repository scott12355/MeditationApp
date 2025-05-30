using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MeditationApp.Services;
using Microsoft.Maui.Storage;

namespace MeditationApp.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private const string NotificationsEnabledKey = "notifications_enabled";
    private const string ReminderTimeKey = "reminder_time";

    private readonly HybridAuthService _hybridAuthService;
    private readonly NotificationService _notificationService;
    
    private string _userName = "John Doe";
    private string _email = "john.doe@example.com";
    private bool _notificationsEnabled = true;
    private TimeSpan _reminderTime = new TimeSpan(19, 30, 0);
    private bool _isNotificationPermissionGranted = false;

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

    public bool IsNotificationPermissionGranted
    {
        get => _isNotificationPermissionGranted;
        private set
        {
            _isNotificationPermissionGranted = value;
            OnPropertyChanged();
        }
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand LogoutCommand { get; }

    public SettingsViewModel(HybridAuthService hybridAuthService, NotificationService notificationService)
    {
        _hybridAuthService = hybridAuthService;
        _notificationService = notificationService;
        SaveSettingsCommand = new Command(OnSaveSettings);
        LogoutCommand = new Command(async () => await OnLogout());
        LoadUserData();
        LoadSettings();
    }

    private async void LoadSettings()
    {
        try
        {
            // Load notification settings
            var notificationsEnabledStr = await SecureStorage.Default.GetAsync(NotificationsEnabledKey);
            if (bool.TryParse(notificationsEnabledStr, out var notificationsEnabled))
            {
                _notificationsEnabled = notificationsEnabled;
                OnPropertyChanged(nameof(NotificationsEnabled));
            }

            // Load reminder time
            var reminderTimeStr = await SecureStorage.Default.GetAsync(ReminderTimeKey);
            if (TimeSpan.TryParse(reminderTimeStr, out var reminderTime))
            {
                _reminderTime = reminderTime;
                OnPropertyChanged(nameof(ReminderTime));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    private async void OnSaveSettings()
    {
        try
        {
            // Save settings to SecureStorage
            await SecureStorage.Default.SetAsync(NotificationsEnabledKey, NotificationsEnabled.ToString());
            await SecureStorage.Default.SetAsync(ReminderTimeKey, ReminderTime.ToString());

            if (NotificationsEnabled)
            {
                // Schedule the notification
                await _notificationService.ScheduleDailyNotification(ReminderTime);
            }
            else
            {
                // Cancel all notifications if disabled
                await _notificationService.CancelAllNotifications();
            }

            // Show confirmation
            if (Application.Current?.Windows?.FirstOrDefault()?.Page != null)
            {
                await Application.Current.Windows.First().Page.DisplayAlert(
                    "Settings Saved",
                    "Your notification preferences have been updated.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            if (Application.Current?.Windows?.FirstOrDefault()?.Page != null)
            {
                await Application.Current.Windows.First().Page.DisplayAlert(
                    "Error",
                    "There was an error saving your settings. Please try again.",
                    "OK");
            }
        }
    }

    private async Task OnLogout()
    {
        if (Application.Current?.MainPage != null)
        {
            var result = await Application.Current.MainPage.DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
            if (result)
            {
                // Properly clear all user session data using HybridAuthService
                await _hybridAuthService.SignOutAsync();
                
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
