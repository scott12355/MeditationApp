using Microsoft.Extensions.Logging;
using MeditationApp.Services;
using Amazon;
using Amazon.CognitoIdentityProvider;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using MeditationApp.Models;
using CommunityToolkit.Maui;
using Plugin.Maui.Audio;
using MediaManager;
using UraniumUI;
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
            .UseMauiCommunityToolkit()
            .UseUraniumUI()
            .UseUraniumUIBlurs()
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
        
        // Register hybrid authentication service with all dependencies
        builder.Services.AddSingleton<HybridAuthService>(provider =>
        {
            var cognitoService = provider.GetRequiredService<CognitoAuthService>();
            var localService = provider.GetRequiredService<LocalAuthService>();
            var sessionDatabase = provider.GetRequiredService<MeditationSessionDatabase>();
            var preloadService = provider.GetRequiredService<PreloadService>();
            return new HybridAuthService(cognitoService, localService, sessionDatabase, preloadService, provider);
        });

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
        builder.Services.AddSingleton<IAudioDownloadService, AudioDownloadDownloadService>(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var graphQLService = provider.GetRequiredService<GraphQLService>();
            return new AudioDownloadDownloadService(httpClient, graphQLService);
        });

        // Register TodayViewModel as singleton so splash screen and TodayPage share the same instance
        builder.Services.AddSingleton<ViewModels.TodayViewModel>(provider =>
            new ViewModels.TodayViewModel(
                provider.GetRequiredService<GraphQLService>(),
                provider.GetRequiredService<CognitoAuthService>(),
                provider.GetRequiredService<MeditationSessionDatabase>(),
                provider.GetRequiredService<IAudioDownloadService>(),
                provider.GetRequiredService<SessionStatusPoller>(),
                provider.GetRequiredService<AudioPlayerService>(),
                provider.GetRequiredService<DatabaseSyncService>()
            )
        );

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
        builder.Services.AddTransient<ViewModels.SimpleCalendarViewModel>(provider =>
            new ViewModels.SimpleCalendarViewModel(
                provider.GetRequiredService<MeditationSessionDatabase>(),
                provider.GetRequiredService<CalendarDataService>(),
                provider.GetRequiredService<CognitoAuthService>(),
                provider.GetRequiredService<DatabaseSyncService>()
            )
        );
        builder.Services.AddTransient<ViewModels.DayDetailViewModel>(provider =>
            new ViewModels.DayDetailViewModel(
                provider.GetRequiredService<MeditationSessionDatabase>(),
                provider.GetService<CalendarDataService>(),
                provider.GetService<CognitoAuthService>(),
                provider.GetRequiredService<IAudioDownloadService>(),
                provider.GetRequiredService<AudioPlayerService>()
            )
        );

        // Register MeditationSessionDatabase
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "meditation_sessions.db3");
        builder.Services.AddSingleton(new MeditationApp.Services.MeditationSessionDatabase(dbPath));

        // Register CalendarDataService
        builder.Services.AddSingleton<CalendarDataService>();

        // Register DatabaseSyncService
        builder.Services.AddSingleton<DatabaseSyncService>(provider =>
        {
            var database = provider.GetRequiredService<MeditationSessionDatabase>();
            var graphQLService = provider.GetRequiredService<GraphQLService>();
            var localAuthService = provider.GetRequiredService<LocalAuthService>();
            var calendarDataService = provider.GetRequiredService<CalendarDataService>();
            var cognitoAuthService = provider.GetRequiredService<CognitoAuthService>();
            return new DatabaseSyncService(database, graphQLService, localAuthService, calendarDataService, cognitoAuthService);
        });

        // Register NotificationService
        builder.Services.AddSingleton<MeditationApp.Services.NotificationService>();
        
        // Register Plugin.Maui.Audio
        builder.Services.AddSingleton(AudioManager.Current);
        
        // Configure MediaManager for enhanced metadata support
        builder.Services.AddSingleton(CrossMediaManager.Current);
        
        // Register SessionStatusPoller
        builder.Services.AddSingleton<SessionStatusPoller>(provider =>
        {
            var graphQLService = provider.GetRequiredService<GraphQLService>();
            var sessionDatabase = provider.GetRequiredService<MeditationSessionDatabase>();
            return new SessionStatusPoller(graphQLService, sessionDatabase);
        });
        
        // Register AudioPlayerService with Plugin.Maui.Audio dependency
        builder.Services.AddSingleton<AudioPlayerService>(provider =>
        {
            var audioManager = provider.GetRequiredService<IAudioManager>();
            return new AudioPlayerService(audioManager);
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}