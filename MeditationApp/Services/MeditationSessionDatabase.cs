using SQLite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using MeditationApp.Models;

namespace MeditationApp.Services
{
    public class MeditationSessionDatabase
    {
        private readonly SQLiteAsyncConnection _database;
        
        // Simple cache for frequently accessed data
        private readonly Dictionary<string, (DateTime CacheTime, List<Models.MeditationSession> Data)> _monthlyCache = new();
        private readonly Dictionary<string, (DateTime CacheTime, List<Models.MeditationSession> Data)> _dailyCache = new();
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5); // Cache for 5 minutes

        public MeditationSessionDatabase(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<Models.MeditationSession>().Wait();
            _database.CreateTableAsync<Models.UserDailyInsights>().Wait();
        }

        public Task<List<Models.MeditationSession>> GetSessionsAsync()
        {
            return _database.Table<Models.MeditationSession>().ToListAsync();
        }

        public async Task<List<Models.MeditationSession>> GetSessionsForDateAsync(DateTime date)
        {
            var cacheKey = date.ToString("yyyy-MM-dd");
            
            // Check cache first
            if (_dailyCache.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.CacheTime < _cacheExpiry)
                {
                    System.Diagnostics.Debug.WriteLine($"Database: Using cached data for date {cacheKey} - {cached.Data.Count} sessions");
                    return new List<Models.MeditationSession>(cached.Data);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Database: Cache expired for date {cacheKey}, reloading from database");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Database: No cache found for date {cacheKey}, loading from database");
            }
            
            // Load from database
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);
            var sessions = await _database.Table<Models.MeditationSession>()
                .Where(s => s.Timestamp >= startDate && s.Timestamp < endDate)
                .Where(s => s.Status == MeditationSessionStatus.COMPLETED)
                .ToListAsync();
            
            System.Diagnostics.Debug.WriteLine($"Database: Loaded {sessions.Count} sessions for date {cacheKey} from database");
            
            // Update cache
            _dailyCache[cacheKey] = (DateTime.Now, new List<Models.MeditationSession>(sessions));
            
            return sessions;
        }

        public async Task<List<Models.MeditationSession>> GetSessionsForMonthAsync(int year, int month)
        {
            var cacheKey = $"{year}-{month:00}";
            
            // Check cache first
            if (_monthlyCache.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.Now - cached.CacheTime < _cacheExpiry)
                {
                    System.Diagnostics.Debug.WriteLine($"Database: Using cached data for month {cacheKey} - {cached.Data.Count} sessions");
                    return new List<Models.MeditationSession>(cached.Data);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Database: Cache expired for month {cacheKey}, reloading from database");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Database: No cache found for month {cacheKey}, loading from database");
            }
            
            // Load from database
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);
            var sessions = await _database.Table<Models.MeditationSession>()
                .Where(s => s.Timestamp >= startDate && s.Timestamp < endDate)
                .Where(s => s.Status == MeditationSessionStatus.COMPLETED)
                .ToListAsync();
            
            System.Diagnostics.Debug.WriteLine($"Database: Loaded {sessions.Count} sessions for month {cacheKey} from database");
            
            // Update cache
            _monthlyCache[cacheKey] = (DateTime.Now, new List<Models.MeditationSession>(sessions));
            
            return sessions;
        }

        public Task<Models.MeditationSession> GetSessionAsync(int id)
        {
            return _database.Table<Models.MeditationSession>().Where(i => i.SessionID == id).FirstOrDefaultAsync();
        }

        public async Task<int> SaveSessionAsync(Models.MeditationSession session)
        {
            int result;
            if (session.SessionID != 0)
                result = await _database.UpdateAsync(session);
            else
                result = await _database.InsertAsync(session);
            
            // Clear cache to ensure fresh data on next load
            ClearCache();
            
            return result;
        }

        public async Task<int> DeleteSessionAsync(Models.MeditationSession session)
        {
            var result = await _database.DeleteAsync(session);
            
            // Clear cache to ensure fresh data on next load
            ClearCache();
            
            return result;
        }

        public async Task<int> ClearAllSessionsAsync()
        {
            var result = await _database.DeleteAllAsync<Models.MeditationSession>();
            
            // Clear cache
            ClearCache();
            
            return result;
        }

        /// <summary>
        /// Clear all cached data
        /// </summary>
        public void ClearCache()
        {
            _dailyCache.Clear();
            _monthlyCache.Clear();
        }

        public async Task AddSampleDataAsync()
        {
            // Check if we already have data
            var existingSessions = await GetSessionsAsync();
            if (existingSessions.Any()) return;

            // Add some sample sessions
            var sampleSessions = new List<MeditationSession>
            {
                new MeditationSession
                {
                    Uuid = Guid.NewGuid().ToString(),
                    UserID = "sample_user",
                    Timestamp = DateTime.Today.AddHours(7).AddMinutes(30),
                    AudioPath = "morning_meditation.mp3",
                    Status = MeditationSessionStatus.COMPLETED
                },
                new MeditationSession
                {
                    Uuid = Guid.NewGuid().ToString(),
                    UserID = "sample_user",
                    Timestamp = DateTime.Today.AddHours(12).AddMinutes(15),
                    AudioPath = "mindfulness_break.mp3",
                    Status = MeditationSessionStatus.COMPLETED
                },
                new MeditationSession
                {
                    Uuid = Guid.NewGuid().ToString(),
                    UserID = "sample_user",
                    Timestamp = DateTime.Today.AddDays(-1).AddHours(20),
                    AudioPath = "evening_relaxation.mp3",
                    Status = MeditationSessionStatus.COMPLETED
                },
                new MeditationSession
                {
                    Uuid = Guid.NewGuid().ToString(),
                    UserID = "sample_user",
                    Timestamp = DateTime.Today.AddDays(-2).AddHours(8),
                    AudioPath = "breathing_exercise.mp3",
                    Status = MeditationSessionStatus.COMPLETED
                }
            };

            foreach (var session in sampleSessions)
            {
                await SaveSessionAsync(session);
            }
        }

        // User Daily Insights methods
        public async Task<Models.UserDailyInsights?> GetDailyInsightsAsync(string userId, DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);
            
            return await _database.Table<Models.UserDailyInsights>()
                .Where(i => i.UserID == userId && i.Date >= startOfDay && i.Date < endOfDay)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveDailyInsightsAsync(Models.UserDailyInsights insights)
        {
            insights.LastUpdated = DateTime.UtcNow;
            int result;
            
            if (insights.ID != 0)
                result = await _database.UpdateAsync(insights);
            else
                result = await _database.InsertAsync(insights);
            
            return result;
        }

        public async Task<int> DeleteDailyInsightsAsync(Models.UserDailyInsights insights)
        {
            return await _database.DeleteAsync(insights);
        }

        public async Task<List<Models.UserDailyInsights>> GetUnsyncedInsightsAsync()
        {
            return await _database.Table<Models.UserDailyInsights>()
                .Where(i => !i.IsSynced)
                .ToListAsync();
        }

        public async Task MarkInsightsAsSyncedAsync(Models.UserDailyInsights insights)
        {
            insights.IsSynced = true;
            await _database.UpdateAsync(insights);
        }
    }
}
