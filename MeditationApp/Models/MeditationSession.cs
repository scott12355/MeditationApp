using SQLite;
using System;

namespace MeditationApp.Models
{
    public class MeditationSession
    {
        [PrimaryKey, AutoIncrement]
        public int SessionID { get; set; }
        public string Uuid { get; set; } = string.Empty; // Store the GraphQL UUID
        public string UserID { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string AudioPath { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
