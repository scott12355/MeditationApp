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
        public string AudioPath { get; set; } = string.Empty; // Remote audio path
        
        // Store status as string in DB but expose as enum
        private string _status = MeditationSessionStatus.REQUESTED.ToString();
        [Ignore]
        public MeditationSessionStatus Status
        {
            get => Enum.TryParse<MeditationSessionStatus>(_status, out var result) ? result : MeditationSessionStatus.REQUESTED;
            set => _status = value.ToString();
        }
        
        // Property for database storage
        public string StatusString
        {
            get => _status;
            set => _status = value;
        }
        
        public string? LocalAudioPath { get; set; } = null; // Local downloaded audio file path
        public bool IsDownloaded { get; set; } = false; // Whether the audio file has been downloaded
        public DateTime? DownloadedAt { get; set; } = null; // When the file was downloaded
        public long? FileSizeBytes { get; set; } = null; // Size of the downloaded file
        public string? ErrorMessage { get; set; } = null; // Store error message if status is FAILED
    }
}
