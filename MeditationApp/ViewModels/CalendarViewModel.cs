using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MeditationApp.ViewModels;

public class CalendarViewModel : INotifyPropertyChanged
{
    private DateTime _selectedDate = DateTime.Now;
    private ObservableCollection<MeditationSession> _sessions = new();

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

    public ObservableCollection<MeditationSession> Sessions
    {
        get => _sessions;
        set
        {
            _sessions = value;
            OnPropertyChanged();
        }
    }

    public ICommand DateSelectedCommand { get; }

    public CalendarViewModel()
    {
        DateSelectedCommand = new Command<DateTime>(OnDateSelected);
        LoadSessionsForDate();
    }

    private void OnDateSelected(DateTime date)
    {
        SelectedDate = date;
    }

    private void LoadSessionsForDate()
    {
        // TODO: Load actual sessions from service
        Sessions.Clear();
        
        // Sample data
        if (SelectedDate.Date == DateTime.Now.Date)
        {
            Sessions.Add(new MeditationSession
            {
                Date = SelectedDate,
                Duration = TimeSpan.FromMinutes(10),
                Type = "Mindfulness",
                Completed = true
            });
        }
        else if (SelectedDate.Date == DateTime.Now.AddDays(-1).Date)
        {
            Sessions.Add(new MeditationSession
            {
                Date = SelectedDate,
                Duration = TimeSpan.FromMinutes(15),
                Type = "Breathing",
                Completed = true
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class MeditationSession
{
    public DateTime Date { get; set; }
    public TimeSpan Duration { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool Completed { get; set; }
    
    public string FormattedDuration => $"{Duration.TotalMinutes:0} min";
    public string CompletedText => Completed ? "Completed" : "Incomplete";
}
