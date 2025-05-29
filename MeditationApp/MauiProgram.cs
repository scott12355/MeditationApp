using Microsoft.Extensions.Logging;
using MeditationApp.Services;
using Amazon;
using Amazon.CognitoIdentityProvider;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using MeditationApp.Models;
using CommunityToolkit.Maui;
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
            .UseMauiCommunityToolkitMediaElement()
            .UseMauiCommunityToolkit()
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

        // Register preload service
        builder.Services.AddSingleton<PreloadService>();

        // Register IHttpClientFactory for GraphQLService
        builder.Services.AddHttpClient();

        // Register GraphQL service with CognitoAuthService dependency
        builder.Services.AddSingleton<GraphQLService>(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var cognitoAuthService = provider.GetRequiredService<CognitoAuthService>();
            return new GraphQLService(httpClient, cognitoAuthService);
        });

        // Register AudioService
        builder.Services.AddSingleton<IAudioService, AudioService>(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var graphQLService = provider.GetRequiredService<GraphQLService>();
            return new AudioService(httpClient, graphQLService);
        });

        // Register TodayViewModel as singleton so splash screen and TodayPage share the same instance
        builder.Services.AddSingleton<ViewModels.TodayViewModel>();

        // Register views
        builder.Services.AddTransient<Views.SplashPage>();
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
        builder.Services.AddTransient<ViewModels.SettingsViewModel>();
        builder.Services.AddTransient<ViewModels.CalendarControlViewModel>();

        // Register MeditationSessionDatabase
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "meditation_sessions.db3");
        builder.Services.AddSingleton(new MeditationApp.Services.MeditationSessionDatabase(dbPath));

        // Register CalendarDataService (shared service for calendar data)
        builder.Services.AddSingleton<MeditationApp.Services.CalendarDataService>();

        // Register NotificationService
        builder.Services.AddSingleton<MeditationApp.Services.NotificationService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}