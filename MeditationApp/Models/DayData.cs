using System.Collections.ObjectModel;
using MeditationApp.Models;

namespace MeditationApp.Models;

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
