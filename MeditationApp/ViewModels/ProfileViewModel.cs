using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using MeditationApp.Services;

namespace MeditationApp.ViewModels;

// Event args for confirmation dialogs
public class ConfirmationDialogEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ConfirmText { get; set; } = string.Empty;
    public string CancelText { get; set; } = string.Empty;
    public TaskCompletionSource<bool> TaskCompletionSource { get; set; } = new();
}

public class ProfileViewModel : INotifyPropertyChanged
{
    private readonly HybridAuthService _hybridAuthService;
    private readonly CognitoAuthService _cognitoAuthService;
    private readonly GraphQLService _graphQLService;

    private string _username = "Loading...";
    private string _email = "Loading...";
    private string _firstName = "Loading...";
    private string _lastName = "Loading...";
    private string _statusMessage = string.Empty;
    private bool _isOfflineMode;
    private bool _isDeleteButtonEnabled = true;
    private string _deleteButtonText = "Delete My Account";

    public ProfileViewModel(
        HybridAuthService hybridAuthService,
        CognitoAuthService cognitoAuthService,
        GraphQLService graphQLService)
    {
        _hybridAuthService = hybridAuthService;
        _cognitoAuthService = cognitoAuthService;
        _graphQLService = graphQLService;

        DeleteAccountCommand = new Command(async () => await DeleteAccountAsync());
        LoadProfileCommand = new Command(async () => await LoadUserProfileAsync());
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string FirstName
    {
        get => _firstName;
        set => SetProperty(ref _firstName, value);
    }

    public string LastName
    {
        get => _lastName;
        set => SetProperty(ref _lastName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsOfflineMode
    {
        get => _isOfflineMode;
        set => SetProperty(ref _isOfflineMode, value);
    }

    public bool IsDeleteButtonEnabled
    {
        get => _isDeleteButtonEnabled;
        set => SetProperty(ref _isDeleteButtonEnabled, value);
    }

    public string DeleteButtonText
    {
        get => _deleteButtonText;
        set => SetProperty(ref _deleteButtonText, value);
    }

    public ICommand DeleteAccountCommand { get; }
    public ICommand LoadProfileCommand { get; }

    public event EventHandler<string>? ShowAlert;
    public event EventHandler<ConfirmationDialogEventArgs>? ShowConfirmationDialog;

    public async Task LoadUserProfileAsync()
    {
        try
        {
            // Check if we're in offline mode
            var isOfflineMode = await _hybridAuthService.IsOfflineModeAsync();
            IsOfflineMode = isOfflineMode;

            if (isOfflineMode)
            {
                // Load from local storage
                var localProfile = await _hybridAuthService.GetUserProfileAsync();
                if (localProfile != null)
                {
                    Username = localProfile.Username;
                    Email = localProfile.Email;
                    FirstName = localProfile.FirstName;
                    LastName = localProfile.LastName;
                }
                else
                {
                    Username = "Not available offline";
                    Email = "Not available offline";
                    FirstName = "Not available offline";
                    LastName = "Not available offline";
                }
            }
            else
            {
                // Load from online service
                var accessToken = await SecureStorage.Default.GetAsync("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    await LoadOnlineUserProfileAsync(accessToken);
                }
                else
                {
                    // Fallback to local profile
                    var localProfile = await _hybridAuthService.GetUserProfileAsync();
                    if (localProfile != null)
                    {
                        Username = localProfile.Username;
                        Email = localProfile.Email;
                        FirstName = localProfile.FirstName;
                        LastName = localProfile.LastName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowAlert?.Invoke(this, $"Failed to load profile: {ex.Message}");
            // Fallback to local profile
            var localProfile = await _hybridAuthService.GetUserProfileAsync();
            if (localProfile != null)
            {
                Username = localProfile.Username;
                Email = localProfile.Email;
                FirstName = localProfile.FirstName;
                LastName = localProfile.LastName;
            }
        }
    }

    private async Task LoadOnlineUserProfileAsync(string accessToken)
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

            Username = !string.IsNullOrEmpty(username) ? username : email;
            Email = !string.IsNullOrEmpty(email) ? email : "Not available";
            FirstName = !string.IsNullOrEmpty(firstName) ? firstName : "Not available";
            LastName = !string.IsNullOrEmpty(lastName) ? lastName : "Not available";

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
        catch (Amazon.CognitoIdentityProvider.Model.NotAuthorizedException)
        {
            await TryRefreshTokenAndReloadAsync();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("access token") || ex.Message.Contains("revoked") || ex.Message.Contains("expired"))
            {
                await TryRefreshTokenAndReloadAsync();
            }
            else
            {
                throw;
            }
        }
    }

    private async Task TryRefreshTokenAndReloadAsync()
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

                    await LoadOnlineUserProfileAsync(refreshResult.AccessToken ?? string.Empty);
                    return;
                }
                else
                {
                    // Check if refresh token is expired/invalid - if so, logout user
                    if (IsRefreshTokenExpiredError(refreshResult.ErrorMessage))
                    {
                        ShowAlert?.Invoke(this, "Your session has expired. Please log in again.");
                        await ClearTokensAndNavigateToLoginAsync();
                        return;
                    }
                }
            }
            else
            {
                // No refresh token available - logout user
                ShowAlert?.Invoke(this, "Your session has expired. Please log in again.");
                await ClearTokensAndNavigateToLoginAsync();
                return;
            }

            // Refresh failed - show offline data if available
            var localProfile = await _hybridAuthService.GetUserProfileAsync();
            if (localProfile != null)
            {
                IsOfflineMode = true;
                Username = localProfile.Username;
                Email = localProfile.Email;
                FirstName = localProfile.FirstName;
                LastName = localProfile.LastName;
            }
            else
            {
                ShowAlert?.Invoke(this, "Your session has expired and no offline data is available. Please log in again.");
                await ClearTokensAndNavigateToLoginAsync();
            }
        }
        catch (Exception ex)
        {
            // Check if the exception indicates refresh token expiry
            if (IsRefreshTokenExpiredError(ex.Message))
            {
                ShowAlert?.Invoke(this, "Your session has expired. Please log in again.");
                await ClearTokensAndNavigateToLoginAsync();
                return;
            }

            // Show offline data if available
            var localProfile = await _hybridAuthService.GetUserProfileAsync();
            if (localProfile != null)
            {
                IsOfflineMode = true;
                Username = localProfile.Username;
                Email = localProfile.Email;
                FirstName = localProfile.FirstName;
                LastName = localProfile.LastName;
            }
            else
            {
                ShowAlert?.Invoke(this, "Cannot load profile data. Please log in again.");
                await ClearTokensAndNavigateToLoginAsync();
            }
        }
    }

    private async Task DeleteAccountAsync()
    {
        try
        {
            // Show first confirmation dialog
            var firstConfirmationArgs = new ConfirmationDialogEventArgs
            {
                Title = "Delete Account",
                Message = "Are you absolutely sure you want to delete your account? This action cannot be undone and all your data will be permanently lost.",
                ConfirmText = "Delete",
                CancelText = "Cancel"
            };

            ShowConfirmationDialog?.Invoke(this, firstConfirmationArgs);
            bool firstConfirm = await firstConfirmationArgs.TaskCompletionSource.Task;

            if (!firstConfirm)
                return;

            // Show second confirmation dialog for extra safety
            var secondConfirmationArgs = new ConfirmationDialogEventArgs
            {
                Title = "Final Confirmation",
                Message = "This is your last chance to cancel. Once deleted, your account and all meditation data cannot be recovered. Are you sure you want to proceed?",
                ConfirmText = "Yes, Delete Forever",
                CancelText = "Cancel"
            };

            ShowConfirmationDialog?.Invoke(this, secondConfirmationArgs);
            bool finalConfirm = await secondConfirmationArgs.TaskCompletionSource.Task;

            if (!finalConfirm)
                return;

            // Disable the button and show loading state
            IsDeleteButtonEnabled = false;
            DeleteButtonText = "Deleting Account...";

            // Get current user ID from user attributes
            string userID = await GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(userID))
            {
                ShowAlert?.Invoke(this, "Unable to identify current user. Please try logging out and back in.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Delete account - UserID: {userID}");

            // Call the delete account GraphQL mutation
            string deleteAccountMutation = @"
                mutation DeleteAccount($userID: ID!) {
                    deleteUserAccount(userID: $userID) {
                        userID
                    }
                }";

            var variables = new { userID };
            System.Diagnostics.Debug.WriteLine($"Delete account - Variables: {System.Text.Json.JsonSerializer.Serialize(variables)}");
            
            var response = await _graphQLService.QueryAsync(deleteAccountMutation, variables);

            // Only check for HTTP status 200
            if (response != null)
            {
                // Clear all local data using existing sign out functionality
                await _hybridAuthService.SignOutAsync();

                // Show success message
                ShowAlert?.Invoke(this, "Your account deletion request has been processed. All local data has been cleared.");
                // sign out 
                await _hybridAuthService.SignOutAsync();
                
                // Properly clear all user session data using HybridAuthService
                await _hybridAuthService.SignOutAsync();
                
                if (Application.Current != null)
                {
                    var appShell = new AppShell();
                    Application.Current.MainPage = appShell;
                    await appShell.GoToAsync("OnboardingPage1");
                }

            
            }
            else
            {
                ShowAlert?.Invoke(this, "Failed to delete account. Please try again or contact support.");
            }
        }
        catch (UnauthorizedAccessException)
        {
            ShowAlert?.Invoke(this, "Your session has expired. Please log in again.");
            await _hybridAuthService.SignOutAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current.MainPage = new AppShell(); // or new OnboardingPage1()
            });
        }
        catch (Exception ex)
        {
            ShowAlert?.Invoke(this, $"An error occurred while deleting your account: {ex.Message}");
        }
        finally
        {
            // Re-enable the button
            IsDeleteButtonEnabled = true;
            DeleteButtonText = "Delete My Account";
        }
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        try
        {
            var accessToken = await SecureStorage.Default.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                System.Diagnostics.Debug.WriteLine("GetCurrentUserIdAsync: No access token found");
                return string.Empty;
            }

            var userAttributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
            System.Diagnostics.Debug.WriteLine($"GetCurrentUserIdAsync: Found {userAttributes.Count} attributes");
            
            foreach (var attr in userAttributes)
            {
                System.Diagnostics.Debug.WriteLine($"GetCurrentUserIdAsync: Attribute - {attr.Name}: {attr.Value}");
            }
            
            var subAttribute = userAttributes.FirstOrDefault(attr => attr.Name == "sub");
            var userId = subAttribute?.Value ?? string.Empty;
            System.Diagnostics.Debug.WriteLine($"GetCurrentUserIdAsync: Final userID: '{userId}'");
            
            return userId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting user ID: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task ClearTokensAndNavigateToLoginAsync()
    {
        await _hybridAuthService.SignOutAsync();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Application.Current.MainPage = new AppShell(); // or new OnboardingPage1()
        });
    }

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

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
