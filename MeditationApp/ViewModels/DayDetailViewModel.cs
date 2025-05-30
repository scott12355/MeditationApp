using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MeditationApp.Services;
using MeditationApp.Models;
using Microsoft.Maui.Controls;

namespace MeditationApp.ViewModels;

public class DayDetailViewModel : INotifyPropertyChanged
{
    private DateTime _selectedDate = DateTime.Today;
    private readonly MeditationSessionDatabase _database;
    private readonly CalendarDataService? _calendarDataService;
    private readonly CognitoAuthService? _cognitoAuthService;
    private readonly IAudioService _audioService;
    private bool _isLoading;
    private string _notes = string.Empty;
    private int? _mood;

    public DayDetailViewModel(MeditationSessionDatabase database, CalendarDataService? calendarDataService = null, CognitoAuthService? cognitoAuthService = null, IAudioService? audioService = null)
    {
        _database = database;
        _calendarDataService = calendarDataService;
        _cognitoAuthService = cognitoAuthService;
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        AddSessionCommand = new Command(OnAddSession);
        PlaySessionCommand = new Command<MeditationSession>(OnPlaySession);
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            _selectedDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedDate));
        }
    }

    public string FormattedDate => _selectedDate.ToString("dddd, MMMM d, yyyy");

    public ObservableCollection<MeditationSession> Sessions { get; } = new();

    public string Notes
    {
        get => _notes;
        set
        {
            _notes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNotes));
            OnPropertyChanged(nameof(HasNoNotes));
        }
    }

    public int? Mood
    {
        get => _mood;
        set
        {
            _mood = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMood));
            OnPropertyChanged(nameof(MoodEmoji));
            OnPropertyChanged(nameof(MoodDescription));
        }
    }

    public bool HasNotes => !string.IsNullOrEmpty(Notes);
    public bool HasNoNotes => !HasNotes;
    public bool HasMood => Mood.HasValue;
    public bool HasNoSessions => Sessions.Count == 0;

    public string MoodEmoji => Mood switch
    {
        1 => "😢",
        2 => "😕", 
        3 => "😐",
        4 => "😊",
        5 => "😄",
        _ => ""
    };

    public string MoodDescription => Mood switch
    {
        1 => "Very Sad",
        2 => "Sad", 
        3 => "Neutral",
        4 => "Happy",
        5 => "Very Happy",
        _ => ""
    };

    public int TotalMeditationTime => Sessions.Count * 15; // Default 15 min per session
    public int SessionCount { get; private set; }

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
    public ICommand PlaySessionCommand { get; }

    private MeditationSession? _selectedSession;
    public MeditationSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            _selectedSession = value;
            OnPropertyChanged();
            if (_selectedSession != null)
            {
                PlaySessionInternal(_selectedSession);
            }
        }
    }

    private async void PlaySessionInternal(MeditationSession session)
    {
        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
        try
        {
            // Check if session is downloaded and file exists
            var localPath = _audioService.GetLocalAudioPath(session);
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                // Not downloaded or file missing, attempt download
                if (page != null)
                    await page.DisplayAlert("Downloading", "Session audio is not downloaded. Downloading now...", "OK");
                var presignedUrl = await _audioService.GetPresignedUrlAsync(session.Uuid);
                if (string.IsNullOrEmpty(presignedUrl))
                {
                    if (page != null)
                        await page.DisplayAlert("Error", "Failed to get download URL for the session.", "OK");
                    return;
                }
                var success = await _audioService.DownloadSessionAudioAsync(session, presignedUrl);
                if (!success)
                {
                    if (page != null)
                        await page.DisplayAlert("Error", "Failed to download the session audio.", "OK");
                    return;
                }
                // Save updated session info to DB
                await _database.SaveSessionAsync(session);
                localPath = session.LocalAudioPath;
            }
            // At this point, localPath should be valid and file should exist
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                // TODO: Trigger actual audio playback here (e.g., via MediaElement or navigation)
                if (page != null)
                    await page.DisplayAlert("Play Session", $"Playing: {localPath}", "OK");
            }
            else
            {
                if (page != null)
                    await page.DisplayAlert("Error", "Audio file is missing or invalid.", "OK");
            }
        }
        catch (Exception ex)
        {
            if (page != null)
                await page.DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private void OnPlaySession(MeditationSession session)
    {
        PlaySessionInternal(session);
    }

    public async void LoadDayData(DateTime date)
    {
        SelectedDate = date;
        await LoadDayDataAsync();
    }

    private async Task LoadDayDataAsync()
    {
        IsLoading = true;
        try
        {
            Sessions.Clear();
            System.Diagnostics.Debug.WriteLine($"DayDetailViewModel: Loading sessions for date {_selectedDate:yyyy-MM-dd}");
            var allSessions = _calendarDataService != null
                ? await _calendarDataService.GetSessionsForDateAsync(_selectedDate)
                : await _database.GetAllSessionsForDateAsync(_selectedDate);
            System.Diagnostics.Debug.WriteLine($"DayDetailViewModel: Found {allSessions?.Count ?? -1} sessions for date {_selectedDate:yyyy-MM-dd}");
            LoadSessions(allSessions ?? new List<MeditationSession>());

            // Get daily insights if available
            try
            {
                var userId = await GetCurrentUserId();
                System.Diagnostics.Debug.WriteLine($"DayDetailViewModel: userId={userId}");
                if (!string.IsNullOrEmpty(userId))
                {
                    var dailyInsights = await _database.GetDailyInsightsAsync(userId, _selectedDate);
                    System.Diagnostics.Debug.WriteLine($"DayDetailViewModel: dailyInsights is null? {dailyInsights == null}");
                    if (dailyInsights != null)
                    {
                        Notes = dailyInsights.Notes ?? string.Empty;
                        Mood = dailyInsights.Mood;
                    }
                    else
                    {
                        Notes = string.Empty;
                        Mood = null;
                    }
                }
                else
                {
                    Notes = string.Empty;
                    Mood = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading daily insights: {ex.Message}");
                Notes = string.Empty;
                Mood = null;
            }
            
            // Calculate totals
            SessionCount = Sessions.Count;

            OnPropertyChanged(nameof(TotalMeditationTime));
            OnPropertyChanged(nameof(SessionCount));
            OnPropertyChanged(nameof(HasNoSessions));
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
        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
        if (page != null)
            await page.DisplayAlert("Add Session", 
                $"Add meditation session for {_selectedDate:MMMM d, yyyy}", "OK");
    }

    private async Task<string> GetCurrentUserId()
    {
        try
        {
            var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken) && _cognitoAuthService != null)
            {
                var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
                return attributes.FirstOrDefault(a => a.Name == "sub")?.Value ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting user ID: {ex.Message}");
        }
        return string.Empty;
    }

    public void LoadFromDayData(DayData dayData)
    {
        SelectedDate = dayData.Date;
        Notes = dayData.Notes;
        Mood = dayData.Mood;
        Sessions.Clear();
        foreach (var session in dayData.Sessions)
        {
            Sessions.Add(session);
        }
        OnPropertyChanged(nameof(Sessions));
        OnPropertyChanged(nameof(HasNoSessions));
        OnPropertyChanged(nameof(TotalMeditationTime));
        OnPropertyChanged(nameof(SessionCount));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasNoNotes));
        OnPropertyChanged(nameof(HasMood));
        OnPropertyChanged(nameof(MoodEmoji));
        OnPropertyChanged(nameof(MoodDescription));
    }

    private void LoadSessions(IEnumerable<MeditationSession> sessionList)
    {
        Sessions.Clear();
        foreach (var session in sessionList)
        {
            Sessions.Add(session);
        }
        OnPropertyChanged(nameof(Sessions));
        OnPropertyChanged(nameof(HasNoSessions));
        OnPropertyChanged(nameof(TotalMeditationTime));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
