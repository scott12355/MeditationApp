using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace MeditationApp.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationService _platformNotificationService;

    public NotificationService()
    {
#if IOS
        _platformNotificationService = new Platforms.iOS.iOSNotificationService();
#elif ANDROID
        _platformNotificationService = new Platforms.Android.AndroidNotificationService();
#else
        _platformNotificationService = new DefaultNotificationService();
#endif
    }

    public async Task<bool> RequestNotificationPermission()
    {
        return await _platformNotificationService.RequestNotificationPermission();
    }

    public async Task ScheduleDailyNotification(TimeSpan reminderTime)
    {
        await _platformNotificationService.ScheduleDailyNotification(reminderTime);
        
        // Show a toast for immediate feedback
        var toast = Toast.Make("Daily meditation reminder scheduled", ToastDuration.Short);
        await toast.Show();
    }

    public async Task CancelAllNotifications()
    {
        await _platformNotificationService.CancelAllNotifications();
        
        // Show a toast for immediate feedback
        var toast = Toast.Make("Meditation reminders cancelled", ToastDuration.Short);
        await toast.Show();
    }

    public async Task ShowNotification(string title, string message)
    {
        var logMessage = $"ShowNotification called with title: '{title}', message: '{message}'";
        System.Diagnostics.Debug.WriteLine($"[NotificationService] {logMessage}");
        NotificationLogger.Log($"[NotificationService] {logMessage}");
        
        await _platformNotificationService.ShowNotification(title, message);
    }

    public async Task ShowDelayedNotification(string title, string message, int delayInSeconds)
    {
        var initialLog = $"ShowDelayedNotification called with title: '{title}', message: '{message}', delay: {delayInSeconds}s";
        System.Diagnostics.Debug.WriteLine($"[NotificationService] {initialLog}");
        NotificationLogger.Log($"[NotificationService] {initialLog}");
        
        // Use the platform-specific implementation
        await _platformNotificationService.ShowDelayedNotification(title, message, delayInSeconds);
    }

    // Session reminder scheduling delegated to platform services
    public async Task ScheduleSessionReminder(int delayInSeconds)
    {
        // Ensure we have permission before scheduling
        var granted = await RequestNotificationPermission();
        if (!granted)
        {
            var warn = $"Session reminder not scheduled: notification permission not granted";
            System.Diagnostics.Debug.WriteLine($"[NotificationService] {warn}");
            NotificationLogger.Log($"[NotificationService] {warn}");
            return;
        }
        await _platformNotificationService.ScheduleSessionReminder(delayInSeconds);
    }

    public async Task CancelSessionReminder()
    {
        // Cancellation does not require permission
        await _platformNotificationService.CancelSessionReminder();
    }
}

// Default implementation for platforms that don't support notifications
internal class DefaultNotificationService : INotificationService
{
    public Task<bool> RequestNotificationPermission()
    {
        return Task.FromResult(false);
    }

    public Task ScheduleDailyNotification(TimeSpan reminderTime)
    {
        return Task.CompletedTask;
    }

    public Task CancelAllNotifications()
    {
        return Task.CompletedTask;
    }

    public Task ShowNotification(string title, string message)
    {
        return Task.CompletedTask;
    }

    public Task ShowDelayedNotification(string title, string message, int delayInSeconds)
    {
        return Task.CompletedTask;
    }

    // Stub out session reminder methods to satisfy the interface
    public Task ScheduleSessionReminder(int delayInSeconds)
    {
        return Task.CompletedTask;
    }

    public Task CancelSessionReminder()
    {
        return Task.CompletedTask;
    }
}