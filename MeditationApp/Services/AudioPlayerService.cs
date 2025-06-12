using Microsoft.Maui.Controls;
using Plugin.Maui.Audio;
using MediaManager;
using MediaManager.Media;
using MediaManager.Queue;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using TagFile = TagLib.File;

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
    
    // Flag to prevent MediaManager from overriding our extracted metadata
    private bool _hasCustomMetadata = false;

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
                System.Diagnostics.Debug.WriteLine($"Title changing from '{_title}' to '{value}'");
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
            
            System.Diagnostics.Debug.WriteLine("MediaManager initialized - using custom metadata extraction");
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
        System.Diagnostics.Debug.WriteLine($"OnMediaItemChanged triggered - _hasCustomMetadata: {_hasCustomMetadata}");
        
        // If we have custom metadata, completely ignore MediaManager's metadata
        if (_hasCustomMetadata)
        {
            System.Diagnostics.Debug.WriteLine("Ignoring MediaManager metadata - preserving extracted MP3 metadata");
            return;
        }
        
        // Only use MediaManager metadata if we don't have custom metadata
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.MediaItem != null)
            {
                var mediaTitle = e.MediaItem.Title ?? string.Empty;
                var mediaArtist = e.MediaItem.Artist ?? string.Empty;
                var mediaAlbum = e.MediaItem.Album ?? string.Empty;
                var mediaImage = e.MediaItem.Image?.ToString() ?? string.Empty;
                
                System.Diagnostics.Debug.WriteLine($"Using MediaManager metadata - Title: {mediaTitle}, Artist: {mediaArtist}, Album: {mediaAlbum}");
                
                Title = mediaTitle;
                Artist = mediaArtist;
                Album = mediaAlbum;
                AlbumArt = mediaImage;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnMediaItemChanged: MediaItem is null");
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
            
            // Reset custom metadata flag for URL playback (allow MediaManager to handle it)
            _hasCustomMetadata = false;
            
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

    private async Task ExtractAndSetMetadata(string filePath)
    {
        try
        {
            // Extract metadata from MP3 file
            var (title, artist, album, albumArtData) = ExtractMp3Metadata(filePath);
            
            // Use extracted metadata or fall back to current values
            var metadataTitle = !string.IsNullOrEmpty(title) ? title : _title;
            var metadataArtist = !string.IsNullOrEmpty(artist) ? artist : _artist;
            var metadataAlbum = !string.IsNullOrEmpty(album) ? album : _album;
            
            // Handle album art
            string albumArtPath = _albumArt; // Default to current album art
            if (albumArtData.Length > 0)
            {
                try
                {
                    // Save album art to cache directory
                    var albumArtFileName = $"{Path.GetFileNameWithoutExtension(filePath)}_albumart.jpg";
                    albumArtPath = Path.Combine(FileSystem.CacheDirectory, albumArtFileName);
                    await File.WriteAllBytesAsync(albumArtPath, albumArtData);
                    System.Diagnostics.Debug.WriteLine($"Saved album art to: {albumArtPath}");
                }
                catch (Exception artEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving album art: {artEx.Message}");
                    albumArtPath = _albumArt; // Fall back to current album art
                }
            }
            
            // Update metadata properties directly - this is our source of truth
            Title = metadataTitle;
            Artist = metadataArtist;
            Album = metadataAlbum;
            AlbumArt = albumArtPath;
            
            // Prevent MediaManager from overriding our metadata
            _hasCustomMetadata = true;
            
            System.Diagnostics.Debug.WriteLine($"Set extracted metadata - Title: {Title}, Artist: {Artist}, Album: {Album}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extracting and setting metadata: {ex.Message}");
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
            
            // Extract and set metadata BEFORE playing
            await ExtractAndSetMetadata(filePath);
            
            // Note: MediaManager may show warnings about unrecognized metadata keys - this is normal
            // Our TagLibSharp extraction handles metadata correctly regardless of these warnings
            System.Diagnostics.Debug.WriteLine($"Playing file with extracted metadata - Title: '{Title}', Artist: '{Artist}'");
            
            // Ensure custom metadata flag is set before playing
            _hasCustomMetadata = true;
            
            // Now play the file with MediaManager
            await _mediaManager.Play(filePath);
            
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
            
            // Clear custom metadata flag when stopping to allow fresh metadata on next play
            _hasCustomMetadata = false;
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
        
        // Extract and use MP3 metadata directly
        try
        {
            var (extractedTitle, extractedArtist, extractedAlbum, albumArtData) = ExtractMp3Metadata(filePath);
            
            // Use extracted metadata if available, otherwise use session info
            Title = !string.IsNullOrEmpty(extractedTitle) ? extractedTitle : sessionDate.ToString("dddd, MMMM dd, yyyy");
            Artist = !string.IsNullOrEmpty(extractedArtist) ? extractedArtist : userName;
            Album = !string.IsNullOrEmpty(extractedAlbum) ? extractedAlbum : "Lucen - Daily Meditations";
            
            // Handle album art
            if (albumArtData.Length > 0)
            {
                try
                {
                    // Save album art to cache directory
                    var albumArtFileName = $"{Path.GetFileNameWithoutExtension(filePath)}_albumart.jpg";
                    var albumArtPath = Path.Combine(FileSystem.CacheDirectory, albumArtFileName);
                    File.WriteAllBytes(albumArtPath, albumArtData);
                    AlbumArt = albumArtPath;
                    System.Diagnostics.Debug.WriteLine($"Using extracted album art: {albumArtPath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving album art: {ex.Message}");
                    AlbumArt = GetAppIconPath(); // Fall back to app icon
                }
            }
            else
            {
                AlbumArt = GetAppIconPath(); // Default to app icon
            }
            
            // Mark that we have custom metadata
            _hasCustomMetadata = true;
            
            System.Diagnostics.Debug.WriteLine($"Audio metadata set - Title: {Title}, Artist: {Artist}, Album: {Album}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SetAudioSourceWithMetadata: {ex.Message}");
            // Fallback to session info
            Title = sessionDate.ToString("dddd, MMMM dd, yyyy");
            Artist = userName;
            Album = "Lucen - Daily Meditations";
            AlbumArt = GetAppIconPath();
            _hasCustomMetadata = true;
        }
    }

    // Simplified metadata loading - just extract metadata without complex logic
    public void LoadMetadataFromFile(string filePath)
    {
        try
        {
            var (title, artist, album, albumArtData) = ExtractMp3Metadata(filePath);
            
            if (!string.IsNullOrEmpty(title)) Title = title;
            if (!string.IsNullOrEmpty(artist)) Artist = artist;
            if (!string.IsNullOrEmpty(album)) Album = album;
            
            // Handle album art
            if (albumArtData.Length > 0)
            {
                try
                {
                    var albumArtFileName = $"{Path.GetFileNameWithoutExtension(filePath)}_albumart.jpg";
                    var albumArtPath = Path.Combine(FileSystem.CacheDirectory, albumArtFileName);
                    File.WriteAllBytes(albumArtPath, albumArtData);
                    AlbumArt = albumArtPath;
                    System.Diagnostics.Debug.WriteLine($"Loaded album art from MP3: {albumArtPath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving extracted album art: {ex.Message}");
                }
            }
            
            // Mark that we have custom metadata
            _hasCustomMetadata = true;
            
            System.Diagnostics.Debug.WriteLine($"Loaded metadata from MP3 - Title: {Title}, Artist: {Artist}, Album: {Album}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading metadata from file: {ex.Message}");
        }
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

    public (string title, string artist, string album, byte[] albumArt) ExtractMp3Metadata(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"File not found for metadata extraction: {filePath}");
                return (string.Empty, string.Empty, string.Empty, Array.Empty<byte>());
            }

            using var tagFile = TagFile.Create(filePath);
            
            var title = tagFile.Tag.Title ?? string.Empty;
            var artist = tagFile.Tag.FirstPerformer ?? string.Empty;
            var album = tagFile.Tag.Album ?? string.Empty;
            var albumArt = Array.Empty<byte>();

            System.Diagnostics.Debug.WriteLine($"Raw extracted title before processing: '{title}'");

            // Clean up title by removing UUID patterns and common prefixes
            if (!string.IsNullOrEmpty(title))
            {
                var originalTitle = title;
                
                // Remove .mp3 extension if present
                if (title.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    title = title.Substring(0, title.Length - 4);
                    System.Diagnostics.Debug.WriteLine($"Stripped .mp3 extension - '{originalTitle}' → '{title}'");
                }
                
                // Remove UUID patterns (e.g., "12345678-1234-1234-1234-123456789abc_" or just "12345678-1234-1234-1234-123456789abc")
                var uuidPattern = @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}[_-]?";
                var cleanedTitle = System.Text.RegularExpressions.Regex.Replace(title, uuidPattern, string.Empty);
                
                if (cleanedTitle != title)
                {
                    System.Diagnostics.Debug.WriteLine($"Stripped UUID prefix - '{title}' → '{cleanedTitle}'");
                    title = cleanedTitle;
                }
                
                // Remove any remaining leading underscores or dashes
                title = title.TrimStart('_', '-', ' ');
                
                if (title != originalTitle)
                {
                    System.Diagnostics.Debug.WriteLine($"Final title cleanup - Original: '{originalTitle}' → Final: '{title}'");
                }
            }

            // Extract album art if available
            if (tagFile.Tag.Pictures?.Length > 0)
            {
                albumArt = tagFile.Tag.Pictures[0].Data.Data;
                System.Diagnostics.Debug.WriteLine($"Extracted album art: {albumArt.Length} bytes");
            }

            System.Diagnostics.Debug.WriteLine($"Final extracted MP3 metadata - Title: '{title}', Artist: '{artist}', Album: '{album}'");
            
            return (title, artist, album, albumArt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extracting MP3 metadata from {filePath}: {ex.Message}");
            return (string.Empty, string.Empty, string.Empty, Array.Empty<byte>());
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