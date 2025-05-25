using Microsoft.Extensions.Logging;
using MeditationApp.Services;
using Amazon;
using Amazon.CognitoIdentityProvider;
#if IOS
using Microsoft.Maui.Handlers;
#endif
#if ANDROID
using Microsoft.Maui.Handlers;
using MeditationApp.Controls;
// using MeditationApp.Platforms.Android;
#endif

namespace MeditationApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if IOS
        // Register custom handler for iOS to remove default Entry borders
        EntryHandler.Mapper.AppendToMapping("CustomEntryHandler", (handler, view) =>
        {
            if (handler.PlatformView is UIKit.UITextField textField)
            {
                textField.BorderStyle = UIKit.UITextBorderStyle.None;
                textField.Layer.BorderWidth = 0;
                textField.Layer.CornerRadius = 0;
                textField.BackgroundColor = UIKit.UIColor.Clear;
            }
        });
#endif


        // Register Cognito authentication service
        var cognitoSettings = new CognitoSettings(
            userPoolId: "eu-west-1_FDo9Q79jx", // Replace with your actual User Pool ID
            appClientId: "5t3s0fctvuk3di5b022741sbrg", // Replace with your actual App Client ID
            region: "eu-west-1" // e.g., "us-east-1"
        );

        builder.Services.AddSingleton(cognitoSettings);
        builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(provider =>
            new AmazonCognitoIdentityProviderClient(
                RegionEndpoint.GetBySystemName(cognitoSettings.Region)));
        builder.Services.AddSingleton<CognitoAuthService>(provider =>
        {
            var settings = provider.GetRequiredService<CognitoSettings>();
            var cognitoProvider = provider.GetRequiredService<IAmazonCognitoIdentityProvider>();
            return new CognitoAuthService(settings.AppClientId, settings.UserPoolId, settings.Region);
        });

        // Register local authentication service
        builder.Services.AddSingleton<LocalAuthService>();
        
        // Register hybrid authentication service
        builder.Services.AddSingleton<HybridAuthService>();

        // Register views
        builder.Services.AddTransient<Views.LoginPage>();
        builder.Services.AddTransient<Views.SignUpPage>();
        builder.Services.AddTransient<Views.VerificationPage>();
        builder.Services.AddTransient<Views.ProfilePage>();
        builder.Services.AddTransient<Views.TodayPage>();
        builder.Services.AddTransient<Views.CalendarPage>();
        builder.Services.AddTransient<Views.SettingsPage>();
        builder.Services.AddTransient<Views.DayDetailPage>();

        // Register view models
        builder.Services.AddTransient<ViewModels.LoginViewModel>();
        builder.Services.AddTransient<ViewModels.SignUpViewModel>();
        builder.Services.AddTransient<ViewModels.VerificationViewModel>();
        builder.Services.AddTransient<ViewModels.TodayViewModel>();
        builder.Services.AddTransient<ViewModels.CalendarViewModel>();
        builder.Services.AddTransient<ViewModels.SettingsViewModel>();
        builder.Services.AddTransient<ViewModels.CalendarControlViewModel>();
        builder.Services.AddTransient<ViewModels.DayDetailViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}