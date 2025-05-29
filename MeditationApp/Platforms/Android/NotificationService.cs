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