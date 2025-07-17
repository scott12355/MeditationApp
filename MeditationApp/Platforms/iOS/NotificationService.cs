using Foundation;
using UserNotifications;
using MeditationApp.Services;
using UIKit;

namespace MeditationApp.Platforms.iOS;

public class iOSNotificationService : INotificationService
{
    private const string NotificationIdentifier = "meditation_reminder";
    private const string SessionReminderIdentifier = "session_reminder";

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

            // Remove any existing daily reminders first
            center.RemovePendingNotificationRequests(new string[] { NotificationIdentifier });

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

    public async Task ShowNotification(string title, string message)
    {
        var initialLog = $"ShowNotification called with title: '{title}', message: '{message}'";
        System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {initialLog}");
        NotificationLogger.Log($"[iOSNotificationService] {initialLog}");
        
        try
        {
            var center = UNUserNotificationCenter.Current;
            
            // Check if notifications are authorized
            var settings = await center.GetNotificationSettingsAsync();
            var authLog = $"Notification authorization status: {settings.AuthorizationStatus}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {authLog}");
            NotificationLogger.Log($"[iOSNotificationService] {authLog}");
            
            var detailLog = $"Alert: {settings.AlertSetting}, Badge: {settings.BadgeSetting}, Sound: {settings.SoundSetting}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {detailLog}");
            NotificationLogger.Log($"[iOSNotificationService] {detailLog}");
            
            if (settings.AuthorizationStatus != UNAuthorizationStatus.Authorized)
            {
                var errorLog = "Notifications not authorized - cannot show notification";
                System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {errorLog}");
                NotificationLogger.Log($"[iOSNotificationService] {errorLog}");
                
                // Fallback: Show alert if notifications are not authorized and app is in foreground
                if (UIApplication.SharedApplication.ApplicationState == UIApplicationState.Active)
                {
                    await ShowFallbackAlert(title, message);
                }
                return;
            }

            // Create notification content with enhanced properties for better background delivery
            var content = new UNMutableNotificationContent
            {
                Title = title,
                Body = message,
                Sound = UNNotificationSound.Default,
                Badge = NSNumber.FromInt32(1), // Add badge to ensure it's noticeable
                CategoryIdentifier = "MEDITATION_SESSION", // Add category for better handling
                ThreadIdentifier = "meditation_updates" // Group related notifications
            };

            // Add user info for better handling and debugging
            content.UserInfo = NSDictionary.FromObjectsAndKeys(
                new object[] { 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "meditation_status",
                    UIApplication.SharedApplication.ApplicationState.ToString()
                },
                new object[] { 
                    "timestamp", 
                    "category",
                    "app_state_when_sent"
                }
            );

            // Check app state to determine delivery strategy
            var appState = UIApplication.SharedApplication.ApplicationState;
            var stateLog = $"Current app state: {appState}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {stateLog}");
            NotificationLogger.Log($"[iOSNotificationService] {stateLog}");
            
            // For background notifications, use immediate delivery
            // iOS requires immediate triggers for local notifications to work in background
            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(0.1, false);
            var triggerLog = "Using immediate trigger for notification delivery";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {triggerLog}");
            NotificationLogger.Log($"[iOSNotificationService] {triggerLog}");
            
            var uniqueId = "meditation_" + DateTime.Now.Ticks; // Use unique identifier to avoid conflicts
            var request = UNNotificationRequest.FromIdentifier(uniqueId, content, trigger);
            
            await center.AddNotificationRequestAsync(request);
            var successLog = $"Notification request added successfully with ID: {uniqueId}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {successLog}");
            NotificationLogger.Log($"[iOSNotificationService] {successLog}");
            
            // Additionally, for background scenarios, try to force delivery by requesting background time
            if (appState != UIApplicationState.Active)
            {
                var bgLog = "App is in background, attempting to ensure notification delivery";
                System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {bgLog}");
                NotificationLogger.Log($"[iOSNotificationService] {bgLog}");
                
                // Request background processing time to ensure notification is processed
                var bgTaskId = UIApplication.SharedApplication.BeginBackgroundTask(() =>
                {
                    var expireLog = "Background task time expired";
                    System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {expireLog}");
                    NotificationLogger.Log($"[iOSNotificationService] {expireLog}");
                });
                
                // Give the system a moment to process the notification
                await Task.Delay(500);
                
                if (bgTaskId != UIApplication.BackgroundTaskInvalid)
                {
                    UIApplication.SharedApplication.EndBackgroundTask(bgTaskId);
                    var completeLog = "Background task completed";
                    System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {completeLog}");
                    NotificationLogger.Log($"[iOSNotificationService] {completeLog}");
                }
            }
            
            // Log pending notifications for debugging
            var pendingRequests = await center.GetPendingNotificationRequestsAsync();
            var deliveredNotifications = await center.GetDeliveredNotificationsAsync();
            var statsLog = $"Total pending notifications: {pendingRequests.Length}, Total delivered notifications: {deliveredNotifications.Length}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {statsLog}");
            NotificationLogger.Log($"[iOSNotificationService] {statsLog}");
        }
        catch (Exception ex)
        {
            var errorLog = $"Error showing notification: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {errorLog}");
            NotificationLogger.Log($"[iOSNotificationService] {errorLog}");
            
            var stackLog = $"Stack trace: {ex.StackTrace}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {stackLog}");
            NotificationLogger.Log($"[iOSNotificationService] {stackLog}");
        }
    }
    
    private async Task ShowFallbackAlert(string title, string message)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var window = UIApplication.SharedApplication.KeyWindow;
                var viewController = window?.RootViewController;
                
                while (viewController?.PresentedViewController != null)
                {
                    viewController = viewController.PresentedViewController;
                }
                
                if (viewController != null)
                {
                    var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    await viewController.PresentViewControllerAsync(alert, true);
                    System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] Fallback alert shown");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] Error showing fallback alert: {ex.Message}");
        }
    }

    public async Task ShowDelayedNotification(string title, string message, int delayInSeconds)
    {
        var initialLog = $"ShowDelayedNotification called with title: '{title}', message: '{message}', delay: {delayInSeconds}s";
        System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {initialLog}");
        NotificationLogger.Log($"[iOSNotificationService] {initialLog}");
        
        try
        {
            var center = UNUserNotificationCenter.Current;
            
            // Check if notifications are authorized
            var settings = await center.GetNotificationSettingsAsync();
            var authLog = $"Notification authorization status: {settings.AuthorizationStatus}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {authLog}");
            NotificationLogger.Log($"[iOSNotificationService] {authLog}");
            
            if (settings.AuthorizationStatus != UNAuthorizationStatus.Authorized)
            {
                var errorLog = "Notifications not authorized - cannot show delayed notification";
                System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {errorLog}");
                NotificationLogger.Log($"[iOSNotificationService] {errorLog}");
                return;
            }

            // Create notification content with enhanced properties for better background delivery
            var content = new UNMutableNotificationContent
            {
                Title = title,
                Body = message,
                Sound = UNNotificationSound.Default,
                Badge = NSNumber.FromInt32(1), // Add badge to ensure it's noticeable
                CategoryIdentifier = "MEDITATION_TEST", // Add category for better handling
                ThreadIdentifier = "meditation_test_updates" // Group related notifications
            };

            // Add user info for better handling and debugging
            content.UserInfo = NSDictionary.FromObjectsAndKeys(
                new object[] { 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "meditation_delayed_test",
                    UIApplication.SharedApplication.ApplicationState.ToString(),
                    delayInSeconds.ToString()
                },
                new object[] { 
                    "timestamp", 
                    "category",
                    "app_state_when_scheduled",
                    "delay_seconds"
                }
            );

            // Check app state to determine delivery strategy
            var appState = UIApplication.SharedApplication.ApplicationState;
            var stateLog = $"Current app state: {appState}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {stateLog}");
            NotificationLogger.Log($"[iOSNotificationService] {stateLog}");
            
            // Use time interval trigger for delayed notifications
            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(delayInSeconds, false);
            var triggerLog = $"Using time interval trigger for {delayInSeconds}s delay";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {triggerLog}");
            NotificationLogger.Log($"[iOSNotificationService] {triggerLog}");
            
            var uniqueId = "meditation_delayed_" + DateTime.Now.Ticks; // Use unique identifier to avoid conflicts
            var request = UNNotificationRequest.FromIdentifier(uniqueId, content, trigger);
            
            await center.AddNotificationRequestAsync(request);
            var successLog = $"Delayed notification request added successfully with ID: {uniqueId}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {successLog}");
            NotificationLogger.Log($"[iOSNotificationService] {successLog}");
            
            // Log pending notifications for debugging
            var pendingRequests = await center.GetPendingNotificationRequestsAsync();
            var deliveredNotifications = await center.GetDeliveredNotificationsAsync();
            var statsLog = $"Total pending notifications: {pendingRequests.Length}, Total delivered notifications: {deliveredNotifications.Length}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {statsLog}");
            NotificationLogger.Log($"[iOSNotificationService] {statsLog}");
            
            // Log details of all pending notifications
            foreach (var req in pendingRequests)
            {
                var reqLog = $"Pending notification ID: {req.Identifier}, Title: {req.Content.Title}";
                System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {reqLog}");
                NotificationLogger.Log($"[iOSNotificationService] {reqLog}");
            }
        }
        catch (Exception ex)
        {
            var errorLog = $"Error showing delayed notification: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {errorLog}");
            NotificationLogger.Log($"[iOSNotificationService] {errorLog}");
            
            var stackLog = $"Stack trace: {ex.StackTrace}";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {stackLog}");
            NotificationLogger.Log($"[iOSNotificationService] {stackLog}");
        }
    }

    public async Task ScheduleSessionReminder(int delayInSeconds)
    {
        var center = UNUserNotificationCenter.Current;
        var logMsg = $"ScheduleSessionReminder called with delay: {delayInSeconds}s";
        System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {logMsg}");
        NotificationLogger.Log($"[iOSNotificationService] {logMsg}");

        // Check authorization before scheduling
        var settings = await center.GetNotificationSettingsAsync();
        var authLog = $"AuthorizationStatus for reminders: {settings.AuthorizationStatus}";
        System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {authLog}");
        NotificationLogger.Log($"[iOSNotificationService] {authLog}");
        if (settings.AuthorizationStatus != UNAuthorizationStatus.Authorized)
        {
            var warnLog = "Cannot schedule session reminder: notifications not authorized";
            System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {warnLog}");
            NotificationLogger.Log($"[iOSNotificationService] {warnLog}");
            return;
        }

        // Remove any existing session reminder
        center.RemovePendingNotificationRequests(new string[]{ SessionReminderIdentifier });

        // Create content
        var content = new UNMutableNotificationContent
        {
            Title = "Session Reminder",
            Body = "Come back to listen to your meditation session.",
            Sound = UNNotificationSound.Default
        };
        // Create trigger
        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(delayInSeconds, false);
        // Create request
        var request = UNNotificationRequest.FromIdentifier(SessionReminderIdentifier, content, trigger);
        // Schedule
        await center.AddNotificationRequestAsync(request);

        var pending = await center.GetPendingNotificationRequestsAsync();
        var pendingLog = $"Total pending reminders after schedule: {pending.Length}";
        System.Diagnostics.Debug.WriteLine($"[iOSNotificationService] {pendingLog}");
        NotificationLogger.Log($"[iOSNotificationService] {pendingLog}");
    }

    public Task CancelSessionReminder()
    {
        var center = UNUserNotificationCenter.Current;
        center.RemovePendingNotificationRequests(new string[]{ SessionReminderIdentifier });
        return Task.CompletedTask;
    }
}