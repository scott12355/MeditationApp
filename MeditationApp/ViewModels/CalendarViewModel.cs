using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MeditationApp.Services;
using MeditationApp.Models;

namespace MeditationApp.ViewModels;

public class CalendarViewModel : INotifyPropertyChanged
{
    private DateTime _selectedDate = DateTime.Now;
    private ObservableCollection<MeditationSessionDisplay> _sessions = new();
    private readonly MeditationSessionDatabase _database;
    private int _totalSessions;
    private int _totalMinutes;
    private bool _isLoading;
    private bool _isLoadingStats;

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            _selectedDate = value;
            OnPropertyChanged();
            LoadSessionsForDate();
        }
    }

    public ObservableCollection<MeditationSessionDisplay> Sessions
    {
        get => _sessions;
        set
        {
            _sessions = value;
            OnPropertyChanged();
        }
    }

    public int TotalSessions
    {
        get => _totalSessions;
        set
        {
            _totalSessions = value;
            OnPropertyChanged();
        }
    }

    public int TotalMinutes
    {
        get => _totalMinutes;
        set
        {
            _totalMinutes = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoadingStats
    {
        get => _isLoadingStats;
        set
        {
            _isLoadingStats = value;
            OnPropertyChanged();
        }
    }

    public ICommand DateSelectedCommand { get; }

    public CalendarViewModel(MeditationSessionDatabase database)
    {
        _database = database;
        DateSelectedCommand = new Command<DateTime>(OnDateSelected);
        
        // Initialize with sample data and then load actual data
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        await _database.AddSampleDataAsync();
        LoadSessionsForDate();
        LoadMonthlyStats();
    }

    private void OnDateSelected(DateTime date)
    {
        SelectedDate = date;
    }

    private async void LoadSessionsForDate()
    {
        IsLoading = true;
        try
        {
            Sessions.Clear();
            
            // Add a small delay to show loading state (remove in production if not needed)
            await Task.Delay(300);
            
            var sessionsForDate = await _database.GetSessionsForDateAsync(SelectedDate);
            
            foreach (var session in sessionsForDate)
            {
                Sessions.Add(new MeditationSessionDisplay
                {
                    Date = session.Timestamp,
                    Duration = TimeSpan.FromMinutes(15), // Default duration since it's not in the model
                    Type = !string.IsNullOrEmpty(session.AudioPath) ? Path.GetFileNameWithoutExtension(session.AudioPath) : "Meditation",
                    Completed = session.Status == "completed"
                });
            }
        }
        catch (Exception ex)
        {
            // Log error or handle appropriately
            System.Diagnostics.Debug.WriteLine($"Error loading sessions: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void LoadMonthlyStats()
    {
        IsLoadingStats = true;
        try
        {
            // Add a small delay to show loading state (remove in production if not needed)
            await Task.Delay(500);
            
            var currentMonth = DateTime.Now;
            var monthSessions = await _database.GetSessionsForMonthAsync(currentMonth.Year, currentMonth.Month);
            
            TotalSessions = monthSessions.Count;
            // For now, assume each session is about 15 minutes (you may want to add duration to the model)
            TotalMinutes = monthSessions.Count * 15; // Placeholder calculation
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading monthly stats: {ex.Message}");
            TotalSessions = 0;
            TotalMinutes = 0;
        }
        finally
        {
            IsLoadingStats = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class MeditationSessionDisplay
{
    public DateTime Date { get; set; }
    public TimeSpan Duration { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool Completed { get; set; }
    
    public string FormattedDuration => $"{Duration.TotalMinutes:0} min";
    public string CompletedText => Completed ? "Completed" : "Incomplete";
}
