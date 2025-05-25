using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MeditationApp.ViewModels;

public class TodayViewModel : INotifyPropertyChanged
{
    private string _welcomeMessage = "Today's Meditation";
    private DateTime _currentDate = DateTime.Now;
    private int _todaySessionsCompleted = 0;
    private int _streakDays = 0;

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set
        {
            _welcomeMessage = value;
            OnPropertyChanged();
        }
    }

    public DateTime CurrentDate
    {
        get => _currentDate;
        set
        {
            _currentDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedDate));
        }
    }

    public string FormattedDate => CurrentDate.ToString("dddd, MMMM dd, yyyy");

    private int _selectedMood = 3; // Default to neutral mood
    public int SelectedMood
    {
        get => _selectedMood;
        set
        {
            if (_selectedMood != value)
            {
                _selectedMood = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand StartMeditationCommand { get; }
    public ICommand SelectMoodCommand { get; }

    public TodayViewModel()
    {
        StartMeditationCommand = new Command(OnStartMeditation);
        SelectMoodCommand = new Command<int>(OnSelectMood);
        LoadTodayData();
    }

    private async void OnStartMeditation()
    {
        // TODO: Navigate to meditation session
        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert("Meditation", "Starting your meditation session...", "OK");
    }

    private void OnSelectMood(int mood)
    {
        SelectedMood = mood;
        // Optionally: Save mood to a service or database here
    }

    private void LoadTodayData()
    {
        // TODO: Load actual data from service

    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
