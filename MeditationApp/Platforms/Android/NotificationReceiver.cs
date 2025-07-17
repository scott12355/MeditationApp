using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace MeditationApp.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = false)]
    [IntentFilter(new[] { "com.meditationapp.SCHEDULE_NOTIFICATION", "com.meditationapp.SESSION_REMINDER", Intent.ActionBootCompleted })]
    public class NotificationReceiver : BroadcastReceiver
    {
        private const string ChannelId = "meditation_reminders";

        public override void OnReceive(Context context, Intent intent)
        {
            // Ensure the notification channel exists
            CreateNotificationChannel(context);

            var manager = NotificationManagerCompat.From(context);
            string title;
            string message;
            int notificationId;

            switch (intent.Action)
            {
                case NotificationServiceActionSchedule:
                    title = "Time to Meditate";
                    message = "Take a moment to find peace and clarity—start your meditation session.";
                    notificationId = 100;
                    break;
                case NotificationServiceActionSessionReminder:
                    title = "Session Reminder";
                    message = "Come back to listen to your meditation session.";
                    notificationId = 102;
                    break;
                default:
                    // Do nothing for other actions
                    return;
            }

            var notification = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetAutoCancel(true)
                .SetPriority(NotificationCompat.PriorityHigh)
                .Build();

            manager.Notify(notificationId, notification);
        }

        private void CreateNotificationChannel(Context context)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var name = "Meditation Reminders";
                var descriptionText = "Notifications to remind you to meditate and return to your session";
                var importance = NotificationImportance.High;
                var channel = new NotificationChannel(ChannelId, name, importance)
                {
                    Description = descriptionText
                };
                var notificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService);
                notificationManager?.CreateNotificationChannel(channel);
            }
        }

        private const string NotificationServiceActionSchedule = "com.meditationapp.SCHEDULE_NOTIFICATION";
        private const string NotificationServiceActionSessionReminder = "com.meditationapp.SESSION_REMINDER";
    }
}
