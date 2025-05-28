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
        public string Status { get; set; } = string.Empty;
        public string? LocalAudioPath { get; set; } = null; // Local downloaded audio file path
        public bool IsDownloaded { get; set; } = false; // Whether the audio file has been downloaded
        public DateTime? DownloadedAt { get; set; } = null; // When the file was downloaded
        public long? FileSizeBytes { get; set; } = null; // Size of the downloaded file
    }
}
