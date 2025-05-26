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

    public string FormattedDate => CurrentDate.ToString("dddd, MMMM dd, yyyy");

    private readonly GraphQLService _graphQLService;
    private readonly CognitoAuthService _cognitoAuthService;
    private readonly MeditationSessionDatabase _sessionDatabase;

    public TodayViewModel(GraphQLService graphQLService, CognitoAuthService cognitoAuthService, MeditationSessionDatabase sessionDatabase)
    {
        _graphQLService = graphQLService;
        _cognitoAuthService = cognitoAuthService;
        _sessionDatabase = sessionDatabase;
        LoadTodayData();
    }

    [RelayCommand]
    private async Task StartMeditation()
    {
        // TODO: Navigate to meditation session
        Console.WriteLine("Starting meditation session...");
        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert("Meditation", "Starting your meditation session...", "OK");
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
            return;
        var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
        var userId = attributes.FirstOrDefault(a => a.Name == "sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;
        var allSessions = await _sessionDatabase.GetSessionsAsync();
        var today = DateTime.Today;
        var sessions = allSessions.Where(s => s.UserID == userId && s.Timestamp.Date == today).ToList();
        // var sessions = allSessions;
        Debug.WriteLine($"Loaded {sessions.Count} sessions for today from local DB for user {userId}.");
        TodaySessions = new ObservableCollection<MeditationApp.Models.MeditationSession>(sessions);
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

    private async void LoadTodayData()
    {
        // check internet connectivity
        if (Microsoft.Maui.Networking.Connectivity.NetworkAccess != Microsoft.Maui.Networking.NetworkAccess.Internet)
        {
            Debug.WriteLine("No internet connection, loading local data only.");
            await LoadSessionsForToday();
            return;
        }

        try
        {
            // Get the current user's Cognito ID
            var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
                return;
            var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
            var userId = attributes.FirstOrDefault(a => a.Name == "sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return;

            // Load the GraphQL query from MauiAsset
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
            // Parse and save sessions to local DB
            if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                dataElem.TryGetProperty("listUserMeditationSessions", out var sessionsElem) &&
                sessionsElem.ValueKind == JsonValueKind.Array)
            {
                Debug.WriteLine($"Found {sessionsElem.GetArrayLength()} sessions from GraphQL.");
                // Clear all local sessions before saving new ones from GraphQL
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
                                    // Handle scientific notation (double)
                                    double millisDouble = tsElem.GetDouble();
                                    millis = Convert.ToInt64(millisDouble);
                                    timestampVal = DateTimeOffset.FromUnixTimeMilliseconds(millis).DateTime;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Invalid timestamp value (number): {tsElem} - {ex.Message}");
                                continue; // skip this session
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Invalid timestamp value kind: {tsElem.ValueKind}, value: {tsElem}");
                            continue; // skip this session
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Session missing 'timestamp' property, skipping.");
                        continue; // skip this session
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
            Console.WriteLine($"Error loading today's data: {ex.Message}");
        }
    }

    // You might need to add this property changed notification for FormattedDate
    partial void OnCurrentDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FormattedDate));
    }
}
