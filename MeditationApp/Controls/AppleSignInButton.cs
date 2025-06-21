using Microsoft.Maui.Controls;

namespace MeditationApp.Controls;

public class AppleSignInButton : View
{
    public event EventHandler<string?>? SignInCompleted;

    public void OnSignInCompleted(string? idToken)
    {
        SignInCompleted?.Invoke(this, idToken);
    }
}
