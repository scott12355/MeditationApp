using SQLite;
using MeditationApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MeditationApp.Services
{
    public class BreathingDatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public BreathingDatabaseService(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<BreathingSession>().Wait();
            _database.CreateTableAsync<BreathingTechnique>().Wait();
        }

        // Breathing Sessions
        public Task<List<BreathingSession>> GetSessionsAsync()
        {
            return _database.Table<BreathingSession>().ToListAsync();
        }

        public Task<List<BreathingSession>> GetRecentSessionsAsync(int count = 10)
        {
            return _database.Table<BreathingSession>()
                            .OrderByDescending(s => s.StartTime)
                            .Take(count)
                            .ToListAsync();
        }

        public Task<List<BreathingSession>> GetSessionsForDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return _database.Table<BreathingSession>()
                            .Where(s => s.StartTime >= startDate && s.StartTime <= endDate)
                            .OrderByDescending(s => s.StartTime)
                            .ToListAsync();
        }

        public Task<BreathingSession> GetSessionAsync(int id)
        {
            return _database.Table<BreathingSession>()
                            .Where(s => s.Id == id)
                            .FirstOrDefaultAsync();
        }

        public Task<int> SaveSessionAsync(BreathingSession session)
        {
            if (session.Id != 0)
            {
                return _database.UpdateAsync(session);
            }
            else
            {
                return _database.InsertAsync(session);
            }
        }

        public Task<int> DeleteSessionAsync(BreathingSession session)
        {
            return _database.DeleteAsync(session);
        }

        // Custom Techniques
        public Task<List<BreathingTechnique>> GetCustomTechniquesAsync()
        {
            return _database.Table<BreathingTechnique>()
                            .Where(t => t.IsCustom)
                            .ToListAsync();
        }

        public Task<int> SaveTechniqueAsync(BreathingTechnique technique)
        {
            if (technique.Id != 0)
            {
                return _database.UpdateAsync(technique);
            }
            else
            {
                return _database.InsertAsync(technique);
            }
        }

        public Task<int> DeleteTechniqueAsync(BreathingTechnique technique)
        {
            return _database.DeleteAsync(technique);
        }

        // Statistics
        public async Task<BreathingStats> GetStatsAsync()
        {
            var sessions = await GetSessionsAsync();
            var completedSessions = sessions.Where(s => s.IsCompleted).ToList();
            
            var stats = new BreathingStats
            {
                TotalSessions = completedSessions.Count,
                TotalDuration = TimeSpan.FromTicks(completedSessions.Sum(s => s.Duration.Ticks)),
                TotalCyclesCompleted = completedSessions.Sum(s => s.CompletedCycles),
                LastSessionDate = completedSessions.LastOrDefault()?.StartTime.Date
            };

            // Calculate streaks
            stats.CurrentStreak = CalculateCurrentStreak(completedSessions);
            stats.LongestStreak = CalculateLongestStreak(completedSessions);

            // This week and month stats
            var thisWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var thisMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            
            stats.SessionsThisWeek = completedSessions.Count(s => s.StartTime >= thisWeekStart);
            stats.SessionsThisMonth = completedSessions.Count(s => s.StartTime >= thisMonthStart);

            // Favorite technique
            if (completedSessions.Any())
            {
                stats.FavoriteTechnique = completedSessions
                    .GroupBy(s => s.TechniqueName)
                    .OrderByDescending(g => g.Count())
                    .First().Key;
            }

            // Average mood improvement
            var sessionsWithMood = completedSessions.Where(s => s.MoodBefore.HasValue && s.MoodAfter.HasValue);
            if (sessionsWithMood.Any())
            {
                stats.AverageMoodImprovement = sessionsWithMood.Average(s => s.MoodAfter.Value - s.MoodBefore.Value);
            }

            return stats;
        }

        private int CalculateCurrentStreak(List<BreathingSession> sessions)
        {
            if (!sessions.Any()) return 0;

            var sessionsByDate = sessions
                .GroupBy(s => s.StartTime.Date)
                .OrderByDescending(g => g.Key)
                .ToList();

            if (!sessionsByDate.Any()) return 0;

            // Check if there's a session today or yesterday to start the streak
            var today = DateTime.Today;
            var latestSessionDate = sessionsByDate.First().Key;
            
            if (latestSessionDate < today.AddDays(-1))
                return 0; // No recent sessions, streak is broken

            int streak = 0;
            var currentDate = latestSessionDate;
            
            foreach (var group in sessionsByDate)
            {
                if (group.Key == currentDate)
                {
                    streak++;
                    currentDate = currentDate.AddDays(-1);
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        private int CalculateLongestStreak(List<BreathingSession> sessions)
        {
            if (!sessions.Any()) return 0;

            var sessionsByDate = sessions
                .GroupBy(s => s.StartTime.Date)
                .OrderBy(g => g.Key)
                .Select(g => g.Key)
                .ToList();

            int longestStreak = 0;
            int currentStreak = 1;
            
            for (int i = 1; i < sessionsByDate.Count; i++)
            {
                if (sessionsByDate[i] == sessionsByDate[i - 1].AddDays(1))
                {
                    currentStreak++;
                }
                else
                {
                    longestStreak = Math.Max(longestStreak, currentStreak);
                    currentStreak = 1;
                }
            }

            return Math.Max(longestStreak, currentStreak);
        }
    }
}
