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

    private class NotificationData
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public TimeSpan ReminderTime { get; set; }
    }
    
    private NotificationData getNotificationData()
    {
        // This method should retrieve the notification data from your settings or configuration
        // For now, returning a default value for demonstration purposes
        return new NotificationData
        {
            Title = "Time to Meditate",
            Body = NotificationBodies[new Random().Next(NotificationBodies.Length)],
            ReminderTime = TimeSpan.FromHours(9) // Default reminder time at 9 AM
        };
    }
    
    private string[] NotificationBodies => new[]
    {
        "Take a moment to find peace and clarity—start your meditation journey with Lucen.",
        "Breathe deeply and let go of your stress. Lucen is here to guide your meditation.",
        "Center your mind and focus on your breath with Lucen's calming sessions.",
        "Remember to take a break. Lucen makes it easy to meditate with your personalised session.",
        "Find your calm amidst the chaos. Open Lucen and begin your meditation now."
    };

    private UNNotificationContent MapToNotificationContent(NotificationData data)
    {
        var content = new UNMutableNotificationContent
        {
            Title = data.Title,
            Body = data.Body,
            Sound = UNNotificationSound.Default
        };
        return content;
    }

    public async Task ScheduleDailyNotification(TimeSpan reminderTime)
    {
        try
        {
            var center = UNUserNotificationCenter.Current;

            // Remove any existing notifications first
            center.RemoveAllPendingNotificationRequests();

            // Get notification data and map it to UNNotificationContent
            var notificationData = getNotificationData();
            UNNotificationContent content = MapToNotificationContent(notificationData);

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
            await center.AddNotificationRequestAsync(request);
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