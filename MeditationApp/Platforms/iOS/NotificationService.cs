using Foundation;
using UserNotifications;
using MeditationApp.Services;

namespace MeditationApp.Platforms.iOS;

public class iOSNotificationService : INotificationService
{
    private const string NotificationIdentifier = "meditation_reminder";

    public async Task<bool> RequestNotificationPermission()
    {
        var center = UNUserNotificationCenter.Current;
        var options = UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge;
        
        try
        {
            var (granted, error) = await center.RequestAuthorizationAsync(options);
            if (error != null)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting notification permission: {error.Description}");
                return false;
            }
            return granted;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error requesting notification permission: {ex.Message}");
            return false;
        }
    }

    public async Task ScheduleDailyNotification(TimeSpan reminderTime)
    {
        try
        {
            var center = UNUserNotificationCenter.Current;
            
            // Remove any existing notifications first
            center.RemoveAllPendingNotificationRequests();
            
            // Create notification content
            var content = new UNMutableNotificationContent
            {
                Title = "Time to Meditate",
                Body = "Take a moment to find peace and clarity through meditation.",
                Sound = UNNotificationSound.Default
            };

            // Create date components for the trigger
            var now = DateTime.Now;
            var triggerDate = DateTime.Today.Add(reminderTime);
            if (triggerDate <= now)
            {
                triggerDate = triggerDate.AddDays(1);
            }

            var dateComponents = new NSDateComponents
            {
                Hour = triggerDate.Hour,
                Minute = triggerDate.Minute
            };

            // Create the trigger
            var trigger = UNCalendarNotificationTrigger.CreateTrigger(dateComponents, true);

            // Create the request
            var request = UNNotificationRequest.FromIdentifier(
                NotificationIdentifier,
                content,
                trigger);

            // Schedule the notification
            center.AddNotificationRequest(request, null);
            System.Diagnostics.Debug.WriteLine($"Scheduled notification for {triggerDate}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error scheduling notification: {ex.Message}");
        }
    }

    public Task CancelAllNotifications()
    {
        try
        {
            var center = UNUserNotificationCenter.Current;
            center.RemoveAllPendingNotificationRequests();
            center.RemoveAllDeliveredNotifications();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error canceling notifications: {ex.Message}");
            return Task.CompletedTask;
        }
    }
} 