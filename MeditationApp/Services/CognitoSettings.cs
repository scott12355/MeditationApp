namespace MeditationApp.Services;

public class CognitoSettings
{
    public string UserPoolId { get; set; }
    public string AppClientId { get; set; }
    public string Region { get; set; }

    public CognitoSettings(string userPoolId, string appClientId, string region)
    {
        UserPoolId = userPoolId;
        AppClientId = appClientId;
        Region = region;
    }
}
