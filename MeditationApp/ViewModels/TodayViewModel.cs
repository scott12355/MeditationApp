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

    public string FormattedDate => CurrentDate.ToString("dddd, MMMM dd, yyyy");

    private readonly GraphQLService _graphQLService;
    private readonly CognitoAuthService _cognitoAuthService;
    private readonly MeditationSessionDatabase _sessionDatabase;
    
    // Token refresh management
    private static DateTime _lastRefreshAttempt = DateTime.MinValue;
    private static int _refreshAttemptCount = 0;
    private const int MAX_REFRESH_ATTEMPTS = 3;
    private static readonly TimeSpan REFRESH_COOLDOWN = TimeSpan.FromMinutes(1);

    public TodayViewModel(GraphQLService graphQLService, CognitoAuthService cognitoAuthService, MeditationSessionDatabase sessionDatabase)
    {
        _graphQLService = graphQLService;
        _cognitoAuthService = cognitoAuthService;
        _sessionDatabase = sessionDatabase;
        _ = LoadTodayData(); // Fire and forget - async initialization
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
    private async Task PlayTodaySession()
    {
        if (TodaySession == null) return;
        
        // TODO: Navigate to meditation player with the session
        Console.WriteLine($"Playing today's session: {TodaySession.AudioPath}");
        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert("Meditation", $"Playing session from {TodaySession.Timestamp:HH:mm}", "OK");
    }

    [RelayCommand]
    private Task RequestNewSession()
    {
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
            if (Application.Current?.MainPage != null)
                await Application.Current.MainPage.DisplayAlert("Meditation", $"Selected mood: {moodValue}", "OK");
        }
    }

    [RelayCommand]
    private async Task LoadSessionsForToday()
    {
        var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            await LoadLocalSessionsForToday();
            return;
        }
        try
        {
            var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
            var userId = attributes.FirstOrDefault(a => a.Name == "sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                await LoadLocalSessionsForToday();
                return;
            }
            var allSessions = await _sessionDatabase.GetSessionsAsync();
            var today = DateTime.Today;
            var sessions = allSessions.Where(s => s.UserID == userId && s.Timestamp.Date == today).ToList();
            TodaySessions = new ObservableCollection<MeditationApp.Models.MeditationSession>(sessions);
            TodaySession = sessions.FirstOrDefault();
            HasTodaySession = TodaySession != null;
        }
        catch (Exception ex)
        {
            if (IsTokenRelatedError(ex))
            {
                // Try to refresh token
                if (await TryRefreshTokenAsync())
                {
                    // Retry loading sessions after successful token refresh
                    await LoadSessionsForToday();
                    return;
                }
            }
            // Fallback to local
            await LoadLocalSessionsForToday();
        }
    }

    private async Task LoadLocalSessionsForToday()
    {
        var allSessions = await _sessionDatabase.GetSessionsAsync();
        var today = DateTime.Today;
        var sessions = allSessions.Where(s => s.Timestamp.Date == today).ToList();
        TodaySessions = new ObservableCollection<MeditationApp.Models.MeditationSession>(sessions);
        TodaySession = sessions.FirstOrDefault();
        HasTodaySession = TodaySession != null;
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
                    var session = new MeditationApp.Models.MeditationSession
                    {
                        Uuid = sessionElem.TryGetProperty("sessionID", out var idElem) ? idElem.GetString() ?? string.Empty : string.Empty,
                        UserID = sessionElem.TryGetProperty("userID", out var userElem) ? userElem.GetString() ?? string.Empty : string.Empty,
                        Timestamp = timestampVal,
                        AudioPath = sessionElem.TryGetProperty("audioPath", out var audioElem) ? audioElem.GetString() ?? string.Empty : string.Empty,
                        Status = sessionElem.TryGetProperty("status", out var statusElem) ? statusElem.GetString() ?? string.Empty : string.Empty
                    };
                    await _sessionDatabase.SaveSessionAsync(session);
                    savedCount++;
                }
                Debug.WriteLine($"Saved {savedCount} sessions to local DB.");
            }
            await LoadSessionsForToday();
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
                    if (Application.Current?.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Session Expired", 
                            "Your session has expired. Please log in again.", 
                            "OK");
                    }
                    
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
}
