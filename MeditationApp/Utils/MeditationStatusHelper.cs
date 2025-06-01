using MeditationApp.Models;

namespace MeditationApp.Utils;

public static class MeditationSessionStatusHelper
{
    /// <summary>
    /// Helper method to parse session status, handling legacy values
    /// </summary>
    public static MeditationSessionStatus ParseSessionStatus(string status)
    {
        status = status.ToUpperInvariant();
        return status switch
        {
            "COMPLETED" => MeditationSessionStatus.COMPLETED,
            "FAILED" => MeditationSessionStatus.FAILED,
            "REQUESTED" => MeditationSessionStatus.REQUESTED,
            _ => MeditationSessionStatus.REQUESTED
        };
    }
}