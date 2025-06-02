using SQLite;
using System;
using System.ComponentModel;

namespace MeditationApp.Models
{
    public class MeditationSession : INotifyPropertyChanged
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
        
        // Transient properties for UI state (not stored in database)
        private bool _isDownloading = false;
        private string _downloadStatus = string.Empty;
        private bool _isCurrentlyPlaying = false;
        
        [Ignore]
        public bool IsDownloading 
        { 
            get => _isDownloading;
            set 
            {
                if (_isDownloading != value)
                {
                    _isDownloading = value;
                    OnPropertyChanged(nameof(IsDownloading));
                }
            }
        }
        
        [Ignore]
        public string DownloadStatus 
        { 
            get => _downloadStatus;
            set 
            {
                if (_downloadStatus != value)
                {
                    _downloadStatus = value;
                    OnPropertyChanged(nameof(DownloadStatus));
                }
            }
        }
        
        [Ignore]
        public bool IsCurrentlyPlaying 
        { 
            get => _isCurrentlyPlaying;
            set 
            {
                if (_isCurrentlyPlaying != value)
                {
                    _isCurrentlyPlaying = value;
                    OnPropertyChanged(nameof(IsCurrentlyPlaying));
                }
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
