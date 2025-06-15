using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeditationApp.Services;
using MeditationApp.Models;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Core.Primitives;
using MeditationApp.Utils;
using System.ComponentModel;
using Microsoft.Maui.Storage;
using System;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace MeditationApp.ViewModels;

public partial class TodayViewModel : ObservableObject, IAudioPlayerViewModel
{
    [ObservableProperty]
    private DateTime _currentDate = DateTime.Now;

    [ObservableProperty]
    private int? _selectedMood = null; // Default to neutral mood

    [ObservableProperty]
    private MeditationSession? _todaySession = null;

    [ObservableProperty]
    private bool _hasTodaySession = false;

    [ObservableProperty]
    private string _sessionNotes = string.Empty;

    [ObservableProperty]
    private bool _isDownloading = false;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    // [ObservableProperty]
    // private MediaElement? _audioPlayer;

    [ObservableProperty]
    private bool _isLoading = false; // Start with false to prevent initial loading state

    // Track if this is the initial load to prevent UI flicker
    private bool _isInitialLoad = true;

    // Flag to prevent recursive property change notifications during data loading
    private bool _isLoadingData = false;

    [ObservableProperty]
    private bool _isPolling = false;

    [ObservableProperty]
    private string _pollingStatus = string.Empty;

    [ObservableProperty]
    private bool _hasExistingInsights = false;

    [ObservableProperty]
    private DateTime _insightsDate = DateTime.Now.Date;

    [ObservableProperty]
    private bool _isSyncingInsights = false;

    [ObservableProperty]
    private bool _isRequestingSession = false;

    [ObservableProperty]
    private bool _isMoodSelectorExpanded = false;

    [ObservableProperty]
    private bool _isAudioPlayerSheetOpen = false;

    [ObservableProperty]
    private bool _isBottomSheetExpanded;

    // Add sync-related properties
    [ObservableProperty]
    private bool _isSyncing = false;

    [ObservableProperty]
    private string _syncStatus = string.Empty;

    private UserDailyInsights? _currentInsights;

    [ObservableProperty]
    private ObservableCollection<MoodDataPoint> _moodData = new();

    public string FormattedDate => CurrentDate.ToString("dddd, MMMM dd, yyyy");

    // Delegate audio-related properties to the AudioPlayerService
    public bool IsPlaying => _audioPlayerService.IsPlaying;
    public double PlaybackProgress => _audioPlayerService.PlaybackProgress;
    public TimeSpan PlaybackPosition => _audioPlayerService.PlaybackPosition;
    public TimeSpan PlaybackDuration => _audioPlayerService.PlaybackDuration;
    public string CurrentPositionText => _audioPlayerService.CurrentPositionText;
    public string TotalDurationText => _audioPlayerService.TotalDurationText;
    public string PlayPauseIcon => _audioPlayerService.PlayPauseIcon;
    public string PlayPauseIconImage => IsPlaying ? "pause" : "play";

    // Delegate metadata properties to the AudioPlayerService
    public string UserName => _audioPlayerService.UserName;
    public DateTime SessionDate => _audioPlayerService.SessionDate;
    public string SessionDateText => _audioPlayerService.SessionDateText;

    private readonly GraphQLService _graphQLService;
    private readonly CognitoAuthService _cognitoAuthService;
    private readonly MeditationSessionDatabase _sessionDatabase;
    private readonly SessionStatusPoller _sessionStatusPoller;
    private readonly IAudioDownloadService _audioDownloadService;
    private readonly AudioPlayerService _audioPlayerService;
    private readonly DatabaseSyncService _databaseSyncService;
    private readonly MoodChartService _moodChartService;

    private Task? _initializationTask;
    private CancellationTokenSource? _pollingCts;

    // Token refresh management
    private static DateTime _lastRefreshAttempt = DateTime.MinValue;
    private static int _refreshAttemptCount = 0;
    private const int MAX_REFRESH_ATTEMPTS = 3;
    private static readonly TimeSpan REFRESH_COOLDOWN = TimeSpan.FromMinutes(1);

    public TodayViewModel(GraphQLService graphQLService, CognitoAuthService cognitoAuthService, MeditationSessionDatabase sessionDatabase, IAudioDownloadService audioDownloadService, SessionStatusPoller sessionStatusPoller, AudioPlayerService audioPlayerService, DatabaseSyncService databaseSyncService, MoodChartService moodChartService)
    {
        _graphQLService = graphQLService;
        _cognitoAuthService = cognitoAuthService;
        _sessionDatabase = sessionDatabase;
        _audioDownloadService = audioDownloadService;
        _sessionStatusPoller = sessionStatusPoller;
        _audioPlayerService = audioPlayerService;
        _databaseSyncService = databaseSyncService;
        _moodChartService = moodChartService;

        // Subscribe to events
        _audioPlayerService.MediaOpened += OnMediaOpened;
        _audioPlayerService.MediaEnded += OnMediaEnded;

        // Subscribe to property changes to forward audio-related properties
        _audioPlayerService.PropertyChanged += OnAudioPlayerServicePropertyChanged;

        // Subscribe to sync events
        _databaseSyncService.SyncStatusChanged += OnSyncStatusChanged;

        // Start loading data immediately
        _initializationTask = LoadTodayDataAsync();
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PlaybackDuration));
        OnPropertyChanged(nameof(TotalDurationText));
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(PlayPauseIcon));

        // Hide the bottom sheet when audio ends
        IsAudioPlayerSheetOpen = false;
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
            case nameof(AudioPlayerService.UserName):
                OnPropertyChanged(nameof(UserName));
                break;
            case nameof(AudioPlayerService.SessionDate):
                OnPropertyChanged(nameof(SessionDate));
                OnPropertyChanged(nameof(SessionDateText));
                break;
        }
    }

    private void OnSyncStatusChanged(object? sender, SyncStatusEventArgs e)
    {
        IsSyncing = e.Status == Services.SyncStatus.Starting ||
                   e.Status == Services.SyncStatus.SyncingSessions ||
                   e.Status == Services.SyncStatus.SyncingInsights;
        SyncStatus = e.Message ?? string.Empty;
        Debug.WriteLine($"[Sync] Status: {e.Status}, Message: {e.Message}");
    }

    /// <summary>
    /// Ensures today's session data is loaded before showing the UI.
    /// </summary>
    public Task EnsureDataLoaded()
    {
        if (_initializationTask == null)
        {
            Debug.WriteLine("[EnsureDataLoaded] Creating new initialization task");
            _initializationTask = LoadTodayDataAsync();
        }
        return _initializationTask;
    }

    /// <summary>
    /// Resets the ViewModel state for logout/login cycles
    /// </summary>
    public void Reset()
    {
        Debug.WriteLine("[Reset] Resetting TodayViewModel state");

        // Cancel and clear the old initialization task
        _initializationTask = null;

        // Reset state flags
        _isInitialLoad = true;
        _isLoadingData = false;

        // Clear current session and insights
        TodaySession = null;
        HasTodaySession = false;
        _currentInsights = null;
        HasExistingInsights = false;

        // Reset UI properties
        SessionNotes = string.Empty;
        SelectedMood = null;
        InsightsDate = DateTime.Now.Date;
        CurrentDate = DateTime.Now.Date;

        // Reset loading states
        IsLoading = false;
        IsSyncingInsights = false;

        // Reset refresh tracking
        _refreshAttemptCount = 0;
        _lastRefreshAttempt = DateTime.MinValue;

        Debug.WriteLine("[Reset] TodayViewModel state reset completed");
    }

    private async Task LoadTodayDataAsync()
    {
        try
        {
            Debug.WriteLine("[Init] Starting initial data load");
            // Only show loading state if this is not the initial load from splash screen
            if (!_isInitialLoad)
            {
                IsLoading = true;
            }
            await LoadTodayData();
            await LoadMoodChartDataAsync();
            Debug.WriteLine("[Init] Initial data load completed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Init] Error during initial data load: {ex.Message}");
        }
        finally
        {
            // Don't wait for polling to complete if status is REQUESTED
            if (TodaySession?.Status == MeditationSessionStatus.REQUESTED)
            {
                Debug.WriteLine("[Init] Session is in REQUESTED state, starting polling in background");
                _ = StartPollingSessionStatus(TodaySession.Uuid); // Fire and forget
            }

            // Always set loading to false and mark initial load as complete
            IsLoading = false;
            _isInitialLoad = false;
            Debug.WriteLine("[Init] Loading state set to false");
        }
    }

    [RelayCommand]
    private async Task TogglePlayback()
    {
        // If we're currently playing, just pause
        if (_audioPlayerService.IsPlaying)
        {
            _audioPlayerService.Pause();
            return;
        }

        // If we're not playing, we need to ensure audio is loaded before playing or resuming
        if (TodaySession != null && TodaySession.IsDownloaded && !string.IsNullOrEmpty(TodaySession.LocalAudioPath))
        {
            // If paused and not at start or end, resume
            if (_audioPlayerService.PlaybackPosition > TimeSpan.Zero && _audioPlayerService.PlaybackPosition < _audioPlayerService.PlaybackDuration)
            {
                _audioPlayerService.Resume();
                IsAudioPlayerSheetOpen = true;
            }
            else
            {
                await LoadAudioWithMetadata();
                var success = await _audioPlayerService.PlayFromFileAsync(TodaySession.LocalAudioPath);
                if (success)
                {
                    IsAudioPlayerSheetOpen = true;
                }
            }
        }
        else
        {
            // Show message that session needs to be downloaded first
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlert("Download Required",
                    "Please download the session first before playing.",
                    "OK");
            }
        }

        OnPropertyChanged(nameof(PlayPauseIconImage));
    }

    private async Task LoadAudioWithMetadata()
    {
        if (TodaySession == null || string.IsNullOrEmpty(TodaySession.LocalAudioPath))
            return;

        try
        {
            // Get user profile information for metadata
            string userName = "User"; // Default fallback

            // Try to get user information from stored tokens
            try
            {
                var accessToken = await SecureStorage.Default.GetAsync("access_token");
                if (!string.IsNullOrEmpty(accessToken))
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

            // Set audio source with metadata
            _audioPlayerService.SetAudioSourceWithMetadata(
                TodaySession.LocalAudioPath,
                userName,
                TodaySession.Timestamp);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading audio with metadata: {ex.Message}");
            // Fallback to basic audio loading if metadata fails
            _audioPlayerService.SetAudioSourceWithMetadata(TodaySession.LocalAudioPath, "User", TodaySession.Timestamp);
        }
    }

    [RelayCommand]
    private async Task DownloadTodaySession()
    {
        await DownloadSessionInternal();
    }

    private async Task DownloadSessionInternal()
    {
        if (TodaySession == null || IsDownloading) return;

        try
        {
            IsDownloading = true;
            DownloadStatus = "Getting download URL...";

            // Get presigned URL for the session
            var presignedUrl = await _audioDownloadService.GetPresignedUrlAsync(TodaySession.Uuid);
            if (string.IsNullOrEmpty(presignedUrl))
            {
                DownloadStatus = "Failed to get download URL";
                IsDownloading = false;
                return;
            }

            DownloadStatus = "Downloading session...";

            // Download the audio file
            var success = await _audioDownloadService.DownloadSessionAudioAsync(TodaySession, presignedUrl);
            if (success)
            {
                // Update session in database
                await _sessionDatabase.SaveSessionAsync(TodaySession);

                // Trigger property changed to update UI - no need to reload from DB
                // The TodaySession object already has the correct download status set by AudioService
                OnPropertyChanged(nameof(TodaySession));

                DownloadStatus = "Download complete!";


            }
            else
            {
                DownloadStatus = "Download failed";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading session: {ex.Message}");
            DownloadStatus = "Download failed";
        }
        finally
        {
            IsDownloading = false;
            // Clear status after delay
            await Task.Delay(3000);
            DownloadStatus = string.Empty;
        }
    }

    // MediaElement event handlers called from the View
    // public void SetMediaElement(MediaElement player) { /* commented out */ }
    // public void CleanupMediaElement() { /* commented out */ }

    // private void OnMediaOpened(object? sender, EventArgs e) { /* commented out */ }
    // private void OnMediaEnded(object? sender, EventArgs e) { /* commented out */ }
    // private void OnMediaFailed(object? sender, MediaFailedEventArgs e) { /* commented out */ }
    // private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e) { /* commented out */ }
    // private void OnStateChanged(object? sender, MediaStateChangedEventArgs e) { /* commented out */ }

    [RelayCommand]
    private async Task RequestNewSession()
    {
        if (string.IsNullOrEmpty(SessionNotes))
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlert("Notes Required",
                    "Please add some notes about how you're feeling or what you'd like to focus on.",
                    "OK");
            }
            return;
        }

        try
        {
            IsRequestingSession = true;
            // First verify we have a valid access token
            var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.WriteLine("[RequestSession] No access token found");
                var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                if (page != null)
                {
                    await page.DisplayAlert("Authentication Error",
                        "Your session has expired. Please log in again.",
                        "OK");
                    await LogoutAndNavigateToLogin();
                }
                return;
            }

            var userId = await GetCurrentUserId();
            Debug.WriteLine($"[RequestSession] Retrieved userID: {userId}");

            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("[RequestSession] Cannot create session: No user ID available");
                var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                if (page != null)
                {
                    await page.DisplayAlert("Error",
                        "Unable to create session. Please try logging out and back in.",
                        "OK");
                    await LogoutAndNavigateToLogin();
                }
                return;
            }

            // Try the request, with at most one token refresh
            bool hasRefreshedToken = false;
            while (true) // We'll break out of this loop after one refresh attempt
            {
                Debug.WriteLine("[RequestSession] Starting session creation request");

                // Load the mutation
                string mutation = await GraphQLQueryLoader.LoadQueryAsync("CreateMeditationSession.graphql");
                if (string.IsNullOrWhiteSpace(mutation))
                {
                    Debug.WriteLine("[RequestSession] GraphQL mutation is empty or failed to load.");
                    mutation = @"mutation CreateMeditationSession($userID: ID!) {
                        createMeditationSession(userID: $userID) {
                            sessionID
                            userID
                            timestamp
                            audioPath
                            status
                        }
                    }";
                }

                // Prepare variables - sending userID as required by the backend
                var variables = new { userID = userId };

                Debug.WriteLine($"[RequestSession] Executing mutation with userID: {userId}");

                // Call the mutation
                var result = await _graphQLService.QueryAsync(mutation, variables);
                Debug.WriteLine($"[RequestSession] Raw mutation result: {result.RootElement}");

                // Check for GraphQL errors first
                if (result.RootElement.TryGetProperty("errors", out var errorsElem))
                {
                    var errorMessage = "Failed to create meditation session.";
                    if (errorsElem.ValueKind == JsonValueKind.Array && errorsElem.GetArrayLength() > 0)
                    {
                        var firstError = errorsElem[0];
                        if (firstError.TryGetProperty("message", out var messageElem))
                        {
                            errorMessage = messageElem.GetString() ?? errorMessage;
                        }
                    }

                    Debug.WriteLine($"[RequestSession] GraphQL errors: {errorsElem}");

                    // Check if it's an authentication error and we haven't refreshed yet
                    if (!hasRefreshedToken && errorMessage.Contains("not authenticated", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine("[RequestSession] Authentication error detected, attempting token refresh");

                        // Store the old token to check if it actually changed
                        var oldToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");

                        if (await TryRefreshTokenAsync())
                        {
                            // Verify the token actually changed
                            var newToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
                            if (newToken == oldToken)
                            {
                                Debug.WriteLine("[RequestSession] Token refresh did not change the token, logging out");
                                await LogoutAndNavigateToLogin();
                                return;
                            }

                            Debug.WriteLine("[RequestSession] Token refresh successful with new token, retrying request");
                            hasRefreshedToken = true;
                            continue; // Try the request again with the new token
                        }
                        else
                        {
                            Debug.WriteLine("[RequestSession] Token refresh failed, logging out user");
                            await LogoutAndNavigateToLogin();
                            return;
                        }
                    }

                    // If we get here, either:
                    // 1. It's not an auth error
                    // 2. We already tried refreshing
                    // 3. The refresh didn't help
                    var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                    if (page != null)
                    {
                        await page.DisplayAlert("Error", errorMessage, "OK");
                    }
                    return;
                }

                // Check for null response
                if (!result.RootElement.TryGetProperty("data", out var dataElem) ||
                    !dataElem.TryGetProperty("createMeditationSession", out var sessionElem) ||
                    sessionElem.ValueKind == JsonValueKind.Null)
                {
                    Debug.WriteLine("[RequestSession] Received null response from server");
                    var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                    if (page != null)
                    {
                        await page.DisplayAlert("Error",
                            "Failed to create meditation session. Please try again.",
                            "OK");
                    }
                    return;
                }

                // Process successful response
                var sessionId = sessionElem.GetProperty("sessionID").GetString();

                long timestampMillis = 0;
                if (sessionElem.TryGetProperty("timestamp", out var timestampElem))
                {
                    if (timestampElem.ValueKind == JsonValueKind.Number)
                    {
                        // Attempt to read as Int64 first, fallback to Double
                        if (!timestampElem.TryGetInt64(out timestampMillis))
                        {
                            var timestampDouble = timestampElem.GetDouble();
                            timestampMillis = Convert.ToInt64(timestampDouble);
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[RequestSession] Unexpected timestamp value kind: {timestampElem.ValueKind}");
                    }
                }

                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
                    timestampMillis).LocalDateTime;

                var status = MeditationSessionStatusHelper.ParseSessionStatus(sessionElem.GetProperty("status").GetString() ?? MeditationSessionStatus.REQUESTED.ToString());
                var audioPath = sessionElem.TryGetProperty("audioPath", out var audioPathElem) ? audioPathElem.GetString() : null;

                // Create new session object
                var newSession = new MeditationSession
                {
                    Uuid = sessionId ?? Guid.NewGuid().ToString(),
                    UserID = userId,
                    Timestamp = timestamp,
                    Status = status,
                    AudioPath = audioPath ?? string.Empty
                };

                // Save to local database
                await _sessionDatabase.SaveSessionAsync(newSession);

                // Update UI
                TodaySession = newSession;
                HasTodaySession = true;

                // Start polling for status
                await StartPollingSessionStatus(newSession.Uuid);

                Debug.WriteLine($"[RequestSession] Session created successfully - ID: {newSession.Uuid}, Status: {newSession.Status}");
                return; // Success! Exit the method
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RequestSession] Error requesting new session: {ex.Message}\nStack trace: {ex.StackTrace}");

            if (IsTokenRelatedError(ex))
            {
                if (await TryRefreshTokenAsync())
                {
                    try
                    {
                        await RequestNewSession();
                        return;
                    }
                    catch (Exception retryEx)
                    {
                        Debug.WriteLine($"[RequestSession] Retry after token refresh failed: {retryEx.Message}");
                    }
                }
            }

            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlert("Error",
                    "Failed to create meditation session. Please try again.",
                    "OK");
            }
        }
        finally
        {
            IsRequestingSession = false;
        }
    }

    [RelayCommand]
    private async Task SelectMood(string mood)
    {
        // Skip saving if we're currently loading data to prevent recursion
        if (_isLoadingData)
        {
            Debug.WriteLine("[SelectMood] Skipping mood selection during data loading");
            return;
        }

        if (int.TryParse(mood, out int moodValue))
        {
            SelectedMood = moodValue;
            IsMoodSelectorExpanded = false; // Collapse the selector after selection
            await SaveUserDailyInsights();
        }
    }

    [RelayCommand]
    private void ToggleMoodSelector()
    {
        IsMoodSelectorExpanded = !IsMoodSelectorExpanded;
    }

    [RelayCommand]
    private async Task NavigateToPastSessions()
    {
        await Shell.Current.GoToAsync("PastSessionsPage");
    }

    [RelayCommand]
    private void ShowAudioPlayerSheet()
    {
        IsAudioPlayerSheetOpen = true;
    }

    [RelayCommand]
    private void HideAudioPlayerSheet()
    {
        IsAudioPlayerSheetOpen = false;
    }

    [RelayCommand]
    private void ToggleAudioPlayerSheet()
    {
        IsAudioPlayerSheetOpen = !IsAudioPlayerSheetOpen;
    }

    [RelayCommand]
    private void SeekBackward()
    {
        if (_audioPlayerService.IsPlaying || _audioPlayerService.PlaybackPosition > TimeSpan.Zero)
        {
            var newPosition = _audioPlayerService.PlaybackPosition.Subtract(TimeSpan.FromSeconds(15));
            if (newPosition < TimeSpan.Zero)
                newPosition = TimeSpan.Zero;

            _audioPlayerService.Seek(newPosition.TotalSeconds);
        }
    }

    [RelayCommand]
    private void SeekForward()
    {
        if (_audioPlayerService.IsPlaying || _audioPlayerService.PlaybackPosition > TimeSpan.Zero)
        {
            var newPosition = _audioPlayerService.PlaybackPosition.Add(TimeSpan.FromSeconds(15));
            if (newPosition > _audioPlayerService.PlaybackDuration)
                newPosition = _audioPlayerService.PlaybackDuration;

            _audioPlayerService.Seek(newPosition.TotalSeconds);
        }
    }

    partial void OnSessionNotesChanged(string value)
    {
        Debug.WriteLine($"[Notes] Notes changed to: {value}");

        // Skip saving if we're currently loading data to prevent recursion
        if (_isLoadingData)
        {
            Debug.WriteLine("[Notes] Skipping notes change handling during data loading");
            return;
        }

        // Debounce the save operation to avoid too many API calls
        _ = Task.Run(async () =>
        {
            Debug.WriteLine("[Notes] Starting debounce timer");
            await Task.Delay(1000); // Wait 1 second after last change
            Debug.WriteLine($"[Notes] Debounce timer completed. Current value: {_sessionNotes}, Debounced value: {value}");
            if (_sessionNotes == value) // Only save if value hasn't changed
            {
                Debug.WriteLine("[Notes] Value unchanged after debounce, calling SaveUserDailyInsights");
                await SaveUserDailyInsights();
            }
            else
            {
                Debug.WriteLine("[Notes] Value changed during debounce, skipping save");
            }
        });
    }

    private async Task SaveUserDailyInsights()
    {
        try
        {
            // Prevent recursive calls when loading data
            if (_isLoadingData)
            {
                Debug.WriteLine("[SaveInsights] Skipping save during data loading to prevent recursion");
                return;
            }

            Debug.WriteLine("[SaveInsights] Starting to save user daily insights");
            var userId = await GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("[SaveInsights] Cannot save insights: No user ID available");
                return;
            }

            // Save to local database first
            if (_currentInsights == null)
            {
                _currentInsights = new Models.UserDailyInsights
                {
                    UserID = userId,
                    Date = DateTime.Now.Date,
                    Notes = SessionNotes,
                    Mood = SelectedMood,
                    IsSynced = false
                };
            }
            else
            {
                _currentInsights.Notes = SessionNotes;
                _currentInsights.Mood = SelectedMood;
                _currentInsights.IsSynced = false;
            }

            await _sessionDatabase.SaveDailyInsightsAsync(_currentInsights);
            Debug.WriteLine("[SaveInsights] Saved insights to local database");

            // Try to sync with server if online
            if (Microsoft.Maui.Networking.Connectivity.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.Internet)
            {
                IsSyncingInsights = true;
                try
                {
                    Debug.WriteLine($"[SaveInsights] Syncing insights with server - Notes: {SessionNotes}, Mood: {SelectedMood}");
                    string mutation = await GraphQLQueryLoader.LoadQueryAsync("AddUserDailyInsights.graphql");
                    if (string.IsNullOrWhiteSpace(mutation))
                    {
                        Debug.WriteLine("[SaveInsights] GraphQL mutation is empty or failed to load.");
                        mutation = @"mutation AddUserDailyInsights($UserDailyInsightsInput: UserDailyInsightsInput!) {
                            addUserDailyInsights(UserDailyInsightsInput: $UserDailyInsightsInput) {
                                userID
                                date
                                notes
                                mood
                            }
                        }";
                    }

                    var input = new
                    {
                        userID = userId,
                        date = DateTime.Now.Date.ToString("yyyy-MM-dd"),
                        notes = SessionNotes,
                        mood = SelectedMood
                    };

                    Debug.WriteLine($"[SaveInsights] Executing mutation with input: {System.Text.Json.JsonSerializer.Serialize(input)}");
                    var variables = new { UserDailyInsightsInput = input };
                    var result = await _graphQLService.QueryAsync(mutation, variables);
                    Debug.WriteLine($"[SaveInsights] GraphQL mutation result: {result.RootElement}");

                    if (result.RootElement.TryGetProperty("errors", out var errorsElem))
                    {
                        Debug.WriteLine($"[SaveInsights] GraphQL errors: {errorsElem}");
                        // Don't mark as synced if there were errors
                        return;
                    }

                    // Mark as synced in local database
                    if (_currentInsights != null)
                    {
                        await _sessionDatabase.MarkInsightsAsSyncedAsync(_currentInsights);
                        Debug.WriteLine("[SaveInsights] Successfully synced insights with server");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SaveInsights] Error syncing with server: {ex.Message}");
                    // Insights will remain marked as unsynced for retry later
                }
                finally
                {
                    IsSyncingInsights = false;
                }
            }
            else
            {
                Debug.WriteLine("[SaveInsights] No internet connection, insights saved locally only");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveInsights] Error saving user daily insights: {ex.Message}\nStack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Loads today's session data and insights, refreshing from server if online
    /// </summary>
    public async Task LoadTodayData()
    {
        if (Microsoft.Maui.Networking.Connectivity.NetworkAccess != Microsoft.Maui.Networking.NetworkAccess.Internet)
        {
            Debug.WriteLine("No internet connection, loading local data only.");
            await LoadLocalSessionsForToday();
            await LoadLocalInsights();
            return;
        }
        try
        {
            var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                await LoadLocalSessionsForToday();
                await LoadLocalInsights();
                return;
            }
            var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
            var userId = attributes.FirstOrDefault(a => a.Name == "sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                await LoadLocalSessionsForToday();
                await LoadLocalInsights();
                return;
            }

            // Start both syncs in parallel with timeout and debugging
            Debug.WriteLine("[LoadTodayData] Starting parallel tasks");
            var sessionsTask = LoadSessionsAsync(userId);
            var insightsTask = LoadDailyInsightsAsync(userId);
            var fullSyncTask = SyncAllInsightsAsync();

            // Also trigger a background sync with the new service (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await TriggerBackgroundSyncAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LoadTodayData] Background sync failed: {ex.Message}");
                }
            });

            // Add timeout to prevent infinite hang
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                try
                {
                    Debug.WriteLine("[LoadTodayData] Waiting for sessions and insights tasks with 20s timeout");

                    // Create a task that completes when both core tasks finish
                    var coreTask = Task.WhenAll(sessionsTask, insightsTask);

                    // Wait with timeout
                    await coreTask.WaitAsync(cts.Token);
                    Debug.WriteLine("[LoadTodayData] Core tasks completed successfully");
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("[LoadTodayData] Tasks timed out after 20 seconds, checking individual task status");

                    // Check which task is hanging
                    if (sessionsTask.IsCompleted)
                        Debug.WriteLine("[LoadTodayData] - Sessions task: COMPLETED");
                    else
                        Debug.WriteLine("[LoadTodayData] - Sessions task: HANGING");

                    if (insightsTask.IsCompleted)
                        Debug.WriteLine("[LoadTodayData] - Insights task: COMPLETED");
                    else
                        Debug.WriteLine("[LoadTodayData] - Insights task: HANGING");

                    // Fall back to local data
                    Debug.WriteLine("[LoadTodayData] Falling back to local data due to timeout");
                    await LoadLocalSessionsForToday();
                    await LoadLocalInsights();
                    return;
                }
            }

            // Process today's insights result
            var insightsResult = await insightsTask;
            if (insightsResult.RootElement.TryGetProperty("data", out var insightsData) &&
                insightsData.TryGetProperty("listUserDailyInsights", out var insightsElem) &&
                insightsElem.ValueKind == JsonValueKind.Array)
            {
                var today = DateTime.Now.Date;
                var todayInsight = insightsElem.EnumerateArray()
                    .FirstOrDefault(i => DateTime.Parse(i.GetProperty("date").GetString() ?? string.Empty, null, System.Globalization.DateTimeStyles.RoundtripKind).Date == today);

                if (todayInsight.ValueKind != JsonValueKind.Undefined)
                {
                    // Set flag to prevent recursive property change notifications
                    _isLoadingData = true;
                    try
                    {
                        var notes = todayInsight.GetProperty("notes").GetString();
                        if (todayInsight.TryGetProperty("mood", out var moodElem) &&
                            moodElem.TryGetInt32(out var mood))
                        {
                            SelectedMood = mood;
                        }
                        SessionNotes = notes ?? string.Empty;
                        HasExistingInsights = true;
                        InsightsDate = today;

                        // Save to local database
                        _currentInsights = new Models.UserDailyInsights
                        {
                            UserID = userId,
                            Date = InsightsDate,
                            Notes = SessionNotes,
                            Mood = SelectedMood,
                            IsSynced = true
                        };
                        await _sessionDatabase.SaveDailyInsightsAsync(_currentInsights);

                        Debug.WriteLine($"Loaded and saved insights from server - Mood: {SelectedMood}, Notes: {SessionNotes}");
                    }
                    finally
                    {
                        _isLoadingData = false;
                    }
                }
                else
                {
                    // Try to load from local database
                    await LoadLocalInsights();
                }
            }

            // Process sessions result
            var sessionsResult = await sessionsTask;
            await ProcessSessionsResult(sessionsResult, userId);

            // Wait for full sync to complete in background
            _ = fullSyncTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.WriteLine($"[SyncInsights] Background sync failed: {t.Exception?.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in LoadTodayData: {ex.Message}");

            if (IsTokenRelatedError(ex))
            {
                if (await TryRefreshTokenAsync())
                {
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

            Debug.WriteLine("Falling back to local data from LoadTodayData");
            await LoadLocalSessionsForToday();
            await LoadLocalInsights();
        }
    }

    private async Task<JsonDocument> LoadDailyInsightsAsync(string userId)
    {
        string query = await GraphQLQueryLoader.LoadQueryAsync("ListUserDailyInsights.graphql");
        if (string.IsNullOrWhiteSpace(query))
        {
            Debug.WriteLine("GraphQL query is empty or failed to load.");
            query = @"query ListUserDailyInsights($userID: ID!) {
                listUserDailyInsights(userID: $userID) {
                    userID
                    date
                    notes
                    mood
                }
            }";
        }
        var variables = new { userID = userId };
        return await _graphQLService.QueryAsync(query, variables);
    }

    private async Task LoadLocalInsights()
    {
        try
        {
            var userId = await GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("[LoadLocal] Cannot load insights: No user ID available");
                return;
            }

            // Only set flag if not already loading to prevent double-setting
            bool wasAlreadyLoading = _isLoadingData;
            if (!wasAlreadyLoading)
            {
                _isLoadingData = true;
            }

            try
            {
                var insights = await _sessionDatabase.GetDailyInsightsAsync(userId, DateTime.Now.Date);
                if (insights != null)
                {
                    _currentInsights = insights;
                    SelectedMood = insights.Mood;
                    SessionNotes = insights.Notes;
                    HasExistingInsights = true;
                    InsightsDate = insights.Date;
                    Debug.WriteLine($"[LoadLocal] Loaded insights from local DB - Mood: {SelectedMood}, Notes: {SessionNotes}");
                }
                else
                {
                    // Clear insights if none exist
                    _currentInsights = null;
                    SelectedMood = null;
                    SessionNotes = string.Empty;
                    HasExistingInsights = false;
                    Debug.WriteLine("[LoadLocal] No insights found in local DB");
                }
            }
            finally
            {
                // Only reset the flag if we set it in this call
                if (!wasAlreadyLoading)
                {
                    _isLoadingData = false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoadLocal] Error loading local insights: {ex.Message}");
        }
    }

    private async Task<JsonDocument> LoadSessionsAsync(string userId)
    {
        string query = await GraphQLQueryLoader.LoadQueryAsync("ListUserMeditationSessions.graphql");
        if (string.IsNullOrWhiteSpace(query))
        {
            Debug.WriteLine("GraphQL query is empty or failed to load.");
            query = "query ListUserMeditationSessions($userID: ID!) { listUserMeditationSessions(userID: $userID) { sessionID userID timestamp audioPath status } }";
        }
        var variables = new { userID = userId };
        return await _graphQLService.QueryAsync(query, variables);
    }

    private async Task ProcessSessionsResult(JsonDocument result, string userId)
    {
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
                                timestampVal = DateTimeOffset.FromUnixTimeMilliseconds(millis).LocalDateTime;
                            }
                            else
                            {
                                double millisDouble = tsElem.GetDouble();
                                millis = Convert.ToInt64(millisDouble);
                                timestampVal = DateTimeOffset.FromUnixTimeMilliseconds(millis).LocalDateTime;
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
                    Status = sessionElem.TryGetProperty("status", out var statusElem) ?
                        MeditationSessionStatusHelper.ParseSessionStatus(statusElem.GetString() ?? MeditationSessionStatus.REQUESTED.ToString()) :
                        MeditationSessionStatus.REQUESTED
                };

                // Preserve download status from existing session if available
                if (existingSessionsDict.TryGetValue(sessionId, out var existingSession))
                {
                    session.IsDownloaded = existingSession.IsDownloaded;
                    session.LocalAudioPath = existingSession.LocalAudioPath;
                    session.DownloadedAt = existingSession.DownloadedAt;
                    session.FileSizeBytes = existingSession.FileSizeBytes;

                    // Preserve higher status (don't downgrade COMPLETED to REQUESTED)
                    if (existingSession.Status == MeditationSessionStatus.COMPLETED &&
                        session.Status == MeditationSessionStatus.REQUESTED)
                    {
                        Debug.WriteLine($"Preserving COMPLETED status for session {sessionId} instead of downgrading to REQUESTED");
                        session.Status = existingSession.Status;
                    }
                }

                await _sessionDatabase.SaveSessionAsync(session);
                savedCount++;
            }
            Debug.WriteLine($"Saved {savedCount} sessions to local DB.");
        }
        await LoadLocalSessionsForToday();
    }

    private async Task LoadLocalSessionsForToday()
    {
        Debug.WriteLine("[LoadLocal] Starting to load local sessions for today");
        var allSessions = await _sessionDatabase.GetSessionsAsync();
        var today = DateTime.Today;
        var sessions = allSessions.Where(s => s.Timestamp.Date == today).ToList();
        Debug.WriteLine($"[LoadLocal] Found {sessions.Count} sessions for today");

        // Verify file existence for each session
        foreach (var session in sessions)
        {
            Debug.WriteLine($"[LoadLocal] Checking session {session.Uuid} - Status: {session.Status}, IsDownloaded: {session.IsDownloaded}");
            if (session.IsDownloaded && !string.IsNullOrEmpty(session.LocalAudioPath))
            {
                if (!File.Exists(session.LocalAudioPath))
                {
                    Debug.WriteLine($"[LoadLocal] Session {session.Uuid} marked as downloaded but file missing at {session.LocalAudioPath}");
                    // Update session state to reflect missing file
                    session.IsDownloaded = false;
                    session.LocalAudioPath = null;
                    session.DownloadedAt = null;
                    session.FileSizeBytes = null;
                    await _sessionDatabase.SaveSessionAsync(session);
                }
            }
        }

        TodaySession = sessions.FirstOrDefault();
        HasTodaySession = TodaySession != null;
        Debug.WriteLine($"[LoadLocal] TodaySession set to: {(TodaySession != null ? $"UUID: {TodaySession.Uuid}, Status: {TodaySession.Status}" : "null")}");

        // Auto-download the session if it exists but isn't downloaded yet
        if (HasTodaySession && TodaySession != null)
        {
            Debug.WriteLine($"[LoadLocal] Session found - Status: {TodaySession.Status}, IsDownloaded: {TodaySession.IsDownloaded}");

            // Auto-download if not downloaded and not in REQUESTED state
            if (!TodaySession.IsDownloaded && TodaySession.Status != MeditationSessionStatus.REQUESTED)
            {
                Debug.WriteLine("[LoadLocal] Starting auto-download for session");
                // Only show download UI if this is not the initial load
                await DownloadSessionSilentlyIfInitialLoad();
            }
        }
        else
        {
            Debug.WriteLine("[LoadLocal] No session found for today");
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

            // Clear view model state first
            ClearViewModelState();

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

                    // Clear the navigation stack and navigate to login
                    await Shell.Current.GoToAsync("LoginPage", animate: true);
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

    /// <summary>
    /// Clears all state in the view model
    /// </summary>
    public void ClearViewModelState()
    {
        try
        {
            // // Stop any playing audio
            // if (IsPlaying)
            // {
            //     StopAudio();
            // } 

            // Clear all properties
            CurrentDate = DateTime.Now;
            SelectedMood = null;
            SessionNotes = string.Empty;
            HasExistingInsights = false;
            InsightsDate = DateTime.MinValue;
            _currentInsights = null;
            _refreshAttemptCount = 0;
            _lastRefreshAttempt = DateTime.MinValue;
            IsSyncingInsights = false;
            IsLoading = false;
            // Audio state is now managed by AudioPlayerService

            // Clear any cached data
            _sessionDatabase.ClearCache();

            Debug.WriteLine("TodayViewModel state cleared successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing TodayViewModel state: {ex.Message}");
        }
    }

    // You might need to add this property changed notification for FormattedDate
    partial void OnCurrentDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FormattedDate));
    }



    partial void OnTodaySessionChanged(MeditationApp.Models.MeditationSession? oldValue, MeditationApp.Models.MeditationSession? newValue)
    {
        Debug.WriteLine($"[TodaySession] Changed from {(oldValue != null ? $"UUID: {oldValue.Uuid}, Status: {oldValue.Status}, IsDownloaded: {oldValue.IsDownloaded}" : "null")} " +
                       $"to {(newValue != null ? $"UUID: {newValue.Uuid}, Status: {newValue.Status}, IsDownloaded: {newValue.IsDownloaded}" : "null")}");
    }

    private async Task StartPollingSessionStatus(string sessionId)
    {
        if (TodaySession == null) return;

        IsPolling = true;
        PollingStatus = "Checking session status...";

        await _sessionStatusPoller.PollSessionStatusAsync(
            TodaySession,
            async (newStatus, errorMessage) =>
            {
                await UpdateSessionStatus(newStatus, errorMessage);
                if (newStatus == MeditationSessionStatus.COMPLETED || newStatus == MeditationSessionStatus.FAILED)
                {
                    IsPolling = false;
                    PollingStatus = string.Empty;
                }
            }
        );
    }

    private async Task UpdateSessionStatus(MeditationSessionStatus newStatus, string? errorMessage)
    {
        if (TodaySession == null) return;

        Debug.WriteLine($"[UpdateStatus] Updating session {TodaySession.Uuid} status to {newStatus}");

        // Don't update if we're trying to set a lower status (e.g., don't go back to REQUESTED from COMPLETED)
        if (newStatus == MeditationSessionStatus.REQUESTED &&
            (TodaySession.Status == MeditationSessionStatus.COMPLETED ||
             TodaySession.Status == MeditationSessionStatus.FAILED))
        {
            Debug.WriteLine($"[UpdateStatus] Ignoring status change to REQUESTED as current status is {TodaySession.Status}");
            return;
        }

        // Create a new session object to ensure UI update
        var updatedSession = new MeditationSession
        {
            Uuid = TodaySession.Uuid,
            UserID = TodaySession.UserID,
            Timestamp = TodaySession.Timestamp,
            Status = newStatus,
            ErrorMessage = errorMessage,
            AudioPath = TodaySession.AudioPath,
            IsDownloaded = TodaySession.IsDownloaded,
            LocalAudioPath = TodaySession.LocalAudioPath,
            DownloadedAt = TodaySession.DownloadedAt,
            FileSizeBytes = TodaySession.FileSizeBytes
        };

        // Save to database
        await _sessionDatabase.SaveSessionAsync(updatedSession);

        // Update on main thread to ensure UI updates
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            TodaySession = updatedSession;
            Debug.WriteLine($"[UpdateStatus] UI updated with new status: {newStatus}");
        });

        // Auto-download when status changes to COMPLETED
        if (newStatus == MeditationSessionStatus.COMPLETED && !updatedSession.IsDownloaded)
        {
            Debug.WriteLine("[UpdateStatus] Session completed, starting auto-download");
            await DownloadSessionInternal();

            // Notify that new data is available (but don't force refresh to avoid loops)
            Debug.WriteLine("[UpdateStatus] New session completed - calendar will refresh on next navigation");
        }
    }

    public async Task StopPollingSessionStatus()
    {
        Debug.WriteLine("[Polling] Stopping polling session status");
        if (_pollingCts != null)
        {
            Debug.WriteLine("[Polling] Cancelling and disposing polling token");
            await Task.Run(() =>
            {
                _pollingCts.Cancel();
                _pollingCts.Dispose();
                _pollingCts = null;
            });
        }
        IsPolling = false;
        PollingStatus = string.Empty;
        Debug.WriteLine("[Polling] Polling stopped and resources cleaned up");
    }

    private async Task<string> GetCurrentUserId()
    {
        try
        {
            var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
                return attributes.FirstOrDefault(a => a.Name == "sub")?.Value ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting user ID: {ex.Message}");
        }
        return string.Empty;
    }

    private async Task DownloadSessionSilentlyIfInitialLoad()
    {
        if (TodaySession == null) return;

        // If this is the initial load, download silently without UI updates
        if (_isInitialLoad)
        {
            try
            {
                Debug.WriteLine($"[DownloadSilent] Silently downloading session {TodaySession.Uuid} during initial load");

                // Get presigned URL for the session
                var presignedUrl = await _audioDownloadService.GetPresignedUrlAsync(TodaySession.Uuid);
                if (string.IsNullOrEmpty(presignedUrl))
                {
                    Debug.WriteLine("[DownloadSilent] Failed to get presigned URL during silent download");
                    return;
                }

                // Download the audio file without UI updates
                var success = await _audioDownloadService.DownloadSessionAudioAsync(TodaySession, presignedUrl);
                if (success)
                {
                    // Update session in database
                    await _sessionDatabase.SaveSessionAsync(TodaySession);

                    // Trigger property changed to update UI
                    OnPropertyChanged(nameof(TodaySession));

                    Debug.WriteLine($"[DownloadSilent] Successfully downloaded session {TodaySession.Uuid} silently");
                }
                else
                {
                    Debug.WriteLine($"[DownloadSilent] Failed to download session {TodaySession.Uuid} silently");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadSilent] Error during silent download: {ex.Message}");
            }
        }
        else
        {
            // If not initial load, use the regular download method with UI
            await DownloadSessionInternal();
        }
    }

    private async Task SyncAllInsightsAsync()
    {
        try
        {
            Debug.WriteLine("[SyncInsights] Starting full insights sync");
            var userId = await GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("[SyncInsights] Cannot sync insights: No user ID available");
                return;
            }

            string query = await GraphQLQueryLoader.LoadQueryAsync("ListUserDailyInsights.graphql");
            if (string.IsNullOrWhiteSpace(query))
            {
                Debug.WriteLine("[SyncInsights] GraphQL query is empty or failed to load.");
                query = @"query ListUserDailyInsights($userID: ID!) {
                    listUserDailyInsights(userID: $userID) {
                        userID
                        date
                        notes
                        mood
                    }
                }";
            }

            var variables = new { userID = userId };

            Debug.WriteLine("[SyncInsights] Fetching all insights");
            var result = await _graphQLService.QueryAsync(query, variables);

            if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                dataElem.TryGetProperty("listUserDailyInsights", out var insightsElem) &&
                insightsElem.ValueKind == JsonValueKind.Array)
            {
                var insights = insightsElem.EnumerateArray();
                var savedCount = 0;
                var updatedCount = 0;

                foreach (var insightElem in insights)
                {
                    var date = DateTime.Parse(insightElem.GetProperty("date").GetString() ?? string.Empty, null, System.Globalization.DateTimeStyles.RoundtripKind);
                    var notes = insightElem.GetProperty("notes").GetString() ?? string.Empty;
                    int? mood = null;
                    if (insightElem.TryGetProperty("mood", out var moodElem) &&
                        moodElem.ValueKind != JsonValueKind.Null)
                    {
                        mood = moodElem.GetInt32();
                    }

                    // Get existing insight from local DB
                    var existingInsight = await _sessionDatabase.GetDailyInsightsAsync(userId, date);

                    if (existingInsight == null)
                    {
                        // Create new insight
                        var newInsight = new Models.UserDailyInsights
                        {
                            UserID = userId,
                            Date = date,
                            Notes = notes,
                            Mood = mood,
                            IsSynced = true,
                            LastUpdated = DateTime.UtcNow
                        };
                        await _sessionDatabase.SaveDailyInsightsAsync(newInsight);
                        savedCount++;
                    }
                    else if (!existingInsight.IsSynced ||
                             existingInsight.LastUpdated < DateTime.UtcNow.AddMinutes(-5))
                    {
                        // Update existing insight if it's not synced or is older than 5 minutes
                        existingInsight.Notes = notes;
                        existingInsight.Mood = mood;
                        existingInsight.IsSynced = true;
                        existingInsight.LastUpdated = DateTime.UtcNow;
                        await _sessionDatabase.SaveDailyInsightsAsync(existingInsight);
                        updatedCount++;
                    }
                }

                Debug.WriteLine($"[SyncInsights] Sync completed - {savedCount} new insights saved, {updatedCount} existing insights updated");

                // If today's insights were updated, refresh the UI
                if (savedCount > 0 || updatedCount > 0)
                {
                    // Set flag to prevent recursive calls during refresh
                    _isLoadingData = true;
                    try
                    {
                        await LoadLocalInsights();
                    }
                    finally
                    {
                        _isLoadingData = false;
                    }
                }
            }
            else
            {
                Debug.WriteLine("[SyncInsights] No insights data in response or invalid format");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SyncInsights] Error syncing insights: {ex.Message}\nStack trace: {ex.StackTrace}");
        }
    }

    private void StopAudio()
    {
        try
        {
            // if (AudioPlayer != null) { /* commented out */ }
            // if (AudioPlayer.CurrentState == MediaElementState.Playing) { /* commented out */ }
            // AudioPlayer.Stop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping audio: {ex.Message}");
        }
    }

    /// <summary>
    /// Triggers a background sync if conditions are met
    /// </summary>
    public async Task TriggerBackgroundSyncAsync()
    {
        try
        {
            Debug.WriteLine("[TodayViewModel] Triggering background sync");
            var result = await _databaseSyncService.TriggerSyncIfNeededAsync();

            if (result.IsSuccess)
            {
                Debug.WriteLine($"[TodayViewModel] Background sync completed: {result.SessionsUpdated} sessions, {result.InsightsUpdated} insights updated");

                // Refresh local data after successful sync
                await LoadLocalSessionsForToday();
                await LoadLocalInsights();
            }
            else
            {
                Debug.WriteLine($"[TodayViewModel] Background sync skipped or failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TodayViewModel] Error during background sync: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshData()
    {
        Debug.WriteLine("TodayViewModel: RefreshData triggered.");
        await LoadTodayDataAsync();
        await LoadMoodChartDataAsync();
    }

    private async Task LoadMoodChartDataAsync()
    {
        try
        {
            var moodData = await _moodChartService.GetLastSevenDaysMoodDataAsync();

            Debug.WriteLine($"[TodayViewModel] Loaded {moodData.Count} mood data points.");

            MoodData.Clear();
            foreach (var dataPoint in moodData)
            {
                MoodData.Add(dataPoint);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading mood chart data: {ex.Message}");
        }
    }

    // Explicit interface implementations for IAudioPlayerViewModel
    ICommand IAudioPlayerViewModel.TogglePlaybackCommand => TogglePlaybackCommand;
    ICommand IAudioPlayerViewModel.SeekBackwardCommand => SeekBackwardCommand;
    ICommand IAudioPlayerViewModel.SeekForwardCommand => SeekForwardCommand;
    ICommand IAudioPlayerViewModel.HideAudioPlayerSheetCommand => HideAudioPlayerSheetCommand;

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
