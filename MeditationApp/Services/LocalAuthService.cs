using System.Security.Cryptography;
using System.Text;

namespace MeditationApp.Services;

public class LocalAuthService
{
    private const string UserCredentialsKey = "local_user_credentials";
    private const string UserProfileKey = "local_user_profile";
    private const string LastOnlineAuthKey = "last_online_auth";
    private const string OfflineModeEnabledKey = "offline_mode_enabled";

    public async Task<bool> StoreUserCredentialsAsync(string username, string password)
    {
        try
        {
            var hashedPassword = HashPassword(password);
            var credentials = new LocalUserCredentials
            {
                Username = username,
                HashedPassword = hashedPassword,
                CreatedAt = DateTime.UtcNow
            };

            var json = System.Text.Json.JsonSerializer.Serialize(credentials);
            await SecureStorage.Default.SetAsync(UserCredentialsKey, json);
            await SecureStorage.Default.SetAsync(OfflineModeEnabledKey, "true");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing credentials: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ValidateLocalCredentialsAsync(string username, string password)
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(UserCredentialsKey);
            if (string.IsNullOrEmpty(json))
                return false;

            var credentials = System.Text.Json.JsonSerializer.Deserialize<LocalUserCredentials>(json);
            if (credentials == null)
                return false;

            var hashedPassword = HashPassword(password);
            return credentials.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                   credentials.HashedPassword == hashedPassword;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating local credentials: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsOfflineModeEnabledAsync()
    {
        var enabled = await SecureStorage.Default.GetAsync(OfflineModeEnabledKey);
        return !string.IsNullOrEmpty(enabled) && enabled == "true";
    }

    public async Task StoreUserProfileAsync(LocalUserProfile profile)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(profile);
            await SecureStorage.Default.SetAsync(UserProfileKey, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing user profile: {ex.Message}");
        }
    }

    public async Task<LocalUserProfile?> GetLocalUserProfileAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(UserProfileKey);
            if (string.IsNullOrEmpty(json))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<LocalUserProfile>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving user profile: {ex.Message}");
            return null;
        }
    }

    public async Task UpdateLastOnlineAuthAsync()
    {
        await SecureStorage.Default.SetAsync(LastOnlineAuthKey, DateTime.UtcNow.ToString("O"));
    }

    public async Task<DateTime?> GetLastOnlineAuthAsync()
    {
        try
        {
            var dateString = await SecureStorage.Default.GetAsync(LastOnlineAuthKey);
            if (string.IsNullOrEmpty(dateString))
                return null;

            return DateTime.Parse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
        catch
        {
            return null;
        }
    }

    public Task<bool> IsNetworkAvailableAsync()
    {
        try
        {
            var current = Connectivity.Current.NetworkAccess;
            return Task.FromResult(current == NetworkAccess.Internet);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task ClearLocalDataAsync()
    {
        SecureStorage.Default.Remove(UserCredentialsKey);
        SecureStorage.Default.Remove(UserProfileKey);
        SecureStorage.Default.Remove(LastOnlineAuthKey);
        SecureStorage.Default.Remove(OfflineModeEnabledKey);
        return Task.CompletedTask;
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "MeditationAppSalt"));
        return Convert.ToBase64String(hashedBytes);
    }
}

public class LocalUserCredentials
{
    public string Username { get; set; } = string.Empty;
    public string HashedPassword { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class LocalUserProfile
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
