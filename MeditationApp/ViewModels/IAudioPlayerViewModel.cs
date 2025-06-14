using System.ComponentModel;
using System.Windows.Input;

namespace MeditationApp.ViewModels;

/// <summary>
/// Interface for ViewModels that support audio playback functionality
/// </summary>
public interface IAudioPlayerViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Whether the audio player bottom sheet is currently open
    /// </summary>
    bool IsAudioPlayerSheetOpen { get; set; }

    /// <summary>
    /// Whether the bottom sheet is expanded (showing full content)
    /// </summary>
    bool IsBottomSheetExpanded { get; set; }

    /// <summary>
    /// Whether audio is currently playing
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Current playback progress (0.0 to 1.0)
    /// </summary>
    double PlaybackProgress { get; }

    /// <summary>
    /// Current position text (e.g., "2:30")
    /// </summary>
    string CurrentPositionText { get; }

    /// <summary>
    /// Total duration text (e.g., "15:00")
    /// </summary>
    string TotalDurationText { get; }

    /// <summary>
    /// Session date text for display
    /// </summary>
    string SessionDateText { get; }

    /// <summary>
    /// Play/pause icon image source
    /// </summary>
    string PlayPauseIconImage { get; }

    /// <summary>
    /// Command to toggle playback (play/pause)
    /// </summary>
    ICommand TogglePlaybackCommand { get; }

    /// <summary>
    /// Command to seek backward by 15 seconds
    /// </summary>
    ICommand SeekBackwardCommand { get; }

    /// <summary>
    /// Command to seek forward by 15 seconds
    /// </summary>
    ICommand SeekForwardCommand { get; }

    /// <summary>
    /// Command to hide the audio player sheet
    /// </summary>
    ICommand HideAudioPlayerSheetCommand { get; }
} 