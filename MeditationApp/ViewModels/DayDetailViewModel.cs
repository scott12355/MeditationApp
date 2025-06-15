using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Diagnostics;
using MeditationApp.Services;
using MeditationApp.Models;
using Microsoft.Maui.Controls;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeditationApp.ViewModels;

public partial class DayDetailViewModel : ObservableObject, IAudioPlayerViewModel
{
    private DateTime _selectedDate = DateTime.Today;
    private readonly MeditationSessionDatabase _database;
    private readonly CalendarDataService? _calendarDataService;
    private readonly CognitoAuthService? _cognitoAuthService;
    private readonly IAudioDownloadService _audioDownloadService;
    private readonly AudioPlayerService? _audioPlayerService;
    private bool _isLoading;
    private string _notes = string.Empty;
    private int? _mood;
    private MeditationSession? _currentlyPlayingSession;
    private DateTime? _playingSessionDate = null;

    public DayDetailViewModel(MeditationSessionDatabase database, CalendarDataService? calendarDataService = null, CognitoAuthService? cognitoAuthService = null, IAudioDownloadService? audioService = null, AudioPlayerService? audioPlayerService = null)
    {
        _database = database;
        _calendarDataService = calendarDataService;
        _cognitoAuthService = cognitoAuthService;
        _audioDownloadService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _audioPlayerService = audioPlayerService;
        
        AddSessionCommand = new Command(OnAddSession);
        PlaySessionCommand = new Command<MeditationSession>(OnPlaySession);
        TogglePlaybackCommand = new RelayCommand(OnTogglePlayback);
        StopPlaybackCommand = new RelayCommand(OnStopPlayback);
        SeekBackwardCommand = new RelayCommand(OnSeekBackward);
        SeekForwardCommand = new RelayCommand(OnSeekForward);
        HideAudioPlayerSheetCommand = new RelayCommand(OnHideAudioPlayerSheet);
        
        // Subscribe to audio player events
        if (_audioPlayerService != null)
        {
            _audioPlayerService.PropertyChanged += OnAudioPlayerServicePropertyChanged;
            _audioPlayerService.MediaOpened += OnMediaOpened;
            _audioPlayerService.MediaEnded += OnMediaEnded;
        }
    }

    /// <summary>
    /// Resets the ViewModel state for logout/login cycles to prevent cross-user data contamination
    /// </summary>
    public void Reset()
    {
        System.Diagnostics.Debug.WriteLine("DayDetailViewModel: Resetting state");
        
        // Clear sessions
        Sessions.Clear();
        
        // Reset notes and mood
        Notes = string.Empty;
        Mood = null;
        
        // Reset selected date to today
        SelectedDate = DateTime.Today;
        
        // Reset loading state
        IsLoading = false;
        
        // Clear currently playing session
        CurrentlyPlayingSession = null;
        
        System.Diagnostics.Debug.WriteLine("DayDetailViewModel: State reset completed");
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            _selectedDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedDate));
        }
    }

    public string FormattedDate => _selectedDate.ToString("dddd, MMMM d, yyyy");

    public ObservableCollection<MeditationSession> Sessions { get; } = new();

    public string Notes
    {
        get => _notes;
        set
        {
            _notes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNotes));
            OnPropertyChanged(nameof(HasNoNotes));
        }
    }

    public int? Mood
    {
        get => _mood;
        set
        {
            _mood = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMood));
            OnPropertyChanged(nameof(MoodEmoji));
            OnPropertyChanged(nameof(MoodDescription));
        }
    }

    public bool HasNotes => !string.IsNullOrEmpty(Notes);
    public bool HasNoNotes => !HasNotes;
    public bool HasMood => Mood.HasValue;
    public bool HasNoSessions => Sessions.Count == 0;

    public string MoodEmoji => Mood switch
    {
        1 => "😢",
        2 => "😕", 
        3 => "😐",
        4 => "😊",
        5 => "😄",
        _ => ""
    };

    public string MoodDescription => Mood switch
    {
        1 => "Very Sad",
        2 => "Sad", 
        3 => "Neutral",
        4 => "Happy",
        5 => "Very Happy",
        _ => ""
    };

    public int TotalMeditationTime => Sessions.Count * 15; // Default 15 min per session
    public int SessionCount { get; private set; }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddSessionCommand { get; }
    public ICommand PlaySessionCommand { get; }
    public ICommand TogglePlaybackCommand { get; }
    public ICommand StopPlaybackCommand { get; }
    public ICommand SeekBackwardCommand { get; }
    public ICommand SeekForwardCommand { get; }
    public ICommand HideAudioPlayerSheetCommand { get; }
    
    // Audio playback properties (delegate to AudioPlayerService)
    public bool IsPlaying => _audioPlayerService?.IsPlaying ?? false;
    public double PlaybackProgress => _audioPlayerService?.PlaybackProgress ?? 0.0;
    public TimeSpan PlaybackPosition => _audioPlayerService?.PlaybackPosition ?? TimeSpan.Zero;
    public TimeSpan PlaybackDuration => _audioPlayerService?.PlaybackDuration ?? TimeSpan.Zero;
    public string CurrentPositionText => _audioPlayerService?.CurrentPositionText ?? "0:00";
    public string TotalDurationText => _audioPlayerService?.TotalDurationText ?? "0:00";
    public string PlayPauseIcon => _audioPlayerService?.PlayPauseIcon ?? "▶";
    public string PlayPauseIconImage => IsPlaying ? "pause" : "play";

    // IAudioPlayerViewModel properties
    [ObservableProperty]
    private bool _isAudioPlayerSheetOpen;

    [ObservableProperty]
    private bool _isBottomSheetExpanded;

    public string SessionDateText => _playingSessionDate.HasValue ? _playingSessionDate.Value.ToString("dddd, MMMM d, yyyy") : FormattedDate;

    // Currently playing session tracking
    public MeditationSession? CurrentlyPlayingSession
    {
        get => _currentlyPlayingSession;
        set
        {
            var previousSession = _currentlyPlayingSession;
            _currentlyPlayingSession = value;
            OnPropertyChanged();
            
            // Update IsCurrentlyPlaying for all sessions
            foreach (var session in Sessions)
            {
                UpdateSessionPlayingState(session);
            }
            
            // If we stopped playing a session, update its state
            if (previousSession != null)
            {
                UpdateSessionPlayingState(previousSession);
            }
        }
    }

    private void OnAudioPlayerServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Forward audio-related property changes to update UI bindings
        switch (e.PropertyName)
        {
            case nameof(AudioPlayerService.IsPlaying):
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(PlayPauseIcon));
                OnPropertyChanged(nameof(PlayPauseIconImage));
                break;
            case nameof(AudioPlayerService.PlaybackPosition):
                OnPropertyChanged(nameof(PlaybackPosition));
                OnPropertyChanged(nameof(CurrentPositionText));
                break;
            case nameof(AudioPlayerService.PlaybackDuration):
                OnPropertyChanged(nameof(PlaybackDuration));
                OnPropertyChanged(nameof(TotalDurationText));
                break;
            case nameof(AudioPlayerService.PlaybackProgress):
                OnPropertyChanged(nameof(PlaybackProgress));
                break;
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PlaybackDuration));
        OnPropertyChanged(nameof(TotalDurationText));
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        // Clear currently playing session when media ends
        if (CurrentlyPlayingSession != null)
        {
            CurrentlyPlayingSession.IsCurrentlyPlaying = false;
            CurrentlyPlayingSession = null;
        }
        
        // Clear playing session metadata
        _playingSessionDate = null;
        OnPropertyChanged(nameof(SessionDateText));
        
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(PlayPauseIcon));
        OnPropertyChanged(nameof(PlayPauseIconImage));
    }

    private void OnTogglePlayback()
    {
        if (_audioPlayerService == null) return;
        
        if (_audioPlayerService.IsPlaying)
        {
            _audioPlayerService.Pause();
        }
        else
        {
            // If we have a currently playing session, resume it
            if (CurrentlyPlayingSession != null && !string.IsNullOrEmpty(CurrentlyPlayingSession.LocalAudioPath))
            {
                if (_audioPlayerService.PlaybackPosition > TimeSpan.Zero && _audioPlayerService.PlaybackPosition < _audioPlayerService.PlaybackDuration)
                {
                    _audioPlayerService.Resume();
                }
                else
                {
                    _ = _audioPlayerService.PlayFromFileAsync(CurrentlyPlayingSession.LocalAudioPath);
                }
            }
        }
    }

    private void OnStopPlayback()
    {
        if (_audioPlayerService == null) return;
        
        _ = _audioPlayerService.StopAsync();
        CurrentlyPlayingSession = null;
    }

    private void OnSeekBackward()
    {
        if (_audioPlayerService == null) return;
        
        var newPosition = _audioPlayerService.PlaybackPosition - TimeSpan.FromSeconds(15);
        if (newPosition < TimeSpan.Zero)
            newPosition = TimeSpan.Zero;
            
        _audioPlayerService.Seek(newPosition.TotalSeconds);
    }

    private void OnSeekForward()
    {
        if (_audioPlayerService == null) return;
        
        var newPosition = _audioPlayerService.PlaybackPosition + TimeSpan.FromSeconds(15);
        if (newPosition > _audioPlayerService.PlaybackDuration)
            newPosition = _audioPlayerService.PlaybackDuration;
            
        _audioPlayerService.Seek(newPosition.TotalSeconds);
    }

    private void OnHideAudioPlayerSheet()
    {
        IsAudioPlayerSheetOpen = false;
        
        // Clear playing session metadata when sheet is manually hidden
        _playingSessionDate = null;
        OnPropertyChanged(nameof(SessionDateText));
    }

    private void UpdateSessionPlayingState(MeditationSession session)
    {
        var isCurrentlyPlaying = CurrentlyPlayingSession?.Uuid == session.Uuid;
        session.IsCurrentlyPlaying = isCurrentlyPlaying;
    }

    private async Task<bool> DownloadSessionAsync(MeditationSession session)
    {
        if (session.IsDownloading)
        {
            return false; // Already downloading
        }

        try
        {
            session.IsDownloading = true;
            session.DownloadStatus = "Getting download URL...";

            // Get presigned URL for the session
            var presignedUrl = await _audioDownloadService.GetPresignedUrlAsync(session.Uuid);
            if (string.IsNullOrEmpty(presignedUrl))
            {
                session.DownloadStatus = "Failed to get download URL";
                return false;
            }

            session.DownloadStatus = "Downloading session...";

            // Download the audio file
            var success = await _audioDownloadService.DownloadSessionAudioAsync(session, presignedUrl);
            if (success)
            {
                // Update session in database
                await _database.SaveSessionAsync(session);
                session.DownloadStatus = "Download complete!";
                
                // Clear status after delay
                await Task.Delay(2000);
                session.DownloadStatus = string.Empty;
            }
            else
            {
                session.DownloadStatus = "Download failed";
                await Task.Delay(3000);
                session.DownloadStatus = string.Empty;
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading session: {ex.Message}");
            session.DownloadStatus = "Download failed";
            await Task.Delay(3000);
            session.DownloadStatus = string.Empty;
            return false;
        }
        finally
        {
            session.IsDownloading = false;
        }
    }

    private void UpdateSessionDownloadState(MeditationSession session)
    {
        // This method is no longer needed since we're updating properties directly on the session
        // But keeping it for compatibility with the existing code structure
        OnPropertyChanged(nameof(Sessions));
    }

    private async Task LoadAudioWithMetadata(MeditationSession session)
    {
        if (_audioPlayerService == null || string.IsNullOrEmpty(session.LocalAudioPath))
            return;

        try
        {
            // Get user profile information for metadata
            string userName = "User"; // Default fallback
            
            // Try to get user information from stored tokens
            try
            {
                var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
                if (!string.IsNullOrEmpty(accessToken) && _cognitoAuthService != null)
                {
                    var userAttributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
                    
                    string firstName = string.Empty;
                    string lastName = string.Empty;
                    string username = string.Empty;
                    string email = string.Empty;

                    foreach (var attribute in userAttributes)
                    {
                        switch (attribute.Name)
                        {
                            case "given_name":
                                firstName = attribute.Value;
                                break;
                            case "family_name":
                                lastName = attribute.Value;
                                break;
                            case "preferred_username":
                            case "username":
                                username = attribute.Value;
                                break;
                            case "email":
                                email = attribute.Value;
                                break;
                        }
                    }

                    // Create display name from available information
                    var displayName = $"{firstName} {lastName}".Trim();
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        userName = displayName;
                    }
                    else if (!string.IsNullOrEmpty(username))
                    {
                        userName = username;
                    }
                    else if (!string.IsNullOrEmpty(email))
                    {
                        userName = email;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not fetch user attributes: {ex.Message}");
                // Continue with default userName
            }

            // Update the ViewModel properties with session-specific metadata
            _playingSessionDate = session.Timestamp;
            OnPropertyChanged(nameof(SessionDateText));
            // Note: SessionDateText is already bound to FormattedDate, so we don't need to update it here

            // Set audio source with metadata
            _audioPlayerService.SetAudioSourceWithMetadata(
                session.LocalAudioPath, 
                userName, 
                session.Timestamp);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading audio with metadata: {ex.Message}");
            // Fallback to basic audio loading if metadata fails
            _playingSessionDate = session.Timestamp;
            OnPropertyChanged(nameof(SessionDateText));
            _audioPlayerService.SetAudioSourceWithMetadata(session.LocalAudioPath, "User", session.Timestamp);
        }
    }

    private MeditationSession? _selectedSession;
    public MeditationSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            _selectedSession = value;
            OnPropertyChanged();
            if (_selectedSession != null)
            {
                PlaySessionInternal(_selectedSession);
            }
        }
    }

    private async void PlaySessionInternal(MeditationSession session)
    {
        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
        try
        {
            // Check if this session is already playing
            if (CurrentlyPlayingSession?.Uuid == session.Uuid && IsPlaying)
            {
                // Pause the current session
                _audioPlayerService?.Pause();
                return;
            }

            // Check if session is downloaded and file exists
            var localPath = _audioDownloadService.GetLocalAudioPath(session);
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                // Not downloaded or file missing, attempt download
                var success = await DownloadSessionAsync(session);
                if (!success)
                {
                    if (page != null)
                        await page.DisplayAlert("Error", "Failed to download the session audio.", "OK");
                    return;
                }
                localPath = session.LocalAudioPath;
            }
            
            // At this point, localPath should be valid and file should exist
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath) && _audioPlayerService != null)
            {
                // Set this session as currently playing
                CurrentlyPlayingSession = session;
                
                // Load audio with metadata
                await LoadAudioWithMetadata(session);
                
                // Play the audio
                var success = await _audioPlayerService.PlayFromFileAsync(localPath);
                if (!success)
                {
                    CurrentlyPlayingSession = null;
                    if (page != null)
                        await page.DisplayAlert("Error", "Failed to play the audio file.", "OK");
                }
                else
                {
                    // Show the audio player bottom sheet when playback starts successfully
                    IsAudioPlayerSheetOpen = true;
                }
            }
            else
            {
                if (page != null)
                    await page.DisplayAlert("Error", "Audio file is missing or invalid.", "OK");
            }
        }
        catch (Exception ex)
        {
            CurrentlyPlayingSession = null;
            if (page != null)
                await page.DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async void OnPlaySession(MeditationSession session)
    {
        // If session is not downloaded, download it first
        if (!session.IsDownloaded)
        {
            var success = await DownloadSessionAsync(session);
            if (!success)
            {
                return; // Download failed, don't try to play
            }
        }
        
        // Now try to play the session
        PlaySessionInternal(session);
    }

    public async void LoadDayData(DateTime date)
    {
        SelectedDate = date;
        await LoadDayDataAsync();
    }

    private async Task LoadDayDataAsync()
    {
        IsLoading = true;
        try
        {
            Sessions.Clear();
            System.Diagnostics.Debug.WriteLine($"DayDetailViewModel: Loading sessions for date {_selectedDate:yyyy-MM-dd}");
            var allSessions = _calendarDataService != null
                ? await _calendarDataService.GetSessionsForDateAsync(_selectedDate)
                : await _database.GetAllSessionsForDateAsync(_selectedDate);
            System.Diagnostics.Debug.WriteLine($"DayDetailViewModel: Found {allSessions?.Count ?? -1} sessions for date {_selectedDate:yyyy-MM-dd}");
            LoadSessions(allSessions ?? new List<MeditationSession>());

            // Get daily insights if available
            try
            {
                var userId = await GetCurrentUserId();
                System.Diagnostics.Debug.WriteLine($"DayDetailViewModel: userId={userId}");
                if (!string.IsNullOrEmpty(userId))
                {
                    var dailyInsights = await _database.GetDailyInsightsAsync(userId, _selectedDate);
                    System.Diagnostics.Debug.WriteLine($"DayDetailViewModel: dailyInsights is null? {dailyInsights == null}");
                    if (dailyInsights != null)
                    {
                        Notes = dailyInsights.Notes ?? string.Empty;
                        Mood = dailyInsights.Mood;
                    }
                    else
                    {
                        Notes = string.Empty;
                        Mood = null;
                    }
                }
                else
                {
                    Notes = string.Empty;
                    Mood = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading daily insights: {ex.Message}");
                Notes = string.Empty;
                Mood = null;
            }
            
            // Calculate totals
            SessionCount = Sessions.Count;

            OnPropertyChanged(nameof(TotalMeditationTime));
            OnPropertyChanged(nameof(SessionCount));
            OnPropertyChanged(nameof(HasNoSessions));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading day data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void OnAddSession()
    {
        // TODO: Navigate to add session page or show modal
        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
        if (page != null)
            await page.DisplayAlert("Add Session", 
                $"Add meditation session for {_selectedDate:MMMM d, yyyy}", "OK");
    }

    private async Task<string> GetCurrentUserId()
    {
        try
        {
            var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken) && _cognitoAuthService != null)
            {
                var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
                return attributes.FirstOrDefault(a => a.Name == "sub")?.Value ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting user ID: {ex.Message}");
        }
        return string.Empty;
    }

    public void LoadFromDayData(DayData dayData)
    {
        SelectedDate = dayData.Date;
        Notes = dayData.Notes;
        Mood = dayData.Mood;
        Sessions.Clear();
        foreach (var session in dayData.Sessions)
        {
            Sessions.Add(session);
        }
        OnPropertyChanged(nameof(Sessions));
        OnPropertyChanged(nameof(HasNoSessions));
        OnPropertyChanged(nameof(TotalMeditationTime));
        OnPropertyChanged(nameof(SessionCount));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasNoNotes));
        OnPropertyChanged(nameof(HasMood));
        OnPropertyChanged(nameof(MoodEmoji));
        OnPropertyChanged(nameof(MoodDescription));
    }

    private void LoadSessions(IEnumerable<MeditationSession> sessionList)
    {
        Sessions.Clear();
        foreach (var session in sessionList)
        {
            Sessions.Add(session);
        }
        OnPropertyChanged(nameof(Sessions));
        OnPropertyChanged(nameof(HasNoSessions));
        OnPropertyChanged(nameof(TotalMeditationTime));
    }

    partial void OnIsAudioPlayerSheetOpenChanged(bool value)
    {
        if (value)
        {
            // When opening the sheet, set it as expanded
            IsBottomSheetExpanded = true;
        }
        else
        {
            // When closing the sheet, collapse it first
            IsBottomSheetExpanded = false;
        }
    }
}
