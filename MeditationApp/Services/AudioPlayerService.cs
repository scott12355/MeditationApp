using Microsoft.Maui.Controls;
using Plugin.Maui.Audio;
using MediaManager;
using MediaManager.Media;
using MediaManager.Queue;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Timers;
using System.Threading.Tasks;
using System.Linq;

namespace MeditationApp.Services;

public class AudioPlayerService : INotifyPropertyChanged
{
    private readonly IAudioManager _audioManager;
    private IAudioPlayer? _currentPlayer;
    private readonly IMediaManager _mediaManager;

    private bool _isPlaying = false;
    private TimeSpan _playbackPosition = TimeSpan.Zero;
    private TimeSpan _playbackDuration = TimeSpan.Zero;
    private double _playbackProgress = 0.0;
    
    // Metadata properties
    private string _userName = string.Empty;
    private DateTime _sessionDate = DateTime.MinValue;
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private string _album = string.Empty;
    private string _albumArt = string.Empty;

    // Timer for position updates
    private System.Timers.Timer? _positionTimer;

    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event PropertyChangedEventHandler? PropertyChanged;

    // Playback state properties
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayPauseIcon));
                
                if (value)
                    StartPositionTimer();
                else
                    StopPositionTimer();
            }
        }
    }

    public TimeSpan PlaybackPosition
    {
        get => _playbackPosition;
        private set
        {
            if (_playbackPosition != value)
            {
                _playbackPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPositionText));
                
                // Update progress
                if (PlaybackDuration.TotalSeconds > 0)
                {
                    PlaybackProgress = value.TotalSeconds / PlaybackDuration.TotalSeconds;
                }
            }
        }
    }

    public TimeSpan PlaybackDuration
    {
        get => _playbackDuration;
        private set
        {
            if (_playbackDuration != value)
            {
                _playbackDuration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalDurationText));
            }
        }
    }

    public double PlaybackProgress
    {
        get => _playbackProgress;
        private set
        {
            if (Math.Abs(_playbackProgress - value) > 0.001)
            {
                _playbackProgress = value;
                OnPropertyChanged();
            }
        }
    }

    // Formatted time properties
    public string CurrentPositionText => PlaybackPosition.ToString(@"mm\:ss");
    public string TotalDurationText => PlaybackDuration.ToString(@"mm\:ss");
    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";
    
    // Metadata properties
    public string UserName
    {
        get => _userName;
        private set
        {
            if (_userName != value)
            {
                _userName = value;
                OnPropertyChanged();
            }
        }
    }
    
    public DateTime SessionDate
    {
        get => _sessionDate;
        private set
        {
            if (_sessionDate != value)
            {
                _sessionDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SessionDateText));
            }
        }
    }
    
    public string SessionDateText => SessionDate == DateTime.MinValue 
        ? string.Empty 
        : SessionDate.ToString("dddd, MMMM dd, yyyy");

    public string Title
    {
        get => _title;
        private set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public string Artist
    {
        get => _artist;
        private set
        {
            if (_artist != value)
            {
                _artist = value;
                OnPropertyChanged();
            }
        }
    }

    public string Album
    {
        get => _album;
        private set
        {
            if (_album != value)
            {
                _album = value;
                OnPropertyChanged();
            }
        }
    }

    public string AlbumArt
    {
        get => _albumArt;
        private set
        {
            if (_albumArt != value)
            {
                _albumArt = value;
                OnPropertyChanged();
            }
        }
    }

    public AudioPlayerService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
        _mediaManager = CrossMediaManager.Current;
        
        // Initialize position timer
        _positionTimer = new System.Timers.Timer(500); // Update every 500ms
        _positionTimer.Elapsed += OnPositionTimerElapsed;
        
        // Initialize MediaManager for enhanced metadata support
        InitializeMediaManager();
    }

    private void InitializeMediaManager()
    {
        try
        {
            // Subscribe to MediaManager events for metadata
            _mediaManager.StateChanged += OnMediaManagerStateChanged;
            _mediaManager.MediaItemChanged += OnMediaItemChanged;
            _mediaManager.PositionChanged += OnMediaManagerPositionChanged;
            
            // Enable lock screen controls
            _mediaManager.Notification.ShowNavigationControls = true;
            _mediaManager.Notification.ShowPlayPauseControls = true;
            _mediaManager.MediaItemChanged += OnMediaItemChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing MediaManager: {ex.Message}");
        }
    }

    private void OnMediaManagerStateChanged(object? sender, MediaManager.Playback.StateChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var newIsPlaying = e.State == MediaManager.Player.MediaPlayerState.Playing;
            if (IsPlaying != newIsPlaying)
            {
                IsPlaying = newIsPlaying;
            }
        });
    }

    private void OnMediaItemChanged(object? sender, MediaManager.Media.MediaItemEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.MediaItem != null)
            {
                Title = e.MediaItem.Title ?? string.Empty;
                Artist = e.MediaItem.Artist ?? string.Empty;
                Album = e.MediaItem.Album ?? string.Empty;
                AlbumArt = e.MediaItem.Image?.ToString() ?? string.Empty;
            }
        });
    }

    private void OnMediaManagerPositionChanged(object? sender, MediaManager.Playback.PositionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlaybackPosition = e.Position;
            PlaybackDuration = _mediaManager.Duration;
        });
    }

    private void OnPositionTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_currentPlayer != null && _currentPlayer.IsPlaying)
        {
            // Update position and duration from the current player
            PlaybackPosition = TimeSpan.FromSeconds(_currentPlayer.CurrentPosition);
            PlaybackDuration = TimeSpan.FromSeconds(_currentPlayer.Duration);
            
            // Check if playback has ended
            if (_currentPlayer.CurrentPosition >= _currentPlayer.Duration && _currentPlayer.Duration > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsPlaying = false;
                    PlaybackPosition = TimeSpan.Zero;
                    PlaybackProgress = 0;
                    MediaEnded?.Invoke(this, EventArgs.Empty);
                });
            }
        }
    }

    private void StartPositionTimer()
    {
        _positionTimer?.Start();
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Stop();
    }

    public async Task<bool> PlayFromUrlAsync(string url)
    {
        try
        {
            await StopAsync();
            
            // Use MediaManager for better lock screen support
            await _mediaManager.Play(url);
            
            // Set metadata after playing starts for lock screen display
            SetMediaMetadata(_title, _artist, _album, _albumArt);
            
            IsPlaying = true;
            MediaOpened?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing audio: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PlayFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"Audio file not found: {filePath}");
                return false;
            }

            await StopAsync();
            
            // Use MediaManager for better lock screen support
            await _mediaManager.Play(filePath);
            
            // Set metadata after playing starts for lock screen display
            SetMediaMetadata(_title, _artist, _album, _albumArt);
            
            IsPlaying = true;
            MediaOpened?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing audio file: {ex.Message}");
            return false;
        }
    }

    public async void Pause()
    {
        try
        {
            await _mediaManager.Pause();
            IsPlaying = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error pausing audio: {ex.Message}");
        }
    }

    public async void Resume()
    {
        try
        {
            await _mediaManager.Play();
            IsPlaying = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resuming audio: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        try
        {
            await _mediaManager.Stop();
            IsPlaying = false;
            PlaybackPosition = TimeSpan.Zero;
            PlaybackProgress = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping audio: {ex.Message}");
        }
        
        // Also stop the old audio player if it exists
        if (_currentPlayer != null)
        {
            _currentPlayer.Stop();
            _currentPlayer.Dispose();
            _currentPlayer = null;
        }
    }

    public async void Seek(double positionInSeconds)
    {
        try
        {
            await _mediaManager.SeekTo(TimeSpan.FromSeconds(positionInSeconds));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error seeking audio: {ex.Message}");
        }
    }

    public void SetAudioSourceWithMetadata(string filePath, string userName, DateTime sessionDate)
    {
        UserName = userName;
        SessionDate = sessionDate;
        
        // Set metadata to display session date and user name instead of session ID
        Title = sessionDate.ToString("dddd, MMMM dd, yyyy");
        Artist = userName;
        Album = "Lucen - Daily Meditations";
        AlbumArt = GetAppIconPath();
        
        System.Diagnostics.Debug.WriteLine($"Audio metadata set - User: {userName}, Date: {sessionDate:yyyy-MM-dd}");
    }

    private string GetAppIconPath()
    {
        try
        {
            // Try to get the app icon from different sources
            var iconPath = GetPlatformAppIconPath();
            System.Diagnostics.Debug.WriteLine($"App icon path: {iconPath}");
            
            // If the platform-specific path doesn't work, try to extract the logo.png
            if (string.IsNullOrEmpty(iconPath))
            {
                iconPath = GetEmbeddedLogoPng();
            }
            
            return iconPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting app icon path: {ex.Message}");
            return string.Empty;
        }
    }

    private string GetEmbeddedLogoPng()
    {
        try
        {
            // Try to access the embedded logo.png file
            var logoPath = Path.Combine(FileSystem.CacheDirectory, "app_logo.png");
            
            // Check if we've already extracted it
            if (File.Exists(logoPath))
            {
                return logoPath;
            }
            
            // Try to extract the logo from embedded resources
            var assembly = typeof(AudioPlayerService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));
                
            if (!string.IsNullOrEmpty(resourceName))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(logoPath);
                    stream.CopyTo(fileStream);
                    System.Diagnostics.Debug.WriteLine($"Extracted logo to: {logoPath}");
                    return logoPath;
                }
            }
            
            System.Diagnostics.Debug.WriteLine("Could not find embedded logo.png resource");
            return string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extracting embedded logo: {ex.Message}");
            return string.Empty;
        }
    }

    private string GetPlatformAppIconPath()
    {
        try
        {
            // Use a fallback approach - try to use the embedded logo.png resource
            // MediaManager should be able to handle file:// or embedded resource paths
            
#if ANDROID
            // For Android, try to use the actual app icon resource
            // This should reference the generated app icon in the drawable folder
            return "android.resource://com.lucen.ios/mipmap/appicon";
#elif IOS  
            // For iOS, try to reference the app icon from the bundle
            return "bundle://AppIcon60x60@2x.png";
#else
            // Fallback for other platforms
            return string.Empty;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting platform app icon path: {ex.Message}");
            return string.Empty;
        }
    }

    public void SetMediaMetadata(string title, string artist, string album, string albumArt = "")
    {
        // Use app icon as default album art if none provided
        if (string.IsNullOrEmpty(albumArt))
        {
            albumArt = GetAppIconPath();
        }
        
        _title = title;
        _artist = artist;
        _album = album;
        _albumArt = albumArt;
        
        // Update the properties which will trigger property changed events
        Title = title;
        Artist = artist;
        Album = album;
        AlbumArt = albumArt;
        
        System.Diagnostics.Debug.WriteLine($"Setting metadata - Title: {title}, Artist: {artist}, Album: {album}, AlbumArt: {albumArt}");
        
        // Apply metadata to MediaManager for lock screen display
        try
        {
            var currentItem = _mediaManager.Queue.Current;
            if (currentItem != null)
            {
                currentItem.Title = title;
                currentItem.Artist = artist;
                currentItem.Album = album;
                if (!string.IsNullOrEmpty(albumArt))
                {
                    currentItem.Image = albumArt;
                    System.Diagnostics.Debug.WriteLine($"Set current item image to: {albumArt}");
                }
                
                // Also try to set the notification icon specifically
                if (!string.IsNullOrEmpty(albumArt))
                {
                    try
                    {
                        // Try to set the notification cover art specifically
                        _mediaManager.Notification.UpdateNotification();
                        System.Diagnostics.Debug.WriteLine("Updated notification with cover art");
                    }
                    catch (Exception notificationEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error setting notification cover art: {notificationEx.Message}");
                    }
                }
                else
                {
                    _mediaManager.Notification.UpdateNotification();
                }
                
                System.Diagnostics.Debug.WriteLine("Updated notification metadata");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No current media item found when setting metadata");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating MediaManager metadata: {ex.Message}");
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        
        // Unsubscribe from MediaManager events
        _mediaManager.StateChanged -= OnMediaManagerStateChanged;
        _mediaManager.MediaItemChanged -= OnMediaItemChanged;
        _mediaManager.PositionChanged -= OnMediaManagerPositionChanged;
        
        _ = StopAsync();
    }
}