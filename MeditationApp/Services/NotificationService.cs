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
} 