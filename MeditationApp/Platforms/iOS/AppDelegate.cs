using Foundation;
using MediaManager;

namespace MeditationApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    
    public override bool FinishedLaunching(UIKit.UIApplication application, Foundation.NSDictionary launchOptions)
    {
        // Initialize MediaManager for iOS lock screen controls
        CrossMediaManager.Current.Init();
        
        return base.FinishedLaunching(application, launchOptions);
    }
}