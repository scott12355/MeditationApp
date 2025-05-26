using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MeditationApp.Services;

namespace MeditationApp.ViewModels;

public class CalendarControlViewModel : INotifyPropertyChanged
{
    private DateTime _currentDate;
    private CalendarDayModel? _selectedDay;
    private bool _isDarkTheme;
    private bool _isNavigating;
    private bool _isLoadingCalendar;
    private readonly MeditationSessionDatabase? _database;
    private readonly CalendarDataService? _calendarDataService;
    
    // Cached colors for performance
    private Color _currentMonthColor = Colors.Black;
    private Color _otherMonthColor = Colors.LightGray;
    private Color _todayColor = Colors.Black;

    public CalendarControlViewModel(MeditationSessionDatabase? database = null, CalendarDataService? calendarDataService = null)
    {
        _database = database;
        _calendarDataService = calendarDataService;
        _currentDate = DateTime.Now;
        _isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
        UpdateCachedColors();
        
        PreviousMonthCommand = new Command(OnPreviousMonth, () => !_isNavigating);
        NextMonthCommand = new Command(OnNextMonth, () => !_isNavigating);
        DayTappedCommand = new Command<CalendarDayModel>(OnDayTapped);
        
        // Listen for theme changes
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += OnThemeChanged;
        }
        
        LoadCalendarDays();
    }
    
    private void UpdateCachedColors()
    {
        _currentMonthColor = _isDarkTheme ? Colors.LightGray : Colors.Black;
        _otherMonthColor = _isDarkTheme ? Colors.Gray : Colors.LightGray;
        _todayColor = _isDarkTheme ? Colors.White : Colors.Black;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        _isDarkTheme = e.RequestedTheme == AppTheme.Dark;
        UpdateCachedColors();
        
        // Update all day colors efficiently
        foreach (var day in CalendarDays)
        {
            day.TextColor = GetTextColor(day.IsCurrentMonth, day.IsToday);
        }
    }

    public ObservableCollection<CalendarDayModel> CalendarDays { get; } = new();

    public string CurrentMonthYear => _currentDate.ToString("MMMM yyyy");

    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand DayTappedCommand { get; }

    public bool IsLoadingCalendar
    {
        get => _isLoadingCalendar;
        set
        {
            _isLoadingCalendar = value;
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
            OnPropertyChanged(nameof(CurrentMonthYear));
            LoadCalendarDays();
        }
    }

    private void OnPreviousMonth()
    {
        if (_isNavigating) return;
        
        _isNavigating = true;
        ((Command)PreviousMonthCommand).ChangeCanExecute();
        ((Command)NextMonthCommand).ChangeCanExecute();
        
        CurrentDate = _currentDate.AddMonths(-1);
        
        _isNavigating = false;
        ((Command)PreviousMonthCommand).ChangeCanExecute();
        ((Command)NextMonthCommand).ChangeCanExecute();
    }

    private void OnNextMonth()
    {
        if (_isNavigating) return;
        
        _isNavigating = true;
        ((Command)PreviousMonthCommand).ChangeCanExecute();
        ((Command)NextMonthCommand).ChangeCanExecute();
        
        CurrentDate = _currentDate.AddMonths(1);
        
        _isNavigating = false;
        ((Command)PreviousMonthCommand).ChangeCanExecute();
        ((Command)NextMonthCommand).ChangeCanExecute();
    }

    private async void OnDayTapped(CalendarDayModel day)
    {
        if (day == null || !day.IsCurrentMonth) return;

        // Clear previous selection
        if (_selectedDay != null)
        {
            _selectedDay.IsSelected = false;
        }

        // Set new selection
        day.IsSelected = true;
        _selectedDay = day;

        // Navigate to day detail page
        await Shell.Current.GoToAsync($"DayDetailPage?date={day.Date:yyyy-MM-dd}");
    }

    private async void LoadCalendarDays()
    {
        IsLoadingCalendar = true;
        System.Diagnostics.Debug.WriteLine($"CalendarControlViewModel: Starting LoadCalendarDays for {_currentDate:yyyy-MM}");
        
        var firstDayOfMonth = new DateTime(_currentDate.Year, _currentDate.Month, 1);
        var startDate = firstDayOfMonth.AddDays(-(int)firstDayOfMonth.DayOfWeek);
        
        // Reuse existing items or create new ones
        const int totalDays = 42;
        
        // Add new items if needed
        while (CalendarDays.Count < totalDays)
        {
            CalendarDays.Add(new CalendarDayModel());
        }
        
        // Remove excess items if any
        while (CalendarDays.Count > totalDays)
        {
            CalendarDays.RemoveAt(CalendarDays.Count - 1);
        }

        // Get sessions for the month - use CalendarDataService if available for better performance
        var sessionsThisMonth = new List<MeditationApp.Models.MeditationSession>();
        if (_calendarDataService != null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"CalendarControlViewModel: Loading sessions via CalendarDataService for month {_currentDate.Year}-{_currentDate.Month}");
                sessionsThisMonth = await _calendarDataService.GetSessionsForMonthAsync(_currentDate.Year, _currentDate.Month);
                System.Diagnostics.Debug.WriteLine($"CalendarControlViewModel: Found {sessionsThisMonth.Count} sessions via CalendarDataService");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalendarControlViewModel: Error loading sessions via CalendarDataService: {ex.Message}");
            }
        }
        else if (_database != null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"CalendarControlViewModel: Loading sessions directly from database for month {_currentDate.Year}-{_currentDate.Month}");
                sessionsThisMonth = await _database.GetSessionsForMonthAsync(_currentDate.Year, _currentDate.Month);
                System.Diagnostics.Debug.WriteLine($"CalendarControlViewModel: Found {sessionsThisMonth.Count} sessions from database");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalendarControlViewModel: Error loading sessions from database: {ex.Message}");
            }
        }

        // Create a HashSet for faster session date lookups (O(1) instead of O(n))
        var sessionDates = new HashSet<DateTime>(sessionsThisMonth.Select(s => s.Timestamp.Date));
        System.Diagnostics.Debug.WriteLine($"CalendarControlViewModel: Created session date lookup with {sessionDates.Count} unique dates");

        // Update existing items in place with notifications enabled
        for (int i = 0; i < totalDays; i++)
        {
            var date = startDate.AddDays(i);
            var isCurrentMonth = date.Month == _currentDate.Month;
            var isToday = date.Date == DateTime.Today;
            var hasSession = sessionDates.Contains(date.Date); // Much faster lookup
            var dayModel = CalendarDays[i];

            dayModel.UpdateDay(
                date, 
                date.Day.ToString(), 
                isCurrentMonth, 
                isToday,
                GetTextColor(isCurrentMonth, isToday),
                isToday ? Microsoft.Maui.Controls.FontAttributes.Bold : Microsoft.Maui.Controls.FontAttributes.None,
                hasSession
            );
        }
        
        IsLoadingCalendar = false;
        System.Diagnostics.Debug.WriteLine("CalendarControlViewModel: LoadCalendarDays completed");
    }

    private Color GetTextColor(bool isCurrentMonth, bool isToday)
    {
        if (!isCurrentMonth)
            return _otherMonthColor;
        
        if (isToday)
            return _todayColor;
            
        return _currentMonthColor;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class CalendarDayModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private DateTime _date;
    private string _day = string.Empty;
    private bool _isCurrentMonth;
    private bool _isToday;
    private Color _textColor = Colors.Black;
    private Microsoft.Maui.Controls.FontAttributes _fontWeight;
    private bool _hasSession;

    public DateTime Date 
    { 
        get => _date;
        set => SetProperty(ref _date, value);
    }
    
    public string Day 
    { 
        get => _day;
        set => SetProperty(ref _day, value);
    }
    
    public bool IsCurrentMonth 
    { 
        get => _isCurrentMonth;
        set => SetProperty(ref _isCurrentMonth, value);
    }
    
    public bool IsToday 
    { 
        get => _isToday;
        set => SetProperty(ref _isToday, value);
    }
    
    public Color TextColor 
    { 
        get => _textColor;
        set => SetProperty(ref _textColor, value);
    }
    
    public Microsoft.Maui.Controls.FontAttributes FontWeight 
    { 
        get => _fontWeight;
        set => SetProperty(ref _fontWeight, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasSession
    {
        get => _hasSession;
        set => SetProperty(ref _hasSession, value);
    }

    public void UpdateDay(DateTime date, string day, bool isCurrentMonth, bool isToday, Color textColor, Microsoft.Maui.Controls.FontAttributes fontWeight, bool hasSession = false)
    {
        // Batch updates to minimize property change notifications
        var hasChanges = false;
        
        // Only update if values have actually changed
        if (_date != date) { _date = date; hasChanges = true; }
        if (_day != day) { _day = day; hasChanges = true; }
        if (_isCurrentMonth != isCurrentMonth) { _isCurrentMonth = isCurrentMonth; hasChanges = true; }
        if (_isToday != isToday) { _isToday = isToday; hasChanges = true; }
        if (_textColor != textColor) { _textColor = textColor; hasChanges = true; }
        if (_fontWeight != fontWeight) { _fontWeight = fontWeight; hasChanges = true; }
        if (_hasSession != hasSession) { _hasSession = hasSession; hasChanges = true; }
        if (_isSelected) { _isSelected = false; hasChanges = true; } // Reset selection when updating
        
        // Fire notifications for all changed properties at once
        if (hasChanges)
        {
            OnPropertyChanged(nameof(Date));
            OnPropertyChanged(nameof(Day));
            OnPropertyChanged(nameof(IsCurrentMonth));
            OnPropertyChanged(nameof(IsToday));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(FontWeight));
            OnPropertyChanged(nameof(HasSession));
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
