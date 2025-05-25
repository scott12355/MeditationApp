using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MeditationApp.ViewModels;

public class DayDetailViewModel : INotifyPropertyChanged
{
    private DateTime _selectedDate;

    public DayDetailViewModel(DateTime selectedDate)
    {
        _selectedDate = selectedDate;
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

    public ICommand AddSessionCommand { get; }

    private void LoadDayData()
    {
        // TODO: Load actual data from service
        // For now, using sample data
        MeditationSessions.Clear();

        // Sample data - replace with actual data loading
        if (_selectedDate.Date == DateTime.Today)
        {
            MeditationSessions.Add(new MeditationSessionModel
            {
                Type = "Morning Meditation",
                Time = new TimeSpan(7, 30, 0),
                Duration = 15,
                Status = "Completed"
            });
            
            MeditationSessions.Add(new MeditationSessionModel
            {
                Type = "Lunch Break Mindfulness",
                Time = new TimeSpan(12, 15, 0),
                Duration = 10,
                Status = "Completed"
            });

            DayNotes = "Felt very centered today. Morning session was particularly peaceful.";
        }
        else if (_selectedDate.Date == DateTime.Today.AddDays(-1))
        {
            MeditationSessions.Add(new MeditationSessionModel
            {
                Type = "Evening Relaxation",
                Time = new TimeSpan(20, 0, 0),
                Duration = 20,
                Status = "Completed"
            });

            DayNotes = "Had a stressful day, evening meditation helped me unwind.";
        }

        // Calculate totals
        TotalMeditationTime = MeditationSessions.Sum(s => s.Duration);
        SessionCount = MeditationSessions.Count;

        OnPropertyChanged(nameof(TotalMeditationTime));
        OnPropertyChanged(nameof(SessionCount));
        OnPropertyChanged(nameof(DayNotes));
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
