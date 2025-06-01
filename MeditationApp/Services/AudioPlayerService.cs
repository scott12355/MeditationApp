using Microsoft.Maui.Controls;
using Plugin.Maui.Audio;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Timers;

namespace MeditationApp.Services;

public class AudioPlayerService : INotifyPropertyChanged
{
    private readonly IAudioManager _audioManager;
    private IAudioPlayer? _currentPlayer;

    private bool _isPlaying = false;
    private TimeSpan _playbackPosition = TimeSpan.Zero;
    private TimeSpan _playbackDuration = TimeSpan.Zero;
    private double _playbackProgress = 0.0;
    
    // Metadata properties
    private string _userName = string.Empty;
    private DateTime _sessionDate = DateTime.MinValue;

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

    public AudioPlayerService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
        
        // Initialize position timer
        _positionTimer = new System.Timers.Timer(500); // Update every 500ms
        _positionTimer.Elapsed += OnPositionTimerElapsed;
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
            _currentPlayer = _audioManager.CreatePlayer(await FileSystem.OpenAppPackageFileAsync(url));
            
            if (_currentPlayer != null)
            {
                _currentPlayer.Play();
                IsPlaying = true;
                MediaOpened?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
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
            
            var fileStream = File.OpenRead(filePath);
            _currentPlayer = _audioManager.CreatePlayer(fileStream);
            
            if (_currentPlayer != null)
            {
                _currentPlayer.Play();
                IsPlaying = true;
                MediaOpened?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing audio file: {ex.Message}");
            return false;
        }
    }

    public void Pause()
    {
        if (_currentPlayer != null && _currentPlayer.IsPlaying)
        {
            _currentPlayer.Pause();
            IsPlaying = false;
        }
    }

    public void Resume()
    {
        if (_currentPlayer != null && !_currentPlayer.IsPlaying)
        {
            _currentPlayer.Play();
            IsPlaying = true;
        }
    }

    public Task StopAsync()
    {
        if (_currentPlayer != null)
        {
            _currentPlayer.Stop();
            _currentPlayer.Dispose();
            _currentPlayer = null;
            IsPlaying = false;
            PlaybackPosition = TimeSpan.Zero;
            PlaybackProgress = 0;
        }
        return Task.CompletedTask;
    }

    public void Seek(double positionInSeconds)
    {
        if (_currentPlayer != null)
        {
            _currentPlayer.Seek(positionInSeconds);
        }
    }

    public void SetAudioSourceWithMetadata(string filePath, string userName, DateTime sessionDate)
    {
        UserName = userName;
        SessionDate = sessionDate;
        // Note: We don't automatically play here, just set metadata
        System.Diagnostics.Debug.WriteLine($"Audio metadata set - User: {userName}, Date: {sessionDate:yyyy-MM-dd}");
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        _ = StopAsync();
    }
}