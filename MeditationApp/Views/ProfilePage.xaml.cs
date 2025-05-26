using Amazon.CognitoIdentityProvider.Model;
using MeditationApp.Services;

namespace MeditationApp.Views;

public partial class ProfilePage : ContentPage
{
    private readonly HybridAuthService _hybridAuthService;
    private readonly CognitoAuthService _cognitoAuthService;

    public ProfilePage(HybridAuthService hybridAuthService, CognitoAuthService cognitoAuthService)
    {
        InitializeComponent();
        _hybridAuthService = hybridAuthService;
        _cognitoAuthService = cognitoAuthService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadUserProfile();
    }

    private async Task LoadUserProfile()
    {
        try
        {
            // Check if we're in offline mode
            var isOfflineMode = await _hybridAuthService.IsOfflineModeAsync();
            OfflineIndicator.IsVisible = isOfflineMode;

            if (isOfflineMode)
            {
                // Load from local storage
                var localProfile = await _hybridAuthService.GetUserProfileAsync();
                if (localProfile != null)
                {
                    UsernameLabel.Text = localProfile.Username;
                    EmailLabel.Text = localProfile.Email;
                    FirstNameLabel.Text = localProfile.FirstName;
                    LastNameLabel.Text = localProfile.LastName;
                }
                else
                {
                    UsernameLabel.Text = "Not available offline";
                    EmailLabel.Text = "Not available offline";
                    FirstNameLabel.Text = "Not available offline";
                    LastNameLabel.Text = "Not available offline";
                }
            }
            else
            {
                // Load from online service
                var accessToken = await SecureStorage.Default.GetAsync("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    await LoadOnlineUserProfile(accessToken);
                }
                else
                {
                    // Fallback to local profile
                    var localProfile = await _hybridAuthService.GetUserProfileAsync();
                    if (localProfile != null)
                    {
                        UsernameLabel.Text = localProfile.Username;
                        EmailLabel.Text = localProfile.Email;
                        FirstNameLabel.Text = localProfile.FirstName;
                        LastNameLabel.Text = localProfile.LastName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load profile: {ex.Message}", "OK");
            // Fallback to local profile
            var localProfile = await _hybridAuthService.GetUserProfileAsync();
            if (localProfile != null)
            {
                UsernameLabel.Text = localProfile.Username;
                EmailLabel.Text = localProfile.Email;
                FirstNameLabel.Text = localProfile.FirstName;
                LastNameLabel.Text = localProfile.LastName;
            }
        }
    }

    private async Task LoadOnlineUserProfile(string accessToken)
    {
        try
        {
            var userAttributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);

            string username = string.Empty;
            string email = string.Empty;
            string firstName = string.Empty;
            string lastName = string.Empty;

            foreach (var attribute in userAttributes)
            {
                switch (attribute.Name)
                {
                    case "email":
                        email = attribute.Value;
                        break;
                    case "preferred_username":
                    case "username":
                    case "sub":
                        username = attribute.Value;
                        break;
                    case "given_name":
                        firstName = attribute.Value;
                        break;
                    case "family_name":
                        lastName = attribute.Value;
                        break;
                }
            }

            UsernameLabel.Text = !string.IsNullOrEmpty(username) ? username : email;
            EmailLabel.Text = !string.IsNullOrEmpty(email) ? email : "Not available";
            FirstNameLabel.Text = !string.IsNullOrEmpty(firstName) ? firstName : "Not available";
            LastNameLabel.Text = !string.IsNullOrEmpty(lastName) ? lastName : "Not available";

            // Update local profile with fresh data
            var profile = new LocalUserProfile
            {
                Username = username,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                LastUpdated = DateTime.UtcNow
            };
            
            // Store the updated profile locally
            var localService = new LocalAuthService();
            await localService.StoreUserProfileAsync(profile);
        }
        catch (NotAuthorizedException)
        {
            await TryRefreshTokenAndReload();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("access token") || ex.Message.Contains("revoked") || ex.Message.Contains("expired"))
            {
                await TryRefreshTokenAndReload();
            }
            else
            {
                throw;
            }
        }
    }

    private async Task TryRefreshTokenAndReload()
    {
        try
        {
            var refreshToken = await SecureStorage.Default.GetAsync("refresh_token");
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refreshResult = await _cognitoAuthService.RefreshTokenAsync(refreshToken);
                if (refreshResult.IsSuccess)
                {
                    await SecureStorage.Default.SetAsync("access_token", refreshResult.AccessToken ?? string.Empty);
                    await SecureStorage.Default.SetAsync("id_token", refreshResult.IdToken ?? string.Empty);
                    if (!string.IsNullOrEmpty(refreshResult.RefreshToken))
                    {
                        await SecureStorage.Default.SetAsync("refresh_token", refreshResult.RefreshToken);
                    }
                    
                    await LoadOnlineUserProfile(refreshResult.AccessToken ?? string.Empty);
                    return;
                }
                else
                {
                    // Check if refresh token is expired/invalid - if so, logout user
                    if (IsRefreshTokenExpiredError(refreshResult.ErrorMessage))
                    {
                        await DisplayAlert("Session Expired", "Your session has expired. Please log in again.", "OK");
                        await ClearTokensAndNavigateToLogin();
                        return;
                    }
                }
            }
            else
            {
                // No refresh token available - logout user
                await DisplayAlert("Session Expired", "Your session has expired. Please log in again.", "OK");
                await ClearTokensAndNavigateToLogin();
                return;
            }
            
            // Refresh failed - show offline data if available
            var localProfile = await _hybridAuthService.GetUserProfileAsync();
            if (localProfile != null)
            {
                OfflineIndicator.IsVisible = true;
                UsernameLabel.Text = localProfile.Username;
                EmailLabel.Text = localProfile.Email;
                FirstNameLabel.Text = localProfile.FirstName;
                LastNameLabel.Text = localProfile.LastName;
            }
            else
            {
                await DisplayAlert("Session Expired", "Your session has expired and no offline data is available. Please log in again.", "OK");
                await ClearTokensAndNavigateToLogin();
            }
        }
        catch (Exception ex)
        {
            // Check if the exception indicates refresh token expiry
            if (IsRefreshTokenExpiredError(ex.Message))
            {
                await DisplayAlert("Session Expired", "Your session has expired. Please log in again.", "OK");
                await ClearTokensAndNavigateToLogin();
                return;
            }
            
            // Show offline data if available
            var localProfile = await _hybridAuthService.GetUserProfileAsync();
            if (localProfile != null)
            {
                OfflineIndicator.IsVisible = true;
                UsernameLabel.Text = localProfile.Username;
                EmailLabel.Text = localProfile.Email;
                FirstNameLabel.Text = localProfile.FirstName;
                LastNameLabel.Text = localProfile.LastName;
            }
            else
            {
                await DisplayAlert("Error", "Cannot load profile data. Please log in again.", "OK");
                await ClearTokensAndNavigateToLogin();
            }
        }
    }

    private async Task ClearTokensAndNavigateToLogin()
    {
        await _hybridAuthService.SignOutAsync();
        await Shell.Current.GoToAsync("///LoginPage");
    }

    private async void OnSignOutClicked(object sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Signing out...";
            SignOutButton.IsEnabled = false;

            await _hybridAuthService.SignOutAsync();
            await Shell.Current.GoToAsync("///LoginPage");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            SignOutButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Helper method to determine if an error message indicates refresh token expiry
    /// </summary>
    private static bool IsRefreshTokenExpiredError(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return false;
            
        var message = errorMessage.ToLowerInvariant();
        return message.Contains("refresh token") && message.Contains("expired") ||
               message.Contains("refresh token") && message.Contains("invalid") ||
               message.Contains("token_expired") ||
               message.Contains("refresh_token_expired") ||
               message.Contains("notauthorized") ||
               message.Contains("invalid_grant");
    }
}
