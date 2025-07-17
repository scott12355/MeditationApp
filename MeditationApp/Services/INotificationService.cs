namespace MeditationApp.Services;

public interface INotificationService
{
    Task<bool> RequestNotificationPermission();
    Task ScheduleDailyNotification(TimeSpan reminderTime);
    Task CancelAllNotifications();
    // Show an immediate notification with title and message
    Task ShowNotification(string title, string message);
    // Show a delayed notification with title, message, and delay in seconds
    Task ShowDelayedNotification(string title, string message, int delayInSeconds);

    // Schedule a one-time session reminder notification (in seconds)
    Task ScheduleSessionReminder(int delayInSeconds);

    // Cancel the scheduled session reminder notification
    Task CancelSessionReminder();
}