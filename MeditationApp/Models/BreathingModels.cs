namespace MeditationApp.Models
{
    public enum BreathingPhase
    {
        Ready,
        Inhale,
        InhaleHold,
        Exhale,
        ExhaleHold,
        Rest,
        Completed
    }

    public enum BreathingState
    {
        Stopped,
        Playing,
        Paused,
        Completed
    }

    public class BreathingSettings
    {
        public bool EnableHapticFeedback { get; set; } = true;
        public bool EnableSoundEffects { get; set; } = true;
        public bool EnableBackgroundMusic { get; set; } = false;
        public string BackgroundMusicTrack { get; set; } = "none";
        public double SoundVolume { get; set; } = 0.5;
        public bool EnableVisualCues { get; set; } = true;
        public bool EnableCountdown { get; set; } = true;
        public bool AutoStartNextCycle { get; set; } = true;
        public bool ShowProgress { get; set; } = true;
        public string PreferredTheme { get; set; } = "nature"; // nature, minimal, cosmic
        public bool EnableReminders { get; set; } = false;
        public TimeSpan ReminderTime { get; set; } = new(9, 0, 0); // 9 AM default
        public List<DayOfWeek> ReminderDays { get; set; } = new();
        public bool EnableSessionSummary { get; set; } = true;
        public bool TrackMoodChanges { get; set; } = false;
    }
}
