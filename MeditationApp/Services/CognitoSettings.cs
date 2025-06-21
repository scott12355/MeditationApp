namespace MeditationApp.Services;

public class CognitoSettings
{
    public string UserPoolId { get; set; }
    public string AppClientId { get; set; }
    public string Region { get; set; }
    public string? AppleClientId { get; set; }
    public string? AppleAppClientId { get; set; } // <-- Add this

    public CognitoSettings(string userPoolId, string appClientId, string region, string? appleClientId = null, string? appleAppClientId = null)
    {
        UserPoolId = userPoolId;
        AppClientId = appClientId;
        Region = region;
        AppleClientId = appleClientId;
        AppleAppClientId = appleAppClientId; // <-- Add this
    }
}
