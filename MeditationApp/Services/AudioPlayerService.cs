using Microsoft.Maui.Controls;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MeditationApp.Services;

public class AudioPlayerService : INotifyPropertyChanged
{
    // private MediaElement? _mediaElement;
    private bool _isPlaying = false;
    private TimeSpan _playbackPosition = TimeSpan.Zero;
    private TimeSpan _playbackDuration = TimeSpan.Zero;
    private double _playbackProgress = 0.0;
    
    // Metadata properties
    private string _userName = string.Empty;
    private DateTime _sessionDate = DateTime.MinValue;

    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    // public event EventHandler<MediaFailedEventArgs>? MediaFailed;
    // public event EventHandler<MediaPositionChangedEventArgs>? PositionChanged;
    // public event EventHandler<MediaStateChangedEventArgs>? StateChanged;
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

    // MediaElement access properties
    // public MediaElementState? CurrentState => _mediaElement?.CurrentState;
    // public TimeSpan? Duration => _mediaElement?.Duration;
    // public TimeSpan? Position => _mediaElement?.Position;

    // public void SetMediaElement(MediaElement mediaElement)
    // {
    //     if (_mediaElement != null)
    //         UnwireEvents(_mediaElement);

    //     _mediaElement = mediaElement;
    //     WireEvents(_mediaElement);
    // }

    public void SetAudioSource(string filePath)
    {
        // if (_mediaElement != null && !string.IsNullOrEmpty(filePath))
        //     _mediaElement.Source = MediaSource.FromFile(filePath);
    }

    public void SetAudioSourceWithMetadata(string filePath, string userName, DateTime sessionDate)
    {
        // Set metadata first
        UserName = userName;
        SessionDate = sessionDate;
        
        // Then set the audio source
        SetAudioSource(filePath);
    }

    public void TogglePlayback()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    public void Play()
    {
        // _mediaElement?.Play();
    }

    public void Pause()
    {
        // _mediaElement?.Pause();
    }

    public void Stop()
    {
        // _mediaElement?.Stop();
    }

    public void SeekTo(TimeSpan position)
    {
        // if (_mediaElement != null && position >= TimeSpan.Zero && position <= _mediaElement.Duration)
        // {
        //     _mediaElement.SeekTo(position);
        // }
    }

    // private void WireEvents(MediaElement mediaElement)
    // {
    //     mediaElement.MediaOpened += OnMediaOpened;
    //     mediaElement.MediaEnded += OnMediaEnded;
    //     mediaElement.MediaFailed += OnMediaFailed;
    //     mediaElement.PositionChanged += OnPositionChanged;
    //     mediaElement.StateChanged += OnStateChanged;
    // }

    // private void UnwireEvents(MediaElement mediaElement)
    // {
    //     mediaElement.MediaOpened -= OnMediaOpened;
    //     mediaElement.MediaEnded -= OnMediaEnded;
    //     mediaElement.MediaFailed -= OnMediaFailed;
    //     mediaElement.PositionChanged -= OnPositionChanged;
    //     mediaElement.StateChanged -= OnStateChanged;
    // }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        // if (_mediaElement != null)
        // {
        //     PlaybackDuration = _mediaElement.Duration;
        // }
        MediaOpened?.Invoke(sender, e);
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        IsPlaying = false;
        PlaybackPosition = TimeSpan.Zero;
        PlaybackProgress = 0;
        MediaEnded?.Invoke(sender, e);
    }

    // private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    // {
    //     IsPlaying = false;
    //     MediaFailed?.Invoke(sender, e);
    // }

    // private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    // {
    //     PlaybackPosition = e.Position;
    //     if (_mediaElement != null)
    //     {
    //         PlaybackDuration = _mediaElement.Duration;
    //         PlaybackProgress = _mediaElement.Duration.TotalSeconds > 0 
    //             ? e.Position.TotalSeconds / _mediaElement.Duration.TotalSeconds 
    //             : 0.0;
    //     }
    //     PositionChanged?.Invoke(sender, e);
    // }

    // private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    // {
    //     IsPlaying = e.NewState == MediaElementState.Playing;
    //     StateChanged?.Invoke(sender, e);
    // }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}