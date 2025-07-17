using System.Text;

namespace MeditationApp.Services;

public static class NotificationLogger
{
    private static readonly List<string> _logs = new List<string>();
    private static readonly object _lock = new object();
    private const int MAX_LOGS = 50;

    public static void Log(string message)
    {
        lock (_lock)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            
            _logs.Add(logEntry);
            
            // Keep only the last 50 logs
            if (_logs.Count > MAX_LOGS)
            {
                _logs.RemoveAt(0);
            }
            
            // Also log to system console (visible in Console app)
            System.Diagnostics.Debug.WriteLine($"[NotificationLogger] {logEntry}");
            Console.WriteLine($"[NotificationLogger] {logEntry}");
            
            // Try to write to a persistent log file
            try
            {
                var logPath = Path.Combine(FileSystem.Current.AppDataDirectory, "notification_logs.txt");
                File.AppendAllText(logPath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationLogger] Failed to write to log file: {ex.Message}");
            }
        }
    }

    public static List<string> GetLogs()
    {
        lock (_lock)
        {
            return new List<string>(_logs);
        }
    }

    public static string GetLogsAsString()
    {
        lock (_lock)
        {
            return string.Join(Environment.NewLine, _logs);
        }
    }

    public static void ClearLogs()
    {
        lock (_lock)
        {
            _logs.Clear();
            
            try
            {
                var logPath = Path.Combine(FileSystem.Current.AppDataDirectory, "notification_logs.txt");
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationLogger] Failed to clear log file: {ex.Message}");
            }
        }
    }

    public static string GetPersistentLogs()
    {
        try
        {
            var logPath = Path.Combine(FileSystem.Current.AppDataDirectory, "notification_logs.txt");
            if (File.Exists(logPath))
            {
                return File.ReadAllText(logPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationLogger] Failed to read log file: {ex.Message}");
        }
        
        return "No persistent logs found.";
    }
}
