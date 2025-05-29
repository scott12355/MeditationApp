using SQLite;
using System;

namespace MeditationApp.Models
{
    public class UserDailyInsights
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }
        
        public string UserID { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int? Mood { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsSynced { get; set; } = false;
    }
} 