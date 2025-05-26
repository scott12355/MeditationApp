using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private int _selectedMood = 3; // Default to neutral mood

    public string FormattedDate => CurrentDate.ToString("dddd, MMMM dd, yyyy");

    public TodayViewModel()
    {
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
            Console.WriteLine($"Selected mood: {moodValue}");
            SelectedMood = moodValue;
            await Application.Current.MainPage.DisplayAlert("Meditation", $"Selected mood: {moodValue}", "OK");
        }
        else
        {
            Console.WriteLine($"Invalid mood value: {mood}");
        }
    }

    private void LoadTodayData()
    {
        // TODO: Load actual data from service
    }

    // You might need to add this property changed notification for FormattedDate
    partial void OnCurrentDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FormattedDate));
    }
}
