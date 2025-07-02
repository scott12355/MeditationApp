using System;

namespace MeditationApp.Models
{
    public class BreathingSession
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int TechniqueId { get; set; }
        public string TechniqueName { get; set; } = string.Empty;
        public int CompletedCycles { get; set; }
        public int TotalCycles { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsCompleted { get; set; }
        public bool WasInterrupted { get; set; }
        
        // Session quality metrics
        public double AverageHeartRate { get; set; }
        public int StreakDay { get; set; }
        
        public TimeSpan ActualDuration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
        public bool IsInProgress => EndTime == null && StartTime != default;
        public double CompletionPercentage => TotalCycles > 0 ? (double)CompletedCycles / TotalCycles * 100 : 0;
    }

    public class BreathingStats
    {
        public int TotalSessions { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? LastSessionDate { get; set; }
        public int TotalCyclesCompleted { get; set; }
        public string FavoriteTechnique { get; set; } = string.Empty;
        public int SessionsThisWeek { get; set; }
        public int SessionsThisMonth { get; set; }
        
        public bool HasSessionToday => LastSessionDate?.Date == DateTime.Today;
        public string FormattedTotalDuration => 
            TotalDuration.TotalHours >= 1 
                ? $"{(int)TotalDuration.TotalHours}h {TotalDuration.Minutes}m"
                : $"{TotalDuration.Minutes}m {TotalDuration.Seconds}s";
    }
}
