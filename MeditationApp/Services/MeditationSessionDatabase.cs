using SQLite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace MeditationApp.Services
{
    public class MeditationSessionDatabase
    {
        private readonly SQLiteAsyncConnection _database;

        public MeditationSessionDatabase(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<Models.MeditationSession>().Wait();
        }

        public Task<List<Models.MeditationSession>> GetSessionsAsync()
        {
            return _database.Table<Models.MeditationSession>().ToListAsync();
        }

        public Task<List<Models.MeditationSession>> GetSessionsForDateAsync(DateTime date)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);
            return _database.Table<Models.MeditationSession>()
                .Where(s => s.Timestamp >= startDate && s.Timestamp < endDate)
                .ToListAsync();
        }

        public Task<List<Models.MeditationSession>> GetSessionsForMonthAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);
            return _database.Table<Models.MeditationSession>()
                .Where(s => s.Timestamp >= startDate && s.Timestamp < endDate)
                .ToListAsync();
        }

        public Task<Models.MeditationSession> GetSessionAsync(int id)
        {
            return _database.Table<Models.MeditationSession>().Where(i => i.SessionID == id).FirstOrDefaultAsync();
        }

        public Task<int> SaveSessionAsync(Models.MeditationSession session)
        {
            if (session.SessionID != 0)
                return _database.UpdateAsync(session);
            else
                return _database.InsertAsync(session);
        }

        public Task<int> DeleteSessionAsync(Models.MeditationSession session)
        {
            return _database.DeleteAsync(session);
        }

        public Task<int> ClearAllSessionsAsync()
        {
            return _database.DeleteAllAsync<Models.MeditationSession>();
        }

        public async Task AddSampleDataAsync()
        {
            // Check if we already have data
            var existingSessions = await GetSessionsAsync();
            if (existingSessions.Any()) return;

            // Add some sample sessions
            var sampleSessions = new List<Models.MeditationSession>
            {
                new Models.MeditationSession
                {
                    Uuid = Guid.NewGuid().ToString(),
                    UserID = "sample_user",
                    Timestamp = DateTime.Today.AddHours(7).AddMinutes(30),
                    AudioPath = "morning_meditation.mp3",
                    Status = "completed"
                },
                new Models.MeditationSession
                {
                    Uuid = Guid.NewGuid().ToString(),
                    UserID = "sample_user",
                    Timestamp = DateTime.Today.AddHours(12).AddMinutes(15),
                    AudioPath = "mindfulness_break.mp3",
                    Status = "completed"
                },
                new Models.MeditationSession
                {
                    Uuid = Guid.NewGuid().ToString(),
                    UserID = "sample_user",
                    Timestamp = DateTime.Today.AddDays(-1).AddHours(20),
                    AudioPath = "evening_relaxation.mp3",
                    Status = "completed"
                },
                new Models.MeditationSession
                {
                    Uuid = Guid.NewGuid().ToString(),
                    UserID = "sample_user",
                    Timestamp = DateTime.Today.AddDays(-2).AddHours(8),
                    AudioPath = "breathing_exercise.mp3",
                    Status = "completed"
                }
            };

            foreach (var session in sampleSessions)
            {
                await SaveSessionAsync(session);
            }
        }
    }
}
