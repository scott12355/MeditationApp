using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using MeditationApp.Services;
using MeditationApp.Models;
using System.Diagnostics;

namespace MeditationApp.ViewModels;

public partial class SwipeCalendarViewModel : ObservableObject
{
    private readonly MeditationSessionDatabase _database;
    private readonly CalendarDataService _calendarDataService;
    private readonly CognitoAuthService _cognitoAuthService;

    [ObservableProperty]
    private DateTime _currentMonth = DateTime.Now.Date.AddDays(1 - DateTime.Now.Day); // First day of current month

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private ObservableCollection<MonthlyData> _months = new();

    [ObservableProperty]
    private MonthlyData? _selectedMonth;

    [ObservableProperty]
    private int _selectedMonthIndex = 0;

    public static DayData? SelectedDayData { get; set; }

    public SwipeCalendarViewModel(MeditationSessionDatabase database, CalendarDataService calendarDataService, CognitoAuthService cognitoAuthService)
    {
        _database = database;
        _calendarDataService = calendarDataService;
        _cognitoAuthService = cognitoAuthService;
        
        InitializeMonths();
    }

    /// <summary>
    /// Resets the ViewModel state for logout/login cycles to prevent cross-user data contamination
    /// </summary>
    public void Reset()
    {
        Debug.WriteLine("SwipeCalendarViewModel: Resetting state");
        
        // Clear static selected day data
        SelectedDayData = null;
        
        // Clear months collection
        Months.Clear();
        
        // Reset selected month properties
        SelectedMonth = null;
        SelectedMonthIndex = 0;
        
        // Reset current month to today
        CurrentMonth = DateTime.Now.Date.AddDays(1 - DateTime.Now.Day);
        
        // Reset loading state
        IsLoading = false;
        
        Debug.WriteLine("SwipeCalendarViewModel: State reset completed");
        
        // Reinitialize months for the new user
        InitializeMonths();
    }

    private async void InitializeMonths()
    {
        IsLoading = true;
        try
        {
            // Create months from 6 months ago to 6 months in the future
            var startMonth = CurrentMonth.AddMonths(-6);
            
            for (int i = 0; i < 13; i++)
            {
                var month = startMonth.AddMonths(i);
                var monthData = new MonthlyData
                {
                    Month = month,
                    DisplayName = month.ToString("MMMM yyyy"),
                    Days = new ObservableCollection<DayData>()
                };
                
                Months.Add(monthData);
                
                // Set the current month as selected
                if (month.Year == CurrentMonth.Year && month.Month == CurrentMonth.Month)
                {
                    SelectedMonth = monthData;
                    SelectedMonthIndex = i;
                }
            }

            // Load data for current month first
            if (SelectedMonth != null)
            {
                await LoadMonthData(SelectedMonth);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing months: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MonthChanged(int newIndex)
    {
        if (newIndex >= 0 && newIndex < Months.Count)
        {
            SelectedMonthIndex = newIndex;
            SelectedMonth = Months[newIndex];
            
            if (SelectedMonth != null && !SelectedMonth.IsLoaded)
            {
                await LoadMonthData(SelectedMonth);
            }
        }
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        if (SelectedMonthIndex > 0)
        {
            SelectedMonthIndex--;
        }
    }

    [RelayCommand]
    private void NextMonth()
    {
        if (SelectedMonthIndex < Months.Count - 1)
        {
            SelectedMonthIndex++;
        }
    }

    private async Task LoadMonthData(MonthlyData monthData)
    {
        try
        {
            if (monthData == null)
            {
                Debug.WriteLine("MonthData is null");
                return;
            }

            monthData.IsLoading = true;
            Debug.WriteLine($"Loading data for month: {monthData.DisplayName}");
            
            // Get all sessions for the month
            List<MeditationSession> sessions;
            try
            {
                sessions = await _calendarDataService.GetSessionsForMonthAsync(monthData.Month.Year, monthData.Month.Month);
                Debug.WriteLine($"Found {sessions?.Count ?? 0} sessions for {monthData.DisplayName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting sessions: {ex.Message}");
                sessions = new List<MeditationSession>();
            }
            
            // Get all daily insights for the month
            string userId;
            try
            {
                userId = await GetCurrentUserId();
                Debug.WriteLine($"Current user ID: {userId ?? "null"}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user ID: {ex.Message}");
                userId = string.Empty;
            }
            
            List<UserDailyInsights> insights;
            try
            {
                insights = string.IsNullOrEmpty(userId) ? new List<UserDailyInsights>() : 
                          await _database.GetDailyInsightsForMonthAsync(userId, monthData.Month.Year, monthData.Month.Month);
                Debug.WriteLine($"Found {insights?.Count ?? 0} insights for {monthData.DisplayName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting insights: {ex.Message}");
                insights = new List<UserDailyInsights>();
            }
            
            // Group sessions and insights by day (with null checks)
            var sessionsByDay = (sessions ?? new List<MeditationSession>())
                .GroupBy(s => s.Timestamp.Date)
                .ToDictionary(g => g.Key, g => g.ToList());
            var insightsByDay = (insights ?? new List<UserDailyInsights>())
                .GroupBy(i => i.Date.Date)
                .ToDictionary(g => g.Key, g => g.First());
            
            // Get all days in the month up to today
            var daysInMonth = DateTime.DaysInMonth(monthData.Month.Year, monthData.Month.Month);
            var today = DateTime.Today;
            var monthStart = new DateTime(monthData.Month.Year, monthData.Month.Month, 1);
            var monthEnd = new DateTime(monthData.Month.Year, monthData.Month.Month, daysInMonth);
            
            // Only show days up to today or the last day of the month if it's a past month
            var lastDayToShow = monthEnd <= today ? daysInMonth : 
                               (monthStart.Year == today.Year && monthStart.Month == today.Month) ? today.Day : 0;
            
            monthData.Days.Clear();
            
            int daysWithData = 0;
            var dayDataList = new List<DayData>();
            
            // Create day data for all days up to the last day to show
            for (int day = 1; day <= lastDayToShow; day++)
            {
                var date = new DateTime(monthData.Month.Year, monthData.Month.Month, day);
                var daySessions = sessionsByDay.ContainsKey(date) ? sessionsByDay[date] : new List<MeditationSession>();
                var dayInsights = insightsByDay.ContainsKey(date) ? insightsByDay[date] : null;
                
                var hasData = daySessions.Any() || !string.IsNullOrEmpty(dayInsights?.Notes);
                if (hasData) daysWithData++;
                
                var dayData = new DayData
                {
                    Date = date,
                    DisplayDate = $"{date:dddd, MMMM d}",
                    Sessions = new ObservableCollection<MeditationSession>(daySessions),
                    Notes = dayInsights?.Notes ?? string.Empty,
                    Mood = dayInsights?.Mood,
                    HasData = hasData
                };
                
                dayDataList.Add(dayData);
            }
            
            // Sort days in descending order (most recent first) and add to collection
            foreach (var dayData in dayDataList.OrderByDescending(d => d.Date))
            {
                monthData.Days.Add(dayData);
            }
            
            Debug.WriteLine($"Created {monthData.Days.Count} day entries, {daysWithData} have data");
            monthData.IsLoaded = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading month data: {ex.Message}");
        }
        finally
        {
            monthData.IsLoading = false;
        }
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

    [RelayCommand]
    private async Task NavigateToDay(DayData dayData)
    {
        SelectedDayData = dayData;
        await Shell.Current.GoToAsync("DayDetailPage");
    }
}

public class MonthlyData : INotifyPropertyChanged
{
    private bool _isLoading;
    private bool _isLoaded;

    public DateTime Month { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public ObservableCollection<DayData> Days { get; set; } = new();

    public bool IsLoading 
    { 
        get => _isLoading; 
        set { _isLoading = value; OnPropertyChanged(); } 
    }

    public bool IsLoaded 
    { 
        get => _isLoaded; 
        set { _isLoaded = value; OnPropertyChanged(); } 
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DayData
{
    public DateTime Date { get; set; }
    public string DisplayDate { get; set; } = string.Empty;
    public ObservableCollection<MeditationSession> Sessions { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public int? Mood { get; set; }
    public bool HasData { get; set; }
    
    public string MoodEmoji => Mood switch
    {
        1 => "😢",
        2 => "😕", 
        3 => "😐",
        4 => "😊",
        5 => "😄",
        _ => ""
    };
    
    public string SessionSummary => Sessions.Count switch
    {
        0 => "No sessions",
        1 => "1 session",
        _ => $"{Sessions.Count} sessions"
    };
}
