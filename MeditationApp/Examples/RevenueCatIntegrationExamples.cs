// using MeditationApp.Services;
//
// namespace MeditationApp.Examples;
//
// /// <summary>
// /// Example implementations showing how to integrate RevenueCat user metadata
// /// </summary>
// public static class RevenueCatIntegrationExamples
// {
//     /// <summary>
//     /// Example: Set user data in RevenueCat after successful login
//     /// Call this method after successful authentication
//     /// </summary>
//     public static async Task SetupRevenueCatUserAsync(IRevenueCatUserService revenueCatService, CognitoAuthService cognitoService)
//     {
//         try
//         {
//             // Get current user info from Cognito
//             var userInfo = await cognitoService.GetCurrentUserInfoAsync();
//             if (userInfo == null) return;
//
//             // Set user ID for RevenueCat analytics
//             await revenueCatService.SetUserIdAsync(userInfo.UserId);
//
//             // Set user attributes
//             var attributes = new UserAttributes
//             {
//                 Email = userInfo.Email,
//                 DisplayName = $"{userInfo.FirstName} {userInfo.LastName}",
//                 SignUpDate = userInfo.SignUpDate,
//                 LastActiveDate = DateTime.UtcNow
//             };
//
//             await revenueCatService.SetUserAttributesAsync(attributes);
//         }
//         catch (Exception ex)
//         {
//             System.Diagnostics.Debug.WriteLine($"Error setting up RevenueCat user: {ex.Message}");
//         }
//     }
//
//     /// <summary>
//     /// Example: Update meditation stats in RevenueCat
//     /// Call this after completing a meditation session
//     /// </summary>
//     public static async Task UpdateMeditationStatsAsync(IRevenueCatUserService revenueCatService, int totalMeditations, int currentStreak)
//     {
//         try
//         {
//             var attributes = new UserAttributes
//             {
//                 TotalMeditations = totalMeditations,
//                 DaysStreakCount = currentStreak,
//                 LastActiveDate = DateTime.UtcNow
//             };
//
//             await revenueCatService.SetUserAttributesAsync(attributes);
//         }
//         catch (Exception ex)
//         {
//             System.Diagnostics.Debug.WriteLine($"Error updating meditation stats: {ex.Message}");
//         }
//     }
//
//     /// <summary>
//     /// Example: Log user behavior events
//     /// Call this when users interact with premium features
//     /// </summary>
//     public static async Task LogFeatureUsageAsync(IRevenueCatUserService revenueCatService, string featureName)
//     {
//         try
//         {
//             var eventData = new Dictionary<string, object>
//             {
//                 ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
//                 ["feature"] = featureName
//             };
//
//             await revenueCatService.LogUserEventAsync("feature_accessed", eventData);
//         }
//         catch (Exception ex)
//         {
//             System.Diagnostics.Debug.WriteLine($"Error logging feature usage: {ex.Message}");
//         }
//     }
//
//     /// <summary>
//     /// Example: Integration in LoginViewModel
//     /// </summary>
//     public static class LoginViewModelExample
//     {
//         public static async Task HandleSuccessfulLogin(
//             IRevenueCatUserService revenueCatService, 
//             CognitoAuthService cognitoService,
//             string userId, 
//             string email, 
//             string displayName)
//         {
//             // Set user ID immediately after login
//             await revenueCatService.SetUserIdAsync(userId);
//
//             // Set basic user info
//             await revenueCatService.SetEmailAsync(email);
//             await revenueCatService.SetDisplayNameAsync(displayName);
//
//             // Set additional attributes
//             var attributes = new UserAttributes
//             {
//                 Email = email,
//                 DisplayName = displayName,
//                 LastActiveDate = DateTime.UtcNow,
//                 SignUpDate = DateTime.UtcNow // You'd get this from your user data
//             };
//
//             await revenueCatService.SetUserAttributesAsync(attributes);
//         }
//     }
//
//     /// <summary>
//     /// Example: Integration in TodayViewModel when completing sessions
//     /// </summary>
//     public static class TodayViewModelExample
//     {
//         public static async Task HandleSessionCompletion(
//             IRevenueCatUserService revenueCatService,
//             int totalCompletedSessions,
//             int currentStreak,
//             string sessionType)
//         {
//             // Update meditation stats
//             await UpdateMeditationStatsAsync(revenueCatService, totalCompletedSessions, currentStreak);
//
//             // Log session completion event
//             var eventData = new Dictionary<string, object>
//             {
//                 ["session_type"] = sessionType,
//                 ["total_sessions"] = totalCompletedSessions,
//                 ["current_streak"] = currentStreak
//             };
//
//             await revenueCatService.LogUserEventAsync("session_completed", eventData);
//         }
//     }
//
//     /// <summary>
//     /// Example: Integration in SettingsViewModel for user profile updates
//     /// </summary>
//     public static class SettingsViewModelExample
//     {
//         public static async Task HandleProfileUpdate(
//             IRevenueCatUserService revenueCatService,
//             string email,
//             string displayName,
//             string phoneNumber)
//         {
//             // Update individual attributes
//             if (!string.IsNullOrEmpty(email))
//                 await revenueCatService.SetEmailAsync(email);
//
//             if (!string.IsNullOrEmpty(displayName))
//                 await revenueCatService.SetDisplayNameAsync(displayName);
//
//             if (!string.IsNullOrEmpty(phoneNumber))
//                 await revenueCatService.SetPhoneNumberAsync(phoneNumber);
//         }
//     }
// }
//
// /// <summary>
// /// Extension methods to make RevenueCat integration easier in ViewModels
// /// </summary>
// public static class RevenueCatExtensions
// {
//     /// <summary>
//     /// Quick setup for new users
//     /// </summary>
//     public static async Task SetupNewUserAsync(this IRevenueCatUserService service, string userId, string email, string displayName)
//     {
//         await service.SetUserIdAsync(userId);
//         await service.SetEmailAsync(email);
//         await service.SetDisplayNameAsync(displayName);
//
//         var attributes = new UserAttributes
//         {
//             Email = email,
//             DisplayName = displayName,
//             SignUpDate = DateTime.UtcNow,
//             LastActiveDate = DateTime.UtcNow
//         };
//
//         await service.SetUserAttributesAsync(attributes);
//     }
//
//     /// <summary>
//     /// Update activity tracking
//     /// </summary>
//     public static async Task UpdateActivityAsync(this IRevenueCatUserService service, int totalMeditations, int streak)
//     {
//         var attributes = new UserAttributes
//         {
//             TotalMeditations = totalMeditations,
//             DaysStreakCount = streak,
//             LastActiveDate = DateTime.UtcNow
//         };
//
//         await service.SetUserAttributesAsync(attributes);
//     }
// }
