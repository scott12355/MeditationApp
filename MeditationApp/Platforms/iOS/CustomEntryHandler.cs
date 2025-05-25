using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MeditationApp.Platforms.iOS;

public class CustomEntryHandler : EntryHandler
{
    protected override void ConnectHandler(MauiTextField platformView)
    {
        base.ConnectHandler(platformView);
        
        if (platformView != null)
        {
            // Remove the default iOS border
            platformView.BorderStyle = UITextBorderStyle.None;
            platformView.Layer.BorderWidth = 0;
            platformView.Layer.CornerRadius = 0;
            
            // Set background to transparent to remove any default background
            platformView.BackgroundColor = UIColor.Clear;
        }
    }
}
