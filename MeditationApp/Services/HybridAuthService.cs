using MeditationApp.Services;

namespace MeditationApp.Services;

public class HybridAuthService
{
    private readonly CognitoAuthService _cognitoService;
    private readonly LocalAuthService _localService;

    public HybridAuthService(CognitoAuthService cognitoService, LocalAuthService localService)
    {
        _cognitoService = cognitoService;
        _localService = localService;
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

        // Check if offline mode is available
        var isOfflineModeEnabled = await _localService.IsOfflineModeEnabledAsync();
        if (isOfflineModeEnabled)
        {
            var lastOnlineAuth = await _localService.GetLastOnlineAuthAsync();
            if (lastOnlineAuth.HasValue)
            {
                // Allow offline access for up to 30 days
                var daysSinceLastOnlineAuth = (DateTime.UtcNow - lastOnlineAuth.Value).TotalDays;
                return daysSinceLastOnlineAuth <= 30;
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
        await _localService.ClearLocalDataAsync();
    }

    public async Task<bool> IsOfflineModeAsync()
    {
        var isNetworkAvailable = await _localService.IsNetworkAvailableAsync();
        return !isNetworkAvailable || !(await _localService.IsNetworkAvailableAsync());
    }
}

public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public bool IsOfflineMode { get; set; }
    public string Message { get; set; } = string.Empty;
}
