using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeditationApp.Services;
using MeditationApp.Models;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;

namespace MeditationApp.ViewModels;

public partial class TodayViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _currentDate = DateTime.Now;

    [ObservableProperty]
    private int? _selectedMood = null; // Default to neutral mood

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
    private bool _isLoading = false; // Start with false to prevent initial loading state

    // Track if this is the initial load to prevent UI flicker
    private bool _isInitialLoad = true;

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

    private Models.UserDailyInsights? _currentInsights;

    public string FormattedDate => CurrentDate.ToString("dddd, MMMM dd, yyyy");
    
    public string CurrentPositionText => PlaybackPosition.ToString(@"mm\:ss");
    
    public string TotalDurationText => PlaybackDuration.ToString(@"mm\:ss");

    private readonly GraphQLService _graphQLService;
    private readonly CognitoAuthService _cognitoAuthService;
    private readonly MeditationSessionDatabase _sessionDatabase;
    private readonly IAudioService _audioService;
    private Task? _initializationTask;
    private CancellationTokenSource? _pollingCts;
    private const int POLLING_INTERVAL_MS = 5000; // Poll every 5 seconds
    private const int MAX_POLLING_DURATION_MS = 300000; // Stop polling after 5 minutes
    
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
            Debug.WriteLine("[Init] Starting initial data load");
            // Only show loading state if this is not the initial load from splash screen
            if (!_isInitialLoad)
            {
                IsLoading = true;
            }
            await LoadTodayData();
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

                var fileInfo = new FileInfo(TodaySession.LocalAudioPath!);
                Debug.WriteLine($"TogglePlayback: Attempting to play audio file: {TodaySession.LocalAudioPath} (Size: {fileInfo.Length} bytes, Last modified: {fileInfo.LastWriteTime})");

                // Verify file is readable
                try
                {
                    using (var stream = File.OpenRead(TodaySession.LocalAudioPath!))
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

                // Trigger property changed to update UI - no need to reload from DB
                // The TodaySession object already has the correct download status set by AudioService
                OnPropertyChanged(nameof(TodaySession));

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

    // MediaElement event handlers called from the View
    public void SetMediaElement(MediaElement player)
    {
        AudioPlayer = player;
        AudioPlayer.MediaOpened += OnMediaOpened;
        AudioPlayer.MediaEnded += OnMediaEnded;
        AudioPlayer.MediaFailed += OnMediaFailed;
        AudioPlayer.PositionChanged += OnPositionChanged;
        AudioPlayer.StateChanged += OnStateChanged;
    }

    public void CleanupMediaElement()
    {
        if (AudioPlayer != null)
        {
            AudioPlayer.MediaOpened -= OnMediaOpened;
            AudioPlayer.MediaEnded -= OnMediaEnded;
            AudioPlayer.MediaFailed -= OnMediaFailed;
            AudioPlayer.PositionChanged -= OnPositionChanged;
            AudioPlayer.StateChanged -= OnStateChanged;
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
        PlaybackPosition = TimeSpan.Zero;
        PlaybackProgress = 0;
        OnPropertyChanged(nameof(CurrentPositionText));
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        if (AudioPlayer == null) return;
        Debug.WriteLine($"OnMediaFailed: Error = {e.ErrorMessage}, State = {AudioPlayer.CurrentState}");
        IsPlaying = false;
        
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

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        if (AudioPlayer == null) return;
        Debug.WriteLine($"OnStateChanged: New state = {e.NewState}");
        
        // Update IsPlaying based on the new state
        IsPlaying = e.NewState == MediaElementState.Playing;
    }

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
                string mutation = await LoadGraphQLQueryFromAssetAsync("CreateMeditationSession.graphql");
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
                    timestampMillis).DateTime;

                var status = ParseSessionStatus(sessionElem.GetProperty("status").GetString() ?? MeditationSessionStatus.REQUESTED.ToString());
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
        if (int.TryParse(mood, out int moodValue))
        {
            SelectedMood = moodValue;
            await SaveUserDailyInsights();
        }
    }

    partial void OnSessionNotesChanged(string value)
    {
        Debug.WriteLine($"[Notes] Notes changed to: {value}");
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
                    string mutation = await LoadGraphQLQueryFromAssetAsync("AddUserDailyInsights.graphql");
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

    private async Task LoadTodayData()
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

            // Start both syncs in parallel
            var sessionsTask = LoadSessionsAsync(userId);
            var insightsTask = LoadDailyInsightsAsync(userId);
            var fullSyncTask = SyncAllInsightsAsync();
            
            // Wait for today's data first
            await Task.WhenAll(sessionsTask, insightsTask);
            
            // Process today's insights result
            var insightsResult = await insightsTask;
            if (insightsResult.RootElement.TryGetProperty("data", out var insightsData) &&
                insightsData.TryGetProperty("listUserDailyInsights", out var insightsElem) &&
                insightsElem.ValueKind == JsonValueKind.Array)
            {
                var today = DateTime.Now.Date;
                var todayInsight = insightsElem.EnumerateArray()
                    .FirstOrDefault(i => DateTime.Parse(i.GetProperty("date").GetString() ?? string.Empty).Date == today);

                if (todayInsight.ValueKind != JsonValueKind.Undefined)
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
        string query = await LoadGraphQLQueryFromAssetAsync("ListUserDailyInsights.graphql");
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoadLocal] Error loading local insights: {ex.Message}");
        }
    }

    private async Task<JsonDocument> LoadSessionsAsync(string userId)
    {
        string query = await LoadGraphQLQueryFromAssetAsync("ListUserMeditationSessions.graphql");
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
                    Status = sessionElem.TryGetProperty("status", out var statusElem) ? 
                        ParseSessionStatus(statusElem.GetString() ?? MeditationSessionStatus.REQUESTED.ToString()) : 
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

    partial void OnTodaySessionChanged(MeditationApp.Models.MeditationSession? oldValue, MeditationApp.Models.MeditationSession? newValue)
    {
        Debug.WriteLine($"[TodaySession] Changed from {(oldValue != null ? $"UUID: {oldValue.Uuid}, Status: {oldValue.Status}, IsDownloaded: {oldValue.IsDownloaded}" : "null")} " +
                       $"to {(newValue != null ? $"UUID: {newValue.Uuid}, Status: {newValue.Status}, IsDownloaded: {newValue.IsDownloaded}" : "null")}");
    }

    private async Task StartPollingSessionStatus(string sessionId)
    {
        Debug.WriteLine($"[Polling] Starting polling for session {sessionId}");
        if (_pollingCts != null)
        {
            Debug.WriteLine("[Polling] Existing polling in progress, stopping it first");
            await StopPollingSessionStatus();
        }

        // Don't start polling if session is already completed or failed
        if (TodaySession != null && 
            (TodaySession.Status == MeditationSessionStatus.COMPLETED || 
             TodaySession.Status == MeditationSessionStatus.FAILED))
        {
            Debug.WriteLine($"[Polling] Not starting polling as session is already in {TodaySession.Status} state");
            return;
        }

        _pollingCts = new CancellationTokenSource();
        IsPolling = true;
        PollingStatus = "Checking session status...";
        Debug.WriteLine("[Polling] Polling initialized and started");

        try
        {
            var startTime = DateTime.UtcNow;
            var pollCount = 0;
            Debug.WriteLine($"[Polling] Starting polling loop at {startTime:HH:mm:ss.fff}");
            
            while (!_pollingCts.Token.IsCancellationRequested)
            {
                pollCount++;
                var elapsedTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                Debug.WriteLine($"[Polling] Poll #{pollCount} at {DateTime.UtcNow:HH:mm:ss.fff} (Elapsed: {elapsedTime:F0}ms)");

                // Don't continue polling if session is already completed or failed
                if (TodaySession != null && 
                    (TodaySession.Status == MeditationSessionStatus.COMPLETED || 
                     TodaySession.Status == MeditationSessionStatus.FAILED))
                {
                    Debug.WriteLine($"[Polling] Stopping polling as session is in {TodaySession.Status} state");
                    await StopPollingSessionStatus();
                    break;
                }

                if (elapsedTime > MAX_POLLING_DURATION_MS)
                {
                    Debug.WriteLine($"[Polling] Polling timeout reached after {pollCount} polls ({elapsedTime:F0}ms)");
                    await StopPollingSessionStatus();
                    if (TodaySession != null)
                    {
                        Debug.WriteLine("[Polling] Setting session status to FAILED due to timeout");
                        await UpdateSessionStatus(MeditationSessionStatus.FAILED, "Session generation timed out. Please try again.");
                    }
                    break;
                }

                try
                {
                    Debug.WriteLine("[Polling] Querying backend for session status");
                    string query = await LoadGraphQLQueryFromAssetAsync("GetMeditationSessionStatus.graphql");
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        Debug.WriteLine("[Polling] Using default GraphQL query");
                        query = "query GetMeditationSessionStatus($sessionID: ID!) { getMeditationSessionStatus(sessionID: $sessionID) { status errorMessage } }";
                    }

                    var variables = new { sessionID = sessionId };
                    var result = await _graphQLService.QueryAsync(query, variables);
                    Debug.WriteLine($"[Polling] Backend response received: {result.RootElement}");

                    if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                        dataElem.TryGetProperty("getMeditationSessionStatus", out var statusElem))
                    {
                        var status = statusElem.GetProperty("status").GetString();
                        var errorMessage = statusElem.GetProperty("errorMessage").GetString();
                        Debug.WriteLine($"[Polling] Session status: {status}, Error: {errorMessage ?? "none"}");

                        if (TodaySession != null)
                        {
                            if (Enum.TryParse<MeditationSessionStatus>(status, out var newStatus))
                            {
                                var oldStatus = TodaySession.Status;
                                Debug.WriteLine($"[Polling] Status changed from {oldStatus} to {newStatus}");

                                if (newStatus == MeditationSessionStatus.FAILED)
                                {
                                    Debug.WriteLine($"[Polling] Session failed with error: {errorMessage}");
                                    await UpdateSessionStatus(newStatus, errorMessage);
                                }
                                else if (newStatus != MeditationSessionStatus.REQUESTED || oldStatus == MeditationSessionStatus.REQUESTED)
                                {
                                    // Only update if not trying to go back to REQUESTED from a higher status
                                    await UpdateSessionStatus(newStatus, null);
                                }

                                // Stop polling if session is complete or failed
                                if (newStatus == MeditationSessionStatus.COMPLETED || 
                                    newStatus == MeditationSessionStatus.FAILED)
                                {
                                    Debug.WriteLine($"[Polling] Stopping polling due to final status: {newStatus}");
                                    await StopPollingSessionStatus();
                                    break;
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[Polling] Failed to parse status: {status}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[Polling] TodaySession is null, cannot update status");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[Polling] Invalid response format from backend");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Polling] Error polling session status: {ex.Message}\nStack trace: {ex.StackTrace}");
                    // Don't stop polling on temporary errors
                }

                Debug.WriteLine($"[Polling] Waiting {POLLING_INTERVAL_MS}ms before next poll");
                await Task.Delay(POLLING_INTERVAL_MS, _pollingCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[Polling] Polling cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Polling] Error in polling loop: {ex.Message}\nStack trace: {ex.StackTrace}");
            if (TodaySession != null)
            {
                Debug.WriteLine("[Polling] Setting session status to FAILED due to polling error");
                await UpdateSessionStatus(MeditationSessionStatus.FAILED, "Error checking session status. Please try again.");
            }
        }
        finally
        {
            Debug.WriteLine("[Polling] Cleaning up polling resources");
            await StopPollingSessionStatus();
        }
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
            await DownloadSessionInternal(false);
        }
    }

    public async Task StopPollingSessionStatus()
    {
        Debug.WriteLine("[Polling] Stopping polling session status");
        if (_pollingCts != null)
        {
            Debug.WriteLine("[Polling] Cancelling and disposing polling token");
            await Task.Run(() => {
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

    /// <summary>
    /// Helper method to parse session status, handling legacy values
    /// </summary>
    private static MeditationSessionStatus ParseSessionStatus(string status)
    {
        // Handle legacy status values
        status = status.ToUpperInvariant();
        return status switch
        {
            "COMPLETED" => MeditationSessionStatus.COMPLETED,
            "FAILED" => MeditationSessionStatus.FAILED,
            "REQUESTED" => MeditationSessionStatus.REQUESTED, 
        };
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
                var presignedUrl = await _audioService.GetPresignedUrlAsync(TodaySession.Uuid);
                if (string.IsNullOrEmpty(presignedUrl))
                {
                    Debug.WriteLine("[DownloadSilent] Failed to get presigned URL during silent download");
                    return;
                }

                // Download the audio file without UI updates
                var success = await _audioService.DownloadSessionAudioAsync(TodaySession, presignedUrl);
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
            await DownloadSessionInternal(false);
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
            
            string query = await LoadGraphQLQueryFromAssetAsync("ListUserDailyInsights.graphql");
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
                    var date = DateTime.Parse(insightElem.GetProperty("date").GetString() ?? string.Empty);
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
                    await LoadLocalInsights();
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
}
