using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeditationApp.Services;
using MeditationApp.Models;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;

namespace MeditationApp.ViewModels;

public partial class TodayViewModel : ObservableObject
{
    [ObservableProperty]
    private string _welcomeMessage = "Today's Meditation";

    [ObservableProperty]
    private DateTime _currentDate = DateTime.Now;

    [ObservableProperty]
    private int _todaySessionsCompleted = 0;

    [ObservableProperty]
    private int? _selectedMood = null; // Default to neutral mood

    [ObservableProperty]
    private ObservableCollection<MeditationApp.Models.MeditationSession> _todaySessions = new();

    [ObservableProperty]
    private MeditationApp.Models.MeditationSession? _todaySession = null;

    [ObservableProperty]
    private bool _hasTodaySession = false;

    [ObservableProperty]
    private string _sessionNotes = string.Empty;

    [ObservableProperty]
    private bool _isDownloading = false;

    [ObservableProperty]
    private bool _isPlaying = false;

    [ObservableProperty]
    private bool _isPaused = false;

    [ObservableProperty]
    private double _playbackProgress = 0.0;

    [ObservableProperty]
    private TimeSpan _playbackPosition = TimeSpan.Zero;

    [ObservableProperty]
    private TimeSpan _playbackDuration = TimeSpan.Zero;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    [ObservableProperty]
    private string _playPauseIcon = "▶";

    [ObservableProperty]
    private MediaElement? _audioPlayer;

    [ObservableProperty]
    private bool _isLoading = true;

    public string FormattedDate => CurrentDate.ToString("dddd, MMMM dd, yyyy");
    
    public string CurrentPositionText => PlaybackPosition.ToString(@"mm\:ss");
    
    public string TotalDurationText => PlaybackDuration.ToString(@"mm\:ss");

    private readonly GraphQLService _graphQLService;
    private readonly CognitoAuthService _cognitoAuthService;
    private readonly MeditationSessionDatabase _sessionDatabase;
    private readonly IAudioService _audioService;
    private Task? _initializationTask;
    
    // Events for MediaElement control
    public event EventHandler? PlayRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? StopRequested;
    
    // Token refresh management
    private static DateTime _lastRefreshAttempt = DateTime.MinValue;
    private static int _refreshAttemptCount = 0;
    private const int MAX_REFRESH_ATTEMPTS = 3;
    private static readonly TimeSpan REFRESH_COOLDOWN = TimeSpan.FromMinutes(1);

    public TodayViewModel(GraphQLService graphQLService, CognitoAuthService cognitoAuthService, MeditationSessionDatabase sessionDatabase, IAudioService audioService)
    {
        _graphQLService = graphQLService;
        _cognitoAuthService = cognitoAuthService;
        _sessionDatabase = sessionDatabase;
        _audioService = audioService;
        
        // Start loading data immediately
        _initializationTask = LoadTodayDataAsync();
    }

    /// <summary>
    /// Ensures today's session data is loaded before showing the UI.
    /// </summary>
    public Task EnsureDataLoaded() => _initializationTask ?? Task.CompletedTask;

    private async Task LoadTodayDataAsync()
    {
        try
        {
            IsLoading = true;
            await LoadTodayData();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during initial data load: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task StartMeditation()
    {
        // TODO: Navigate to meditation session
        Console.WriteLine("Starting meditation session...");
        // if (Application.Current?.MainPage != null)
            // await Application.Current.MainPage.DisplayAlert("Meditation", "Starting your meditation session...", "OK");
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task TogglePlayback()
    {
        Debug.WriteLine("TogglePlayback command triggered");
        if (TodaySession == null || AudioPlayer == null)
        {
            Debug.WriteLine("TogglePlayback: TodaySession or AudioPlayer is null");
            return;
        }

        try
        {
            Debug.WriteLine($"TogglePlayback: Current IsPlaying state: {IsPlaying}");
            if (!IsPlaying)
            {
                Debug.WriteLine("TogglePlayback: Attempting to start playback");
                // Check if session is downloaded and file exists
                if (!TodaySession.IsDownloaded || string.IsNullOrEmpty(TodaySession.LocalAudioPath) || !File.Exists(TodaySession.LocalAudioPath))
                {
                    Debug.WriteLine($"TogglePlayback: Session {TodaySession.Uuid} needs download - IsDownloaded: {TodaySession.IsDownloaded}, File exists: {(!string.IsNullOrEmpty(TodaySession.LocalAudioPath) && File.Exists(TodaySession.LocalAudioPath))}");
                    
                    // Update session state if file is missing
                    if (TodaySession.IsDownloaded)
                    {
                        TodaySession.IsDownloaded = false;
                        TodaySession.LocalAudioPath = null;
                        TodaySession.DownloadedAt = null;
                        TodaySession.FileSizeBytes = null;
                        await _sessionDatabase.SaveSessionAsync(TodaySession);
                    }
                    
                    await DownloadSessionInternal(true);
                    if (!TodaySession.IsDownloaded)
                    {
                        Debug.WriteLine($"TogglePlayback: Download failed for session {TodaySession.Uuid}");
                        return; // Download failed
                    }
                }

                var fileInfo = new FileInfo(TodaySession.LocalAudioPath);
                Debug.WriteLine($"TogglePlayback: Attempting to play audio file: {TodaySession.LocalAudioPath} (Size: {fileInfo.Length} bytes, Last modified: {fileInfo.LastWriteTime})");

                // Verify file is readable
                try
                {
                    using (var stream = File.OpenRead(TodaySession.LocalAudioPath))
                    {
                        Debug.WriteLine($"File stream opened successfully, length: {stream.Length}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error verifying file: {ex.Message}");
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                        if (page != null)
                        {
                            await page.DisplayAlert(
                                "Error", 
                                "The audio file appears to be corrupted. Please try downloading the session again.", 
                                "OK");
                        }
                    });
                    return;
                }

                // Start playback
                Debug.WriteLine($"TogglePlayback: Current state = {AudioPlayer.CurrentState}");
                if (AudioPlayer.CurrentState == MediaElementState.Paused || 
                    AudioPlayer.CurrentState == MediaElementState.Stopped)
                {
                    AudioPlayer.Play();
                    IsPlaying = true;
                    Debug.WriteLine("TogglePlayback: Play() called successfully");
                }
                else
                {
                    Debug.WriteLine($"TogglePlayback: Current state is {AudioPlayer.CurrentState}, cannot play");
                }
            }
            else
            {
                // Pause playback
                Debug.WriteLine($"TogglePlayback: Current state = {AudioPlayer.CurrentState}");
                if (AudioPlayer.CurrentState == MediaElementState.Playing)
                {
                    AudioPlayer.Pause();
                    IsPlaying = false;
                    Debug.WriteLine("TogglePlayback: Pause() called successfully");
                }
                else
                {
                    Debug.WriteLine($"TogglePlayback: Current state is {AudioPlayer.CurrentState}, cannot pause");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TogglePlayback: Error toggling playback: {ex.Message}\nStack trace: {ex.StackTrace}");
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
                await page.DisplayAlert("Error", $"An error occurred while controlling playback: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task DownloadTodaySession()
    {
        await DownloadSessionInternal(true);
    }
    
    private async Task DownloadSessionInternal(bool showDialogs = false)
    {
        if (TodaySession == null || IsDownloading) return;

        try
        {
            IsDownloading = true;
            DownloadStatus = "Getting download URL...";

            // Get presigned URL for the session
            var presignedUrl = await _audioService.GetPresignedUrlAsync(TodaySession.Uuid);
            if (string.IsNullOrEmpty(presignedUrl))
            {
                DownloadStatus = "Failed to get download URL";
                var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                if (showDialogs && page != null)
                {
                    await page.DisplayAlert(
                        "Error", 
                        "Failed to get download URL for the session", 
                        "OK");
                }
                IsDownloading = false;
                return;
            }

            DownloadStatus = "Downloading session...";

            // Download the audio file
            var success = await _audioService.DownloadSessionAudioAsync(TodaySession, presignedUrl);
            if (success)
            {
                // Update session in database
                await _sessionDatabase.SaveSessionAsync(TodaySession);

                // --- Fix: Reload session from DB and reassign TodaySession to trigger UI update ---
                if (!string.IsNullOrEmpty(TodaySession.Uuid))
                {
                    var updatedSession = (await _sessionDatabase.GetSessionsAsync())
                        .FirstOrDefault(s => s.Uuid == TodaySession.Uuid);
                    if (updatedSession != null)
                    {
                        TodaySession = updatedSession;
                    }
                }
                // --- End fix ---

                DownloadStatus = "Download complete!";

                var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                if (showDialogs && page != null)
                {
                    await page.DisplayAlert(
                        "Success", 
                        "Session downloaded successfully!", 
                        "OK");
                }
            }
            else
            {
                DownloadStatus = "Download failed";
                var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                if (showDialogs && page != null)
                {
                    await page.DisplayAlert(
                        "Error", 
                        "Failed to download the session", 
                        "OK");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading session: {ex.Message}");
            DownloadStatus = "Download failed";
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (showDialogs && page != null)
            {
                await page.DisplayAlert(
                    "Error", 
                    "An error occurred while downloading the session", 
                    "OK");
            }
        }
        finally
        {
            IsDownloading = false;
            // Clear status after delay
            await Task.Delay(3000);
            DownloadStatus = string.Empty;
        }
    }

    [RelayCommand]
    private void StopPlayback()
    {
        try
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping playback: {ex.Message}");
        }
    }

    // MediaElement event handlers called from the View
    public void SetMediaElement(MediaElement player)
    {
        AudioPlayer = player;
        AudioPlayer.MediaOpened += OnMediaOpened;
        AudioPlayer.MediaEnded += OnMediaEnded;
        AudioPlayer.MediaFailed += OnMediaFailed;
        AudioPlayer.PositionChanged += OnPositionChanged;
    }

    public void CleanupMediaElement()
    {
        if (AudioPlayer != null)
        {
            AudioPlayer.MediaOpened -= OnMediaOpened;
            AudioPlayer.MediaEnded -= OnMediaEnded;
            AudioPlayer.MediaFailed -= OnMediaFailed;
            AudioPlayer.PositionChanged -= OnPositionChanged;
            AudioPlayer = null;
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (AudioPlayer == null) return;
        Debug.WriteLine($"OnMediaOpened: Duration = {AudioPlayer.Duration}, State = {AudioPlayer.CurrentState}");
        PlaybackDuration = AudioPlayer.Duration;
        OnPropertyChanged(nameof(TotalDurationText));
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (AudioPlayer == null) return;
        Debug.WriteLine($"OnMediaEnded: Event received, State = {AudioPlayer.CurrentState}");
        IsPlaying = false;
        IsPaused = false;
        PlaybackPosition = TimeSpan.Zero;
        PlaybackProgress = 0;
        OnPropertyChanged(nameof(CurrentPositionText));
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        if (AudioPlayer == null) return;
        Debug.WriteLine($"OnMediaFailed: Error = {e.ErrorMessage}, State = {AudioPlayer.CurrentState}");
        IsPlaying = false;
        IsPaused = false;
        
        Debug.WriteLine($"Current session state - Uuid: {TodaySession?.Uuid}, IsDownloaded: {TodaySession?.IsDownloaded}, LocalAudioPath: {TodaySession?.LocalAudioPath}");
        
        if (TodaySession != null && !string.IsNullOrEmpty(TodaySession.LocalAudioPath))
        {
            try
            {
                var fileInfo = new FileInfo(TodaySession.LocalAudioPath);
                Debug.WriteLine($"Audio file info - Exists: {fileInfo.Exists}, Size: {fileInfo.Length} bytes, Last modified: {fileInfo.LastWriteTime}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting file info: {ex.Message}");
            }
        }
        
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlert(
                    "Playback Error", 
                    $"Failed to play audio: {e.ErrorMessage}\n\nPlease try downloading the session again.", 
                    "OK");
            }
        });
    }

    private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        if (AudioPlayer == null) return;
        Debug.WriteLine($"OnPositionChanged: Position = {e.Position}, Duration = {AudioPlayer.Duration}, State = {AudioPlayer.CurrentState}");
        PlaybackPosition = e.Position;
        PlaybackDuration = AudioPlayer.Duration;
        PlaybackProgress = AudioPlayer.Duration.TotalSeconds > 0 ? e.Position.TotalSeconds / AudioPlayer.Duration.TotalSeconds : 0;
        OnPropertyChanged(nameof(CurrentPositionText));
        OnPropertyChanged(nameof(TotalDurationText));
    }

    [RelayCommand]
    private Task RequestNewSession() {
        // TODO: Create a new meditation session with notes
        Console.WriteLine($"Requesting new session with notes: {SessionNotes}");
        // if (Application.Current?.MainPage != null)
            // await Application.Current.MainPage.DisplayAlert("Meditation", $"Requesting new session with notes: {SessionNotes}", "OK");
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SelectMood(string mood)
    {
        if (int.TryParse(mood, out int moodValue))
        {
            SelectedMood = moodValue;
            var page2 = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page2 != null)
                await page2.DisplayAlert("Meditation", $"Selected mood: {moodValue}", "OK");
        }
    }

    private async Task LoadTodayData()
    {
        if (Microsoft.Maui.Networking.Connectivity.NetworkAccess != Microsoft.Maui.Networking.NetworkAccess.Internet)
        {
            Debug.WriteLine("No internet connection, loading local data only.");
            await LoadLocalSessionsForToday();
            return;
        }
        try
        {
            var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                await LoadLocalSessionsForToday();
                return;
            }
            var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
            var userId = attributes.FirstOrDefault(a => a.Name == "sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                await LoadLocalSessionsForToday();
                return;
            }
            string query = await LoadGraphQLQueryFromAssetAsync("ListUserMeditationSessions.graphql");
            if (string.IsNullOrWhiteSpace(query))
            {
                Debug.WriteLine("GraphQL query is empty or failed to load.");
                query = "query ListUserMeditationSessions($userID: ID!) { listUserMeditationSessions(userID: $userID) { sessionID userID timestamp audioPath status } }";
            }
            var variables = new { userID = userId };
            var result = await _graphQLService.QueryAsync(query, variables);
            Debug.WriteLine($"GraphQL query result: {result.RootElement}");
            int savedCount = 0;
            if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                dataElem.TryGetProperty("listUserMeditationSessions", out var sessionsElem) &&
                sessionsElem.ValueKind == JsonValueKind.Array)
            {
                Debug.WriteLine($"Found {sessionsElem.GetArrayLength()} sessions from GraphQL.");
                
                // Get existing sessions to preserve download status
                var existingSessions = await _sessionDatabase.GetSessionsAsync();
                var existingSessionsDict = existingSessions.ToDictionary(s => s.Uuid);
                
                // Clear all sessions
                await _sessionDatabase.ClearAllSessionsAsync();
                
                foreach (var sessionElem in sessionsElem.EnumerateArray())
                {
                    DateTime timestampVal = DateTime.MinValue;
                    if (sessionElem.TryGetProperty("timestamp", out var tsElem))
                    {
                        if (tsElem.ValueKind == JsonValueKind.Number)
                        {
                            try
                            {
                                long millis;
                                if (tsElem.TryGetInt64(out millis))
                                {
                                    timestampVal = DateTimeOffset.FromUnixTimeMilliseconds(millis).DateTime;
                                }
                                else
                                {
                                    double millisDouble = tsElem.GetDouble();
                                    millis = Convert.ToInt64(millisDouble);
                                    timestampVal = DateTimeOffset.FromUnixTimeMilliseconds(millis).DateTime;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Invalid timestamp value (number): {tsElem} - {ex.Message}");
                                continue;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Invalid timestamp value kind: {tsElem.ValueKind}, value: {tsElem}");
                            continue;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Session missing 'timestamp' property, skipping.");
                        continue;
                    }

                    var sessionId = sessionElem.TryGetProperty("sessionID", out var idElem) ? idElem.GetString() ?? string.Empty : string.Empty;
                    var session = new MeditationApp.Models.MeditationSession
                    {
                        Uuid = sessionId,
                        UserID = sessionElem.TryGetProperty("userID", out var userElem) ? userElem.GetString() ?? string.Empty : string.Empty,
                        Timestamp = timestampVal,
                        AudioPath = sessionElem.TryGetProperty("audioPath", out var audioElem) ? audioElem.GetString() ?? string.Empty : string.Empty,
                        Status = sessionElem.TryGetProperty("status", out var statusElem) ? statusElem.GetString() ?? string.Empty : string.Empty
                    };

                    // Preserve download status from existing session if available
                    if (existingSessionsDict.TryGetValue(sessionId, out var existingSession))
                    {
                        session.IsDownloaded = existingSession.IsDownloaded;
                        session.LocalAudioPath = existingSession.LocalAudioPath;
                        session.DownloadedAt = existingSession.DownloadedAt;
                        session.FileSizeBytes = existingSession.FileSizeBytes;
                    }

                    await _sessionDatabase.SaveSessionAsync(session);
                    savedCount++;
                }
                Debug.WriteLine($"Saved {savedCount} sessions to local DB.");
            }
            await LoadLocalSessionsForToday();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in LoadTodayData: {ex.Message}");
            
            if (IsTokenRelatedError(ex))
            {
                // Try to refresh token using centralized method
                if (await TryRefreshTokenAsync())
                {
                    // Retry operation after successful token refresh
                    try
                    {
                        await LoadTodayData();
                        return;
                    }
                    catch (Exception retryEx)
                    {
                        Debug.WriteLine($"Retry after token refresh failed in LoadTodayData: {retryEx.Message}");
                    }
                }
            }
            
            // Fallback to local data
            Debug.WriteLine("Falling back to local sessions data from LoadTodayData");
            await LoadLocalSessionsForToday();
        }
    }

    private async Task LoadLocalSessionsForToday()
    {
        var allSessions = await _sessionDatabase.GetSessionsAsync();
        var today = DateTime.Today;
        var sessions = allSessions.Where(s => s.Timestamp.Date == today).ToList();
        
        // Verify file existence for each session
        foreach (var session in sessions)
        {
            if (session.IsDownloaded && !string.IsNullOrEmpty(session.LocalAudioPath))
            {
                if (!File.Exists(session.LocalAudioPath))
                {
                    Debug.WriteLine($"Session {session.Uuid} marked as downloaded but file missing at {session.LocalAudioPath}");
                    // Update session state to reflect missing file
                    session.IsDownloaded = false;
                    session.LocalAudioPath = null;
                    session.DownloadedAt = null;
                    session.FileSizeBytes = null;
                    await _sessionDatabase.SaveSessionAsync(session);
                }
            }
        }
        
        TodaySessions = new ObservableCollection<MeditationApp.Models.MeditationSession>(sessions);
        TodaySession = sessions.FirstOrDefault();
        HasTodaySession = TodaySession != null;

        // Auto-download the session if it exists but isn't downloaded yet
        if (HasTodaySession && TodaySession != null && !TodaySession.IsDownloaded)
        {
            // Start download automatically without showing dialogs
            await DownloadSessionInternal(false);
        }
    }

    private async Task<string> LoadGraphQLQueryFromAssetAsync(string filename)
    {
        try
        {
            using var stream = await Microsoft.Maui.Storage.FileSystem.OpenAppPackageFileAsync($"GraphQL/Queries/{filename}");
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load GraphQL asset: {filename}, {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Centralized method to handle token refresh logic with rate limiting and proper error handling
    /// </summary>
    private async Task<bool> TryRefreshTokenAsync()
    {
        // Check if we're within the cooldown period
        if (DateTime.Now - _lastRefreshAttempt < REFRESH_COOLDOWN && _refreshAttemptCount >= MAX_REFRESH_ATTEMPTS)
        {
            Debug.WriteLine($"Token refresh rate limited. Last attempt: {_lastRefreshAttempt}, Attempts: {_refreshAttemptCount}");
            return false;
        }

        // Reset attempt count if cooldown period has passed
        if (DateTime.Now - _lastRefreshAttempt >= REFRESH_COOLDOWN)
        {
            _refreshAttemptCount = 0;
        }

        try
        {
            _lastRefreshAttempt = DateTime.Now;
            _refreshAttemptCount++;

            var refreshToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("refresh_token");
            if (string.IsNullOrEmpty(refreshToken))
            {
                Debug.WriteLine("No refresh token available - logging out user");
                await LogoutAndNavigateToLogin();
                return false;
            }

            Debug.WriteLine($"Attempting token refresh (attempt {_refreshAttemptCount}/{MAX_REFRESH_ATTEMPTS})");
            var refreshResult = await _cognitoAuthService.RefreshTokenAsync(refreshToken);
            
            if (refreshResult.IsSuccess && !string.IsNullOrEmpty(refreshResult.AccessToken))
            {
                // Update stored tokens
                await Microsoft.Maui.Storage.SecureStorage.SetAsync("access_token", refreshResult.AccessToken);
                
                if (!string.IsNullOrEmpty(refreshResult.IdToken))
                    await Microsoft.Maui.Storage.SecureStorage.SetAsync("id_token", refreshResult.IdToken);
                
                if (!string.IsNullOrEmpty(refreshResult.RefreshToken))
                    await Microsoft.Maui.Storage.SecureStorage.SetAsync("refresh_token", refreshResult.RefreshToken);

                // Reset attempt counter on success
                _refreshAttemptCount = 0;
                Debug.WriteLine("Token refresh successful");
                return true;
            }
            else
            {
                Debug.WriteLine($"Token refresh failed: {refreshResult.ErrorMessage}");
                
                // Check if refresh token is expired/invalid - if so, logout user
                if (IsRefreshTokenExpiredError(refreshResult.ErrorMessage))
                {
                    Debug.WriteLine("Refresh token is expired or invalid - logging out user");
                    await LogoutAndNavigateToLogin();
                }
                
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception during token refresh: {ex.Message}");
            
            // Check if the exception indicates refresh token expiry
            if (IsRefreshTokenExpiredError(ex.Message))
            {
                Debug.WriteLine("Refresh token exception indicates expiry - logging out user");
                await LogoutAndNavigateToLogin();
            }
            
            return false;
        }
    }

    /// <summary>
    /// Helper method to determine if an exception indicates token issues
    /// </summary>
    private static bool IsTokenRelatedError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("revoked") || 
               message.Contains("expired") || 
               message.Contains("token") ||
               message.Contains("unauthorized") ||
               message.Contains("access_denied");
    }

    /// <summary>
    /// Helper method to determine if an error message indicates refresh token expiry
    /// </summary>
    private static bool IsRefreshTokenExpiredError(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return false;
            
        var message = errorMessage.ToLowerInvariant();
        return message.Contains("refresh token") && message.Contains("expired") ||
               message.Contains("refresh token") && message.Contains("invalid") ||
               message.Contains("token_expired") ||
               message.Contains("refresh_token_expired") ||
               message.Contains("notauthorized") ||
               message.Contains("invalid_grant");
    }

    /// <summary>
    /// Clears all tokens and navigates user back to login page
    /// </summary>
    private async Task LogoutAndNavigateToLogin()
    {
        try
        {
            Debug.WriteLine("Logging out user due to expired/invalid refresh token");
            
            // Clear all stored tokens
            Microsoft.Maui.Storage.SecureStorage.Remove("access_token");
            Microsoft.Maui.Storage.SecureStorage.Remove("id_token");
            Microsoft.Maui.Storage.SecureStorage.Remove("refresh_token");
            
            // Reset refresh attempt counters
            _refreshAttemptCount = 0;
            _lastRefreshAttempt = DateTime.MinValue;
            
            // Navigate to login page on main thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var page3 = Application.Current?.Windows?.FirstOrDefault()?.Page;
                    if (page3 != null)
                        await page3.DisplayAlert(
                            "Session Expired", 
                            "Your session has expired. Please log in again.", 
                            "OK");
                    
                    await Shell.Current.GoToAsync("///LoginPage");
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"Navigation error during logout: {navEx.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during logout: {ex.Message}");
        }
    }

    // You might need to add this property changed notification for FormattedDate
    partial void OnCurrentDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FormattedDate));
    }

    partial void OnIsPlayingChanged(bool value)
    {
        PlayPauseIcon = value ? "⏸" : "▶";
        Debug.WriteLine($"IsPlaying changed to {value}, icon updated to {PlayPauseIcon}");
    }
}
