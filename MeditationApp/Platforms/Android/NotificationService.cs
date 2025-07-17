using Android.App;
using Android.Content;
using Android.OS;
using MeditationApp.Services;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;

namespace MeditationApp.Platforms.Android;

public class AndroidNotificationService : INotificationService
{
    private const string ChannelId = "meditation_reminders";
    private const string ChannelName = "Meditation Reminders";
    private const string ChannelDescription = "Daily reminders for your meditation practice";
    private const int NotificationId = 100;
    private const string ActionScheduleNotification = "com.meditationapp.SCHEDULE_NOTIFICATION";
    private const string ActionCancelNotification = "com.meditationapp.CANCEL_NOTIFICATION";
    private const string ActionSessionReminder = "com.meditationapp.SESSION_REMINDER";

    private Context? _context;
    private AlarmManager? _alarmManager;
    private PendingIntent? _pendingIntent;

    private Context Context => _context ??= Platform.CurrentActivity ?? 
        throw new InvalidOperationException("Current activity is not available. Make sure to call this method after the app is fully initialized.");

    private AlarmManager AlarmManager => _alarmManager ??= (AlarmManager)Context.GetSystemService(Context.AlarmService) ?? 
        throw new InvalidOperationException("Alarm service is not available");

    private PendingIntent PendingIntent => _pendingIntent ??= CreatePendingIntent();

    private PendingIntent CreatePendingIntent()
    {
        var intent = new Intent(Context, typeof(NotificationReceiver));
        intent.SetAction(ActionScheduleNotification);
        return PendingIntent.GetBroadcast(
            Context,
            NotificationId,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    public async Task<bool> RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                return status == PermissionStatus.Granted;
            }
            return true;
        }
        return true; // For Android versions below 13, notification permission is granted by default
    }

    public async Task ScheduleDailyNotification(TimeSpan reminderTime)
    {
        try
        {
            // Create notification channel (required for Android 8.0 and above)
            CreateNotificationChannel();

            // Calculate the first trigger time
            var now = DateTime.Now;
            var triggerTime = DateTime.Today.Add(reminderTime);
            if (triggerTime <= now)
            {
                triggerTime = triggerTime.AddDays(1);
            }

            // Convert to milliseconds since epoch
            var triggerTimeMillis = DateTimeOffset.UtcNow
                .Add(triggerTime - DateTime.Now)
                .ToUnixTimeMilliseconds();

            // Set the alarm
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                AlarmManager.SetExactAndAllowWhileIdle(
                    AlarmType.RtcWakeup,
                    triggerTimeMillis,
                    PendingIntent);
            }
            else
            {
                AlarmManager.SetExact(
                    AlarmType.RtcWakeup,
                    triggerTimeMillis,
                    PendingIntent);
            }

            System.Diagnostics.Debug.WriteLine($"Scheduled notification for {triggerTime}");
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
            AlarmManager.Cancel(PendingIntent);
            
            // Also cancel any existing notifications
            var notificationManager = NotificationManagerCompat.From(Context);
            notificationManager.CancelAll();
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error canceling notifications: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                ChannelName,
                NotificationImportance.High)
            {
                Description = ChannelDescription
            };

            var notificationManager = (NotificationManager)Context.GetSystemService(Context.NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
        }
    }

    public Task ShowNotification(string title, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[AndroidNotificationService] ShowNotification called with title: '{title}', message: '{message}'");
        try
        {
            CreateNotificationChannel();
            var builder = new NotificationCompat.Builder(Context, ChannelId)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetAutoCancel(true);
            var manager = NotificationManagerCompat.From(Context);
            manager.Notify(NotificationId + 1, builder.Build());
            System.Diagnostics.Debug.WriteLine($"[AndroidNotificationService] Notification sent successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing notification: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public async Task ShowDelayedNotification(string title, string message, int delayInSeconds)
    {
        var initialLog = $"ShowDelayedNotification called with title: '{title}', message: '{message}', delay: {delayInSeconds}s";
        System.Diagnostics.Debug.WriteLine($"[AndroidNotificationService] {initialLog}");
        NotificationLogger.Log($"[AndroidNotificationService] {initialLog}");
        
        try
        {
            // Create notification channel if needed
            CreateNotificationChannel();
            
            var stateLog = $"Creating delayed notification for {delayInSeconds} seconds";
            System.Diagnostics.Debug.WriteLine($"[AndroidNotificationService] {stateLog}");
            NotificationLogger.Log($"[AndroidNotificationService] {stateLog}");
            
            // For Android, we'll use a simple Task.Delay approach since we're not in a background service
            _ = Task.Run(async () =>
            {
                await Task.Delay(delayInSeconds * 1000);
                
                var delayedLog = $"Delayed notification firing now after {delayInSeconds}s";
                System.Diagnostics.Debug.WriteLine($"[AndroidNotificationService] {delayedLog}");
                NotificationLogger.Log($"[AndroidNotificationService] {delayedLog}");
                
                await ShowNotification(title, message);
            });
            
            var successLog = $"Delayed notification scheduled successfully";
            System.Diagnostics.Debug.WriteLine($"[AndroidNotificationService] {successLog}");
            NotificationLogger.Log($"[AndroidNotificationService] {successLog}");
        }
        catch (Exception ex)
        {
            var errorLog = $"Error showing delayed notification: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[AndroidNotificationService] {errorLog}");
            NotificationLogger.Log($"[AndroidNotificationService] {errorLog}");
            
            var stackLog = $"Stack trace: {ex.StackTrace}";
            System.Diagnostics.Debug.WriteLine($"[AndroidNotificationService] {stackLog}");
            NotificationLogger.Log($"[AndroidNotificationService] {stackLog}");
        }
    }

    public Task ScheduleSessionReminder(int delayInSeconds)
    {
        CreateNotificationChannel();
        var now = DateTime.Now;
        var triggerTimeMillis = DateTimeOffset.UtcNow.AddSeconds(delayInSeconds).ToUnixTimeMilliseconds();
        var intent = new Intent(Context, typeof(NotificationReceiver));
        intent.SetAction(ActionSessionReminder);
        var pending = PendingIntent.GetBroadcast(
            Context,
            NotificationId + 2,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            AlarmManager.SetExactAndAllowWhileIdle(
                AlarmType.RtcWakeup,
                triggerTimeMillis,
                pending);
        }
        else
        {
            AlarmManager.SetExact(
                AlarmType.RtcWakeup,
                triggerTimeMillis,
                pending);
        }
    }

    public Task CancelSessionReminder()
    {
        CreateNotificationChannel();
        var intent = new Intent(Context, typeof(NotificationReceiver));
        intent.SetAction(ActionSessionReminder);
        var pending = PendingIntent.GetBroadcast(
            Context,
            NotificationId + 2,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        AlarmManager.Cancel(pending);
        return Task.CompletedTask;
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(new[] { "com.meditationapp.SCHEDULE_NOTIFICATION" })]
public class NotificationReceiver : BroadcastReceiver
{
    public override void OnReceive(Context context, Intent intent)
    {
        if (intent.Action == "com.meditationapp.SCHEDULE_NOTIFICATION")
        {
            ShowNotification(context);
            ScheduleNextNotification(context);
        }
    }

    private void ShowNotification(Context context)
    {
        var builder = new NotificationCompat.Builder(context, "meditation_reminders")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle("Time to Meditate")
            .SetContentText("Take a moment to find peace and clarity through meditation.")
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true);

        var notificationManager = NotificationManagerCompat.From(context);
        notificationManager.Notify(100, builder.Build());
    }

    private void ScheduleNextNotification(Context context)
    {
        // Reschedule for next day
        var alarmManager = (AlarmManager)context.GetSystemService(Context.AlarmService);
        var intent = new Intent(context, typeof(NotificationReceiver));
        intent.SetAction("com.meditationapp.SCHEDULE_NOTIFICATION");
        var pendingIntent = PendingIntent.GetBroadcast(
            context,
            100,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var nextDay = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            alarmManager.SetExactAndAllowWhileIdle(
                AlarmType.RtcWakeup,
                nextDay,
                pendingIntent);
        }
        else
        {
            alarmManager.SetExact(
                AlarmType.RtcWakeup,
                nextDay,
                pendingIntent);
        }
    }
}