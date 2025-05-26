using System.Collections.Concurrent;
using MeditationApp.Models;

namespace MeditationApp.Services;

/// <summary>
/// Shared service for managing calendar data across multiple ViewModels
/// to avoid redundant database queries and improve performance
/// </summary>
public class CalendarDataService
{
    private readonly MeditationSessionDatabase _database;
    
    // In-memory cache shared across all calendar views
    private readonly ConcurrentDictionary<string, List<MeditationSession>> _monthlyCache = new();
    private readonly ConcurrentDictionary<string, List<MeditationSession>> _dailyCache = new();
    private readonly ConcurrentDictionary<DateTime, bool> _hasSessionCache = new();
    
    public CalendarDataService(MeditationSessionDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Get sessions for a specific month, using cache when possible
    /// </summary>
    public async Task<List<MeditationSession>> GetSessionsForMonthAsync(int year, int month)
    {
        var cacheKey = $"{year}-{month:00}";
        
        if (_monthlyCache.TryGetValue(cacheKey, out var cachedSessions))
        {
            System.Diagnostics.Debug.WriteLine($"CalendarDataService: Using cached data for month {cacheKey} - {cachedSessions.Count} sessions");
            return new List<MeditationSession>(cachedSessions);
        }
        
        System.Diagnostics.Debug.WriteLine($"CalendarDataService: Loading sessions for month {cacheKey} from database");
        var sessions = await _database.GetSessionsForMonthAsync(year, month);
        
        // Cache the result
        _monthlyCache[cacheKey] = new List<MeditationSession>(sessions);
        
        // Also cache individual day indicators for faster has-session checks
        foreach (var session in sessions)
        {
            _hasSessionCache[session.Timestamp.Date] = true;
        }
        
        return sessions;
    }

    /// <summary>
    /// Get sessions for a specific date, using cache when possible
    /// </summary>
    public async Task<List<MeditationSession>> GetSessionsForDateAsync(DateTime date)
    {
        var cacheKey = date.ToString("yyyy-MM-dd");
        
        if (_dailyCache.TryGetValue(cacheKey, out var cachedSessions))
        {
            System.Diagnostics.Debug.WriteLine($"CalendarDataService: Using cached data for date {cacheKey} - {cachedSessions.Count} sessions");
            return new List<MeditationSession>(cachedSessions);
        }
        
        System.Diagnostics.Debug.WriteLine($"CalendarDataService: Loading sessions for date {cacheKey} from database");
        var sessions = await _database.GetSessionsForDateAsync(date);
        
        // Cache the result
        _dailyCache[cacheKey] = new List<MeditationSession>(sessions);
        _hasSessionCache[date.Date] = sessions.Count > 0;
        
        return sessions;
    }

    /// <summary>
    /// Fast check if a specific date has any sessions (uses cache)
    /// </summary>
    public bool HasSessionOnDate(DateTime date)
    {
        if (_hasSessionCache.TryGetValue(date.Date, out var hasSession))
        {
            return hasSession;
        }
        
        // If not in cache, assume no session for now
        // This will be populated when month data is loaded
        return false;
    }

    /// <summary>
    /// Preload data for a specific month and cache day indicators
    /// </summary>
    public async Task PreloadMonthAsync(int year, int month)
    {
        System.Diagnostics.Debug.WriteLine($"CalendarDataService: Preloading month {year}-{month:00}");
        
        // This will populate both the monthly cache and daily has-session cache
        await GetSessionsForMonthAsync(year, month);
        
        System.Diagnostics.Debug.WriteLine($"CalendarDataService: Preload completed for month {year}-{month:00}");
    }

    /// <summary>
    /// Preload data for current and nearby months
    /// </summary>
    public async Task PreloadCalendarDataAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("CalendarDataService: Starting comprehensive calendar preload");
            var currentDate = DateTime.Now;
            
            // Preload current month
            await PreloadMonthAsync(currentDate.Year, currentDate.Month);
            
            // Preload previous month
            var previousMonth = currentDate.AddMonths(-1);
            await PreloadMonthAsync(previousMonth.Year, previousMonth.Month);
            
            // Preload next month
            var nextMonth = currentDate.AddMonths(1);
            await PreloadMonthAsync(nextMonth.Year, nextMonth.Month);
            
            // Preload individual days around today
            for (int i = -7; i <= 7; i++)
            {
                var date = currentDate.AddDays(i);
                await GetSessionsForDateAsync(date);
            }
            
            System.Diagnostics.Debug.WriteLine("CalendarDataService: Comprehensive calendar preload completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CalendarDataService: Error during preload: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all cached data (call when sessions are modified)
    /// </summary>
    public void ClearCache()
    {
        System.Diagnostics.Debug.WriteLine("CalendarDataService: Clearing all caches");
        _monthlyCache.Clear();
        _dailyCache.Clear();
        _hasSessionCache.Clear();
    }

    /// <summary>
    /// Clear cache for a specific month
    /// </summary>
    public void ClearMonthCache(int year, int month)
    {
        var cacheKey = $"{year}-{month:00}";
        _monthlyCache.TryRemove(cacheKey, out _);
        
        // Clear daily caches for this month
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);
        var keysToRemove = _dailyCache.Keys.Where(key => 
        {
            if (DateTime.TryParse(key, out var date))
            {
                return date >= startDate && date < endDate;
            }
            return false;
        }).ToList();
        
        foreach (var key in keysToRemove)
        {
            _dailyCache.TryRemove(key, out _);
        }
    }
}
