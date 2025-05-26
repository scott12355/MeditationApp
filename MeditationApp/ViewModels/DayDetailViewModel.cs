using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MeditationApp.Services;
using MeditationApp.Models;

namespace MeditationApp.ViewModels;

public class DayDetailViewModel : INotifyPropertyChanged
{
    private DateTime _selectedDate;
    private readonly MeditationSessionDatabase _database;
    private bool _isLoading;

    public DayDetailViewModel(DateTime selectedDate, MeditationSessionDatabase database)
    {
        _selectedDate = selectedDate;
        _database = database;
        AddSessionCommand = new Command(OnAddSession);
        LoadDayData();
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            _selectedDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageTitle));
            LoadDayData();
        }
    }

    public string PageTitle => $"{_selectedDate:dddd, MMMM d}";

    public ObservableCollection<MeditationSessionModel> MeditationSessions { get; } = new();

    public int TotalMeditationTime { get; private set; }
    public int SessionCount { get; private set; }
    public string DayNotes { get; private set; } = string.Empty;

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

    private async void LoadDayData()
    {
        IsLoading = true;
        try
        {
            MeditationSessions.Clear();
            
            // Add a small delay to show loading state (remove in production if not needed)
            await Task.Delay(400);
            
            var sessionsForDate = await _database.GetSessionsForDateAsync(_selectedDate);

            foreach (var session in sessionsForDate)
            {
                MeditationSessions.Add(new MeditationSessionModel
                {
                    Type = !string.IsNullOrEmpty(session.AudioPath) ? Path.GetFileNameWithoutExtension(session.AudioPath) : "Meditation",
                    Time = session.Timestamp.TimeOfDay,
                    Duration = 15, // Default duration since it's not in the database model
                    Status = session.Status == "completed" ? "Completed" : "Incomplete"
                });
            }

            // Calculate totals
            TotalMeditationTime = MeditationSessions.Sum(s => s.Duration);
            SessionCount = MeditationSessions.Count;
            DayNotes = ""; // You could add notes to the database model if needed

            OnPropertyChanged(nameof(TotalMeditationTime));
            OnPropertyChanged(nameof(SessionCount));
            OnPropertyChanged(nameof(DayNotes));
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
        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert("Add Session", 
                $"Add meditation session for {_selectedDate:MMMM d, yyyy}", "OK");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class MeditationSessionModel
{
    public string Type { get; set; } = string.Empty;
    public TimeSpan Time { get; set; }
    public int Duration { get; set; }
    public string Status { get; set; } = string.Empty;
}
