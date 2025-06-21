using MeditationApp.Services;
using MeditationApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MeditationApp.Services;

public class HybridAuthService
{
    private readonly CognitoAuthService _cognitoService;
    private readonly LocalAuthService _localService;
    private readonly MeditationSessionDatabase _sessionDatabase;
    private readonly PreloadService _preloadService;
    private readonly IServiceProvider _serviceProvider;

    public HybridAuthService(
        CognitoAuthService cognitoService,
        LocalAuthService localService,
        MeditationSessionDatabase sessionDatabase,
        PreloadService preloadService,
        IServiceProvider serviceProvider)
    {
        _cognitoService = cognitoService;
        _localService = localService;
        _sessionDatabase = sessionDatabase;
        _preloadService = preloadService;
        _serviceProvider = serviceProvider;
    }

    public async Task<AuthenticationResult> SignInAsync(string username, string password)
    {
        var isNetworkAvailable = await _localService.IsNetworkAvailableAsync();
        var isOfflineModeEnabled = await _localService.IsOfflineModeEnabledAsync();

        // Try online authentication first if network is available
        if (isNetworkAvailable)
        {
            try
            {
                var onlineResult = await _cognitoService.SignInAsync(username, password);
                if (onlineResult.IsSuccess)
                {
                    // Store tokens
                    await SecureStorage.Default.SetAsync("access_token", onlineResult.AccessToken ?? string.Empty);
                    await SecureStorage.Default.SetAsync("id_token", onlineResult.IdToken ?? string.Empty);
                    await SecureStorage.Default.SetAsync("refresh_token", onlineResult.RefreshToken ?? string.Empty);

                    // Store credentials locally for offline use
                    await _localService.StoreUserCredentialsAsync(username, password);
                    await _localService.UpdateLastOnlineAuthAsync();

                    // Get and store user profile
                    try
                    {
                        var userAttributes = await _cognitoService.GetUserAttributesAsync(onlineResult.AccessToken!);
                        var profile = new LocalUserProfile
                        {
                            Username = username,
                            Email = userAttributes.FirstOrDefault(a => a.Name == "email")?.Value ?? username,
                            FirstName = userAttributes.FirstOrDefault(a => a.Name == "given_name")?.Value ?? "",
                            LastName = userAttributes.FirstOrDefault(a => a.Name == "family_name")?.Value ?? "",
                            LastUpdated = DateTime.UtcNow
                        };
                        await _localService.StoreUserProfileAsync(profile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not fetch user profile: {ex.Message}");
                    }

                    return new AuthenticationResult
                    {
                        IsSuccess = true,
                        IsOfflineMode = false,
                        Message = "Successfully signed in online"
                    };
                }
                else
                {
                    // Online auth failed, try offline if enabled
                    if (isOfflineModeEnabled && await _localService.ValidateLocalCredentialsAsync(username, password))
                    {
                        return new AuthenticationResult
                        {
                            IsSuccess = true,
                            IsOfflineMode = true,
                            Message = "Signed in using offline mode"
                        };
                    }

                    return new AuthenticationResult
                    {
                        IsSuccess = false,
                        IsOfflineMode = false,
                        Message = onlineResult.ErrorMessage ?? "Authentication failed"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Online authentication error: {ex.Message}");
                // Fall back to offline mode if enabled
                if (isOfflineModeEnabled && await _localService.ValidateLocalCredentialsAsync(username, password))
                {
                    return new AuthenticationResult
                    {
                        IsSuccess = true,
                        IsOfflineMode = true,
                        Message = "Network error - signed in using offline mode"
                    };
                }

                return new AuthenticationResult
                {
                    IsSuccess = false,
                    IsOfflineMode = false,
                    Message = "Network error and no offline access available"
                };
            }
        }
        else
        {
            // No network - try offline authentication
            if (isOfflineModeEnabled && await _localService.ValidateLocalCredentialsAsync(username, password))
            {
                return new AuthenticationResult
                {
                    IsSuccess = true,
                    IsOfflineMode = true,
                    Message = "Signed in using offline mode (no network)"
                };
            }

            return new AuthenticationResult
            {
                IsSuccess = false,
                IsOfflineMode = false,
                Message = "No network connection and no offline access available"
            };
        }
    }

    public async Task<bool> IsUserLoggedInAsync()
    {
        // Quick check: if offline mode is enabled and we have recent auth, return true immediately
        var isOfflineModeEnabled = await _localService.IsOfflineModeEnabledAsync();
        if (isOfflineModeEnabled)
        {
            var lastOnlineAuth = await _localService.GetLastOnlineAuthAsync();
            if (lastOnlineAuth.HasValue)
            {
                // Allow offline access for up to 30 days
                var daysSinceLastOnlineAuth = (DateTime.UtcNow - lastOnlineAuth.Value).TotalDays;
                if (daysSinceLastOnlineAuth <= 30)
                {
                    // We have valid offline access, no need for network check
                    return true;
                }
            }
        }

        // If we reach here, check network and tokens
        var isNetworkAvailable = await _localService.IsNetworkAvailableAsync();
        var accessToken = await SecureStorage.Default.GetAsync("access_token");

        // If we have a token and network, validate it online
        if (!string.IsNullOrEmpty(accessToken) && isNetworkAvailable)
        {
            try
            {
                var isValid = await _cognitoService.IsTokenValidAsync(accessToken);
                if (isValid)
                {
                    await _localService.UpdateLastOnlineAuthAsync();
                    return true;
                }

                // Token invalid, try to refresh
                var refreshToken = await SecureStorage.Default.GetAsync("refresh_token");
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var refreshResult = await _cognitoService.RefreshTokenAsync(refreshToken);
                    if (refreshResult.IsSuccess)
                    {
                        await SecureStorage.Default.SetAsync("access_token", refreshResult.AccessToken ?? string.Empty);
                        await SecureStorage.Default.SetAsync("id_token", refreshResult.IdToken ?? string.Empty);
                        if (!string.IsNullOrEmpty(refreshResult.RefreshToken))
                        {
                            await SecureStorage.Default.SetAsync("refresh_token", refreshResult.RefreshToken);
                        }
                        await _localService.UpdateLastOnlineAuthAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token validation error: {ex.Message}");
            }
        }

        return false;
    }

    public async Task<LocalUserProfile?> GetUserProfileAsync()
    {
        return await _localService.GetLocalUserProfileAsync();
    }

    public async Task SignOutAsync()
    {
        var accessToken = await SecureStorage.Default.GetAsync("access_token");
        var isNetworkAvailable = await _localService.IsNetworkAvailableAsync();

        // Try to sign out online if possible
        if (!string.IsNullOrEmpty(accessToken) && isNetworkAvailable)
        {
            try
            {
                await _cognitoService.SignOutAsync(accessToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Online sign out error: {ex.Message}");
            }
        }

        // Clear all stored data
        SecureStorage.Default.Remove("access_token");
        SecureStorage.Default.Remove("id_token");
        SecureStorage.Default.Remove("refresh_token");

        // Clear local auth data
        await _localService.ClearLocalDataAsync();

        // Clear database data
        await _sessionDatabase.ClearAllSessionsAsync();
        _sessionDatabase.ClearCache();
        
        // Reset TodayViewModel state to prevent hanging tasks on re-login
        try
        {
            var todayViewModel = _serviceProvider.GetService<TodayViewModel>();
            if (todayViewModel != null)
            {
                todayViewModel.Reset();
                Console.WriteLine("HybridAuthService: TodayViewModel reset completed");
            }
            else
            {
                Console.WriteLine("HybridAuthService: Warning - Could not find TodayViewModel service");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HybridAuthService: Error resetting TodayViewModel: {ex.Message}");
        }
        
        // Clear calendar cache to prevent cross-user data contamination
        try
        {
            var calendarDataService = _serviceProvider.GetService<CalendarDataService>();
            if (calendarDataService != null)
            {
                calendarDataService.ClearCache();
                Console.WriteLine("HybridAuthService: CalendarDataService cache cleared");
            }
            else
            {
                Console.WriteLine("HybridAuthService: Warning - Could not find CalendarDataService");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HybridAuthService: Error clearing CalendarDataService cache: {ex.Message}");
        }
        
        // Reset SimpleCalendarViewModel to clear user-specific calendar data
        try
        {
            // Clear static data directly (most important for cross-user contamination)
            SimpleCalendarViewModel.SelectedDayData = null;
            Console.WriteLine("HybridAuthService: Static SelectedDayData cleared");
            
            var simpleCalendarViewModel = _serviceProvider.GetService<SimpleCalendarViewModel>();
            if (simpleCalendarViewModel != null)
            {
                simpleCalendarViewModel.Reset();
                Console.WriteLine("HybridAuthService: SimpleCalendarViewModel reset completed");
            }
            else
            {
                Console.WriteLine("HybridAuthService: Warning - Could not find SimpleCalendarViewModel (expected for transient services)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HybridAuthService: Error resetting SimpleCalendarViewModel: {ex.Message}");
        }
        
        // Reset DayDetailViewModel to clear user-specific day data
        try
        {
            var dayDetailViewModel = _serviceProvider.GetService<DayDetailViewModel>();
            if (dayDetailViewModel != null)
            {
                dayDetailViewModel.Reset();
                Console.WriteLine("HybridAuthService: DayDetailViewModel reset completed");
            }
            else
            {
                Console.WriteLine("HybridAuthService: Warning - Could not find DayDetailViewModel");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HybridAuthService: Error resetting DayDetailViewModel: {ex.Message}");
        }
        
        // Reset CalendarPage loaded flag to ensure fresh data on next login
        try
        {
            Views.CalendarPage.ResetLoadedFlag();
            Console.WriteLine("HybridAuthService: CalendarPage loaded flag reset");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HybridAuthService: Error resetting CalendarPage loaded flag: {ex.Message}");
        }
        
        Console.WriteLine("HybridAuthService: SignOut completed, all data cleared");
    }

    public async Task<bool> IsOfflineModeAsync()
    {
        var isNetworkAvailable = await _localService.IsNetworkAvailableAsync();
        return !isNetworkAvailable || !(await _localService.IsNetworkAvailableAsync());
    }

    /// <summary>
    /// Quick authentication check that prioritizes local/offline data for faster startup
    /// </summary>
    public async Task<bool> QuickAuthCheckAsync()
    {
        try
        {
            // Check if offline mode is enabled first (fastest check)
            var isOfflineModeEnabled = await _localService.IsOfflineModeEnabledAsync();
            if (isOfflineModeEnabled)
            {
                var lastOnlineAuth = await _localService.GetLastOnlineAuthAsync();
                if (lastOnlineAuth.HasValue)
                {
                    var daysSinceLastOnlineAuth = (DateTime.UtcNow - lastOnlineAuth.Value).TotalDays;
                    if (daysSinceLastOnlineAuth <= 30)
                    {
                        return true; // Valid offline session
                    }
                }
            }

            // Quick token existence check (don't validate online yet)
            var accessToken = await SecureStorage.Default.GetAsync("access_token");
            return !string.IsNullOrEmpty(accessToken);
        }
        catch
        {
            return false;
        }
    }

    public async Task<AuthenticationResult> SignInWithAppleAsync(string idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                IsOfflineMode = false,
                Message = "Apple ID token is missing."
            };
        }

        try
        {
            // Get Cognito settings (with AppleClientId and AppleAppClientId)
            var settings = _serviceProvider.GetRequiredService<CognitoSettings>();

            // Use Cognito's InitiateAuth with the 'USER_SRP_AUTH' or 'CUSTOM_AUTH' flow for federated sign-in
            var request = new Amazon.CognitoIdentityProvider.Model.InitiateAuthRequest
            {
                AuthFlow = Amazon.CognitoIdentityProvider.AuthFlowType.USER_SRP_AUTH, // or CUSTOM_AUTH if configured
                ClientId = settings.AppleAppClientId ?? settings.AppleClientId ?? settings.AppClientId, // Use the Apple-specific Cognito App Client ID if present
                AuthParameters = new Dictionary<string, string>
                {
                    { "IDENTITY_PROVIDER", "Apple" }, // Cognito IdP name is 'Apple'
                    { "TOKEN", idToken }
                }
            };

            var response = await _cognitoService.Provider.InitiateAuthAsync(request);

            if (response.AuthenticationResult != null)
            {
                // Store tokens as needed
                await SecureStorage.Default.SetAsync("access_token", response.AuthenticationResult.AccessToken ?? string.Empty);
                await SecureStorage.Default.SetAsync("id_token", response.AuthenticationResult.IdToken ?? string.Empty);
                await SecureStorage.Default.SetAsync("refresh_token", response.AuthenticationResult.RefreshToken ?? string.Empty);

                return new AuthenticationResult
                {
                    IsSuccess = true,
                    IsOfflineMode = false,
                    Message = "Signed in with Apple successfully."
                };
            }
            else
            {
                return new AuthenticationResult
                {
                    IsSuccess = false,
                    IsOfflineMode = false,
                    Message = "Apple sign-in failed: No authentication result."
                };
            }
        }
        catch (Exception ex)
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                IsOfflineMode = false,
                Message = $"Apple sign-in error: {ex.Message}"
            };
        }
    }
}

public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public bool IsOfflineMode { get; set; }
    public string Message { get; set; } = string.Empty;
}
