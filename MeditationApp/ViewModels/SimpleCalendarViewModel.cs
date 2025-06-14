using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MeditationApp.Services;
using MeditationApp.Models;
using System.Diagnostics;
using System.Linq;

namespace MeditationApp.ViewModels;

public partial class SimpleCalendarViewModel : ObservableObject
{
    private readonly MeditationSessionDatabase _database;
    private readonly CalendarDataService _calendarDataService;
    private readonly CognitoAuthService _cognitoAuthService;
    private readonly DatabaseSyncService _databaseSyncService;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private ObservableCollection<DaySessionData> _daysWithSessions = new();

    public static DayData? SelectedDayData { get; set; }

    public SimpleCalendarViewModel(MeditationSessionDatabase database, CalendarDataService calendarDataService, CognitoAuthService cognitoAuthService, DatabaseSyncService databaseSyncService)
    {
        _database = database;
        _calendarDataService = calendarDataService;
        _cognitoAuthService = cognitoAuthService;
        _databaseSyncService = databaseSyncService;
    }

    /// <summary>
    /// Load all days that have meditation sessions, ordered by date (most recent first)
    /// </summary>
    [RelayCommand]
    public async Task LoadSessionDays()
    {
        IsLoading = true;
        try
        {
            Debug.WriteLine("SimpleCalendarViewModel: Loading all session days");
            
            // Trigger background sync for fresh data (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await TriggerCalendarSyncAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SimpleCalendarViewModel] Background sync failed: {ex.Message}");
                }
            });
            
            // Get all sessions from the database
            var allSessions = await _database.GetSessionsAsync();
            Debug.WriteLine($"SimpleCalendarViewModel: Found {allSessions.Count} total sessions");
            
            // Get user ID for insights
            var userId = await GetCurrentUserId();
            
            // Group sessions by date
            var sessionsByDate = allSessions
                .GroupBy(s => s.Timestamp.Date)
                .OrderByDescending(g => g.Key) // Most recent first
                .ToList();
            
            Debug.WriteLine($"SimpleCalendarViewModel: Grouped into {sessionsByDate.Count} unique dates");
            
            DaysWithSessions.Clear();
            
            foreach (var dateGroup in sessionsByDate)
            {
                var date = dateGroup.Key;
                var sessions = dateGroup.ToList();
                
                // Get insights for this date
                UserDailyInsights? insights = null;
                if (!string.IsNullOrEmpty(userId))
                {
                    insights = await _database.GetDailyInsightsAsync(userId, date);
                }
                
                var dayData = new DaySessionData
                {
                    Date = date,
                    Sessions = new ObservableCollection<MeditationSession>(sessions),
                    Notes = insights?.Notes ?? string.Empty,
                    Mood = insights?.Mood
                };
                
                DaysWithSessions.Add(dayData);
            }
            
            Debug.WriteLine($"SimpleCalendarViewModel: Created {DaysWithSessions.Count} day entries");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SimpleCalendarViewModel: Error loading session days: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToDay(DaySessionData dayData)
    {
        // Set the day data for the detail page
        SelectedDayData = new DayData
        {
            Date = dayData.Date,
            DisplayDate = dayData.DisplayDate,
            Sessions = dayData.Sessions,
            Notes = dayData.Notes,
            Mood = dayData.Mood,
            HasData = true
        };
        
        await Shell.Current.GoToAsync("DayDetailPage");
    }

    /// <summary>
    /// Reset the ViewModel state for logout/login cycles
    /// </summary>
    public void Reset()
    {
        Debug.WriteLine("SimpleCalendarViewModel: Resetting state");
        DaysWithSessions.Clear();
        IsLoading = false;
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
    /// Triggers a background sync when loading calendar data
    /// </summary>
    private async Task TriggerCalendarSyncAsync()
    {
        try
        {
            Debug.WriteLine("[SimpleCalendarViewModel] Triggering background sync for calendar data");
            var result = await _databaseSyncService.TriggerSyncIfNeededAsync();
            
            if (result.IsSuccess && (result.SessionsUpdated > 0 || result.InsightsUpdated > 0))
            {
                Debug.WriteLine($"[SimpleCalendarViewModel] Background sync completed with updates: {result.SessionsUpdated} sessions, {result.InsightsUpdated} insights");
                // Don't call LoadSessionDays here to prevent infinite loops
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SimpleCalendarViewModel] Error during background sync: {ex.Message}");
        }
    }
}

public class DaySessionData
{
    public DateTime Date { get; set; }
    public ObservableCollection<MeditationSession> Sessions { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public int? Mood { get; set; }
    
    public string DisplayDate => Date.ToString("dddd, MMMM d, yyyy");
    
    public string SessionCount => Sessions.Count switch
    {
        0 => "No sessions",
        1 => "1 session",
        _ => $"{Sessions.Count} sessions"
    };
    
    public string MoodEmoji => Mood switch
    {
        1 => "😢",
        2 => "😕", 
        3 => "😐",
        4 => "😊",
        5 => "😄",
        _ => ""
    };
    
    public bool HasNotes => !string.IsNullOrEmpty(Notes);
    public bool HasMood => Mood.HasValue;
    
    public string RelativeDate
    {
        get
        {
            var today = DateTime.Today;
            var daysDiff = (today - Date.Date).Days;
            
            return daysDiff switch
            {
                0 => "Today",
                1 => "Yesterday",
                _ when daysDiff <= 7 => $"{daysDiff} days ago",
                _ when daysDiff <= 30 => $"{daysDiff / 7} week{(daysDiff / 7 > 1 ? "s" : "")} ago",
                _ => Date.ToString("MMM d, yyyy")
            };
        }
    }
}
