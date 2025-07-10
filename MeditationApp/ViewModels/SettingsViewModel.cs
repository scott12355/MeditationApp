using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading;
using System.Threading.Tasks;
using MeditationApp.Services;
using Microsoft.Maui.Storage;
#if IOS
using RevenueCat;
using Tonestro.Maui.RevenueCat.iOS.Extensions;
#endif

namespace MeditationApp.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private const string NotificationsEnabledKey = "notifications_enabled";
    private const string ReminderTimeKey = "reminder_time";

    private readonly HybridAuthService _hybridAuthService;
    private readonly NotificationService _notificationService;
    private readonly InAppPurchaseService _inAppPurchaseService;
    private readonly IPaywallService _paywallService;
    
    private string _userName = "John Doe";
    private string _email = "john.doe@example.com";
    private bool _notificationsEnabled = true;
    private TimeSpan _reminderTime = new TimeSpan(19, 30, 0);
    private bool _isNotificationPermissionGranted = false;
    
    // Auto-save debounce management
    private CancellationTokenSource? _autoSaveCts;
    private const int AutoSaveDelayMs = 1000; // 1 second delay

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
            // Auto-save with debounce when notification setting is changed
            TriggerAutoSave();
        }
    }

    public TimeSpan ReminderTime
    {
        get => _reminderTime;
        set
        {
            _reminderTime = value;
            OnPropertyChanged();
            // Auto-save with debounce when time is updated
            TriggerAutoSave();
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
    public ICommand SubscribeCommand { get; }

    public SettingsViewModel(HybridAuthService hybridAuthService, NotificationService notificationService, InAppPurchaseService inAppPurchaseService, IPaywallService paywallService)
    {
        _hybridAuthService = hybridAuthService;
        _notificationService = notificationService;
        _inAppPurchaseService = inAppPurchaseService;
        _paywallService = paywallService;
        SaveSettingsCommand = new Command(OnSaveSettings);
        LogoutCommand = new Command(async () => await OnLogout());
        SubscribeCommand = new Command(async () => await ShowPaywallAsync());
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
            var mainPage = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (mainPage != null)
            {
                await mainPage.DisplayAlert(
                    "Settings Saved",
                    "Your notification preferences have been updated.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            var mainPage = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (mainPage != null)
            {
                await mainPage.DisplayAlert(
                    "Error",
                    "There was an error saving your settings. Please try again.",
                    "OK");
            }
        }
    }

    /// <summary>
    /// Auto-saves time settings when the reminder time is changed
    /// </summary>
    private async Task AutoSaveTimeSettings()
    {
        try
        {
            // Save settings to SecureStorage
            await SecureStorage.Default.SetAsync(NotificationsEnabledKey, NotificationsEnabled.ToString());
            await SecureStorage.Default.SetAsync(ReminderTimeKey, ReminderTime.ToString());

            if (NotificationsEnabled)
            {
                // Schedule the notification with the new time
                await _notificationService.ScheduleDailyNotification(ReminderTime);
            }
            else
            {
                // Cancel all notifications if disabled
                await _notificationService.CancelAllNotifications();
            }

            System.Diagnostics.Debug.WriteLine($"Settings auto-saved: Notifications={NotificationsEnabled}, Time={ReminderTime}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error auto-saving settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Triggers auto-save with debounce to avoid too many save operations
    /// </summary>
    private void TriggerAutoSave()
    {
        // Cancel any existing auto-save operation
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = new CancellationTokenSource();

        var token = _autoSaveCts.Token;

        // Start the debounced auto-save
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(AutoSaveDelayMs, token);
                
                // If not cancelled, proceed with save
                if (!token.IsCancellationRequested)
                {
                    await AutoSaveTimeSettings();
                }
            }
            catch (TaskCanceledException)
            {
                // Debounce was cancelled, which is expected behavior
                System.Diagnostics.Debug.WriteLine("Auto-save debounce cancelled - new change detected");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in debounced auto-save: {ex.Message}");
            }
        }, token);
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
                
                if (Application.Current != null)
                {
                    var appShell = new AppShell();
                    Application.Current.MainPage = appShell;
                    await appShell.GoToAsync("LoginPage");
                }
            }
        }
    }

    private void LoadUserData()
    {
        // TODO: Load actual user data from service
        // For now using sample data
    }


    private async Task ShowPaywallAsync()
    {
        try
        {
            var result = await _paywallService.ShowPaywallAsync();
            
            if (result.WasPurchased)
            {
                var mainPage = Application.Current?.Windows?.FirstOrDefault()?.Page;
                if (mainPage != null)
                {
                    await mainPage.DisplayAlert("Welcome to Premium!", 
                        "You now have access to all premium meditation content. Enjoy your journey!", 
                        "OK");
                }
            }
            else if (result.IsRestore)
            {
                var mainPage = Application.Current?.Windows?.FirstOrDefault()?.Page;
                if (mainPage != null)
                {
                    await mainPage.DisplayAlert("Purchases Restored", 
                        "Your previous purchases have been restored successfully!", 
                        "OK");
                }
            }
            // If cancelled, no action needed
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing paywall: {ex.Message}");
            var mainPage = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (mainPage != null)
            {
                await mainPage.DisplayAlert("Error", 
                    "There was an error loading the subscription options. Please try again.", 
                    "OK");
            }
        }
    }

    // Old RevenueCat implementation replaced with PaywallService
    // The new PaywallModal provides a better user experience with custom UI
    // that matches the app's design system

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = null;
    }
}
