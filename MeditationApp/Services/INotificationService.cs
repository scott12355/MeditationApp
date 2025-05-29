namespace MeditationApp.Services;

public interface INotificationService
{
    Task<bool> RequestNotificationPermission();
    Task ScheduleDailyNotification(TimeSpan reminderTime);
    Task CancelAllNotifications();
} 