using Foundation;
using UIKit;
using MediaManager;
using ObjCRuntime;
using BackgroundTasks;
using UserNotifications;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace MeditationApp;

// Register attribute to expose AppDelegate to Objective-C runtime
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    
    public override bool FinishedLaunching(UIKit.UIApplication application, Foundation.NSDictionary launchOptions)
    {
        // Initialize MediaManager for iOS lock screen controls
        CrossMediaManager.Current.Init();
        
        // Request notification permission with additional options for background delivery
        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge | UNAuthorizationOptions.ProvidesAppNotificationSettings,
            (granted, error) => { 
                System.Diagnostics.Debug.WriteLine($"[AppDelegate] Notification permission granted: {granted}");
                if (error != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppDelegate] Notification permission error: {error.Description}");
                }
            });
        
        // Set up notification categories for better handling
        SetupNotificationCategories();
        
        // Set notification center delegate to handle foreground notifications
        UNUserNotificationCenter.Current.Delegate = new NotificationCenterDelegate();
            
        // Register BGTask for session status refresh
        BGTaskScheduler.Shared.Register(
            "com.meditationapp.sessionStatusRefresh",
            null,
            task =>
        {
            if (task is BGAppRefreshTask refreshTask)
                HandleSessionStatusRefresh(refreshTask);
            else
                task.SetTaskCompleted(false);
        });
        // Schedule the first refresh
        ScheduleSessionStatusRefresh();
        // Fallback: enable classic background fetch
        UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalMinimum);
        return base.FinishedLaunching(application, launchOptions);
    }
    
    private void SetupNotificationCategories()
    {
        try
        {
            // Create notification category for meditation session updates
            var sessionCategory = UNNotificationCategory.FromIdentifier(
                "MEDITATION_SESSION",
                new UNNotificationAction[0], // No actions for now
                new string[0], // No intent identifiers
                UNNotificationCategoryOptions.None
            );
            
            // Register the categories
            UNUserNotificationCenter.Current.SetNotificationCategories(
                new NSSet<UNNotificationCategory>(sessionCategory)
            );
            
            System.Diagnostics.Debug.WriteLine("[AppDelegate] Notification categories set up successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppDelegate] Error setting up notification categories: {ex.Message}");
        }
    }

    private void ScheduleSessionStatusRefresh()
    {
        var request = new BGAppRefreshTaskRequest("com.meditationapp.sessionStatusRefresh");
        // Schedule at least 1 minute from now
        request.EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(1 * 60);
        NSError? error;
        BGTaskScheduler.Shared.Submit(request, out error);
        if (error != null)
            System.Diagnostics.Debug.WriteLine($"BGTask submit error: {error.LocalizedDescription}");
    }

    private void HandleSessionStatusRefresh(BGAppRefreshTask task)
    {
        // Schedule the next refresh
        ScheduleSessionStatusRefresh();
        // Provide an expiration handler
        task.ExpirationHandler = () => { task.SetTaskCompleted(false); };
        Task.Run(async () =>
        {
            try
            {
                var sessionId = Preferences.Default.Get("current_session_id", string.Empty);
                if (string.IsNullOrEmpty(sessionId))
                {
                    task.SetTaskCompleted(true);
                    return;
                }
                var queryObj = new
                {
                    query = @"query GetMeditationSessionStatus($sessionID: ID!) { getMeditationSessionStatus(sessionID: $sessionID) { status } }",
                    variables = new { sessionID = sessionId }
                };
                var payload = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
                using var client = new HttpClient();
                var resp = await client.PostAsync("https://lhr6w6nilbfovmfs5lt77v7bx4.appsync-api.eu-west-1.amazonaws.com/graphql", payload);
                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    var status = doc.RootElement.GetProperty("data").GetProperty("getMeditationSessionStatus").GetProperty("status").GetString();
                    if (status == "COMPLETED")
                    {
                        var content = new UNMutableNotificationContent
                        {
                            Title = "Session Ready",
                            Body = "Your meditation session is ready to listen to.",
                            Sound = UNNotificationSound.Default
                        };
                        var req = UNNotificationRequest.FromIdentifier(sessionId, content, UNTimeIntervalNotificationTrigger.CreateTrigger(1, false));
                        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(req);
                    }
                    else if (status == "FAILED")
                    {
                        var failContent = new UNMutableNotificationContent
                        {
                            Title = "Session Failed",
                            Body = "We couldn't generate your meditation session. Please try again later.",
                            Sound = UNNotificationSound.Default
                        };
                        var failReq = UNNotificationRequest.FromIdentifier(sessionId + "_fail", failContent, UNTimeIntervalNotificationTrigger.CreateTrigger(1, false));
                        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(failReq);
                    }
                    // Cancel further refresh if session status is final
                    if (status == "COMPLETED" || status == "FAILED")
                    {
                        // Remove scheduled background refresh
                        BGTaskScheduler.Shared.Cancel("com.meditationapp.sessionStatusRefresh");
                        // Clear stored session ID
                        Preferences.Default.Remove("current_session_id");
                        // If completed, user already gets notification below
                        task.SetTaskCompleted(true);
                        return;
                    }
                }
                // complete the task
                task.SetTaskCompleted(true);
            }
            catch
            {
                task.SetTaskCompleted(false);
            }
        });
    }

    /// <summary>
    /// Classic Background Fetch fallback for iOS < BGTaskScheduler support
    /// </summary>
    public override void PerformFetch(UIKit.UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
    {
        Task.Run(async () =>
        {
            try
            {
                var sessionId = Preferences.Default.Get("current_session_id", string.Empty);
                if (string.IsNullOrEmpty(sessionId))
                {
                    completionHandler(UIBackgroundFetchResult.NoData);
                    return;
                }
                var queryObj = new
                {
                    query = @"query GetMeditationSessionStatus($sessionID: ID!) { getMeditationSessionStatus(sessionID: $sessionID) { status } }",
                    variables = new { sessionID = sessionId }
                };
                var payload = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
                using var client = new HttpClient();
                var resp = await client.PostAsync("https://lhr6w6nilbfovmfs5lt77v7bx4.appsync-api.eu-west-1.amazonaws.com/graphql", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    completionHandler(UIBackgroundFetchResult.Failed);
                    return;
                }
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var status = doc.RootElement.GetProperty("data").GetProperty("getMeditationSessionStatus").GetProperty("status").GetString();
                if (status == "COMPLETED")
                {
                    var content = new UNMutableNotificationContent
                    {
                        Title = "Session Ready",
                        Body = "Your meditation session is ready to listen to.",
                        Sound = UNNotificationSound.Default
                    };
                    var req = UNNotificationRequest.FromIdentifier(sessionId, content, UNTimeIntervalNotificationTrigger.CreateTrigger(1, false));
                    await UNUserNotificationCenter.Current.AddNotificationRequestAsync(req);
                    // cleanup
                    BGTaskScheduler.Shared.Cancel("com.meditationapp.sessionStatusRefresh");
                    Preferences.Default.Remove("current_session_id");
                    completionHandler(UIBackgroundFetchResult.NewData);
                    return;
                }
                else if (status == "FAILED")
                {
                    var failContent = new UNMutableNotificationContent
                    {
                        Title = "Session Failed",
                        Body = "We couldn't generate your meditation session. Please try again later.",
                        Sound = UNNotificationSound.Default
                    };
                    var failReq = UNNotificationRequest.FromIdentifier(sessionId + "_fail", failContent, UNTimeIntervalNotificationTrigger.CreateTrigger(1, false));
                    await UNUserNotificationCenter.Current.AddNotificationRequestAsync(failReq);
                    BGTaskScheduler.Shared.Cancel("com.meditationapp.sessionStatusRefresh");
                    Preferences.Default.Remove("current_session_id");
                    completionHandler(UIBackgroundFetchResult.NewData);
                    return;
                }
                completionHandler(UIBackgroundFetchResult.NoData);
            }
            catch
            {
                completionHandler(UIBackgroundFetchResult.Failed);
            }
        });
    }
}

public class NotificationCenterDelegate : UNUserNotificationCenterDelegate
{
    public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
    {
        System.Diagnostics.Debug.WriteLine($"[NotificationDelegate] Will present notification: {notification.Request.Content.Title}");
        System.Diagnostics.Debug.WriteLine($"[NotificationDelegate] Notification body: {notification.Request.Content.Body}");
        System.Diagnostics.Debug.WriteLine($"[NotificationDelegate] Current app state: {UIApplication.SharedApplication.ApplicationState}");
        
        // Show notification even when app is in foreground
        if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
        {
            // iOS 14+ style - use banner and list for better visibility
            completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.List | UNNotificationPresentationOptions.Sound | UNNotificationPresentationOptions.Badge);
        }
        else
        {
            // iOS < 14 style
            completionHandler(UNNotificationPresentationOptions.Alert | UNNotificationPresentationOptions.Sound | UNNotificationPresentationOptions.Badge);
        }
    }
    
    public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
    {
        System.Diagnostics.Debug.WriteLine($"[NotificationDelegate] Did receive notification response: {response.Notification.Request.Content.Title}");
        System.Diagnostics.Debug.WriteLine($"[NotificationDelegate] Response action identifier: {response.ActionIdentifier}");
        
        // Handle notification tap - could navigate to specific screen
        if (response.ActionIdentifier == "com.apple.UNNotificationDefaultActionIdentifier")
        {
            System.Diagnostics.Debug.WriteLine("[NotificationDelegate] User tapped notification");
            // Could add navigation logic here
        }
        
        completionHandler();
    }
}