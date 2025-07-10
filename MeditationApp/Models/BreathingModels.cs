using CommunityToolkit.Mvvm.ComponentModel;

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
}
