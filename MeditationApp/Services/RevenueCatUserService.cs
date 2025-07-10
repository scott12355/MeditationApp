using System;
using System.Threading.Tasks;
#if IOS
using RevenueCat;
using Foundation;
#endif

namespace MeditationApp.Services;

public interface IRevenueCatUserService
{
    Task SetUserIdAsync(string userId);
    Task SetUserAttributesAsync(UserAttributes attributes);
    Task SetUserAttributesAsync(Dictionary<string, string> attributes);
    Task SetEmailAsync(string email);
    Task SetDisplayNameAsync(string displayName);
    Task SetPhoneNumberAsync(string phoneNumber);
    Task LogUserEventAsync(string eventName, Dictionary<string, object>? parameters = null);
}

public class RevenueCatUserService : IRevenueCatUserService
{
#if IOS
    /// <summary>
    /// Helper method to convert Dictionary<string, string> to NSDictionary<NSString, NSString>
    /// </summary>
    private static NSDictionary<NSString, NSString> ConvertToNSDictionary(Dictionary<string, string> dict)
    {
        var keys = new NSString[dict.Count];
        var values = new NSString[dict.Count];
        
        int i = 0;
        foreach (var kvp in dict)
        {
            keys[i] = new NSString(kvp.Key);
            values[i] = new NSString(kvp.Value);
            i++;
        }
        
        return NSDictionary<NSString, NSString>.FromObjectsAndKeys(values, keys);
    }
#endif

    /// <summary>
    /// Sets the user ID for RevenueCat analytics and customer support
    /// Call this after user authentication
    /// </summary>
    public async Task SetUserIdAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
#if IOS
            var tcs = new TaskCompletionSource<bool>();
            
            RCPurchases.SharedPurchases.LogIn(userId, (customerInfo, created, error) =>
            {
                if (error != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error setting user ID: {error.LocalizedDescription}");
                    tcs.SetException(new Exception(error.LocalizedDescription));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[RevenueCat] User ID set: {userId}, Created: {created}");
                    tcs.SetResult(true);
                }
            });
            
            await tcs.Task;
#else
            await Task.CompletedTask;
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] User ID would be set: {userId} (iOS only)");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error setting user ID: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets multiple user attributes at once (convenience overload)
    /// </summary>
    public Task SetUserAttributesAsync(Dictionary<string, string> attributes)
    {
        try
        {
#if IOS
            if (attributes != null && attributes.Count > 0)
            {
                var nsDict = ConvertToNSDictionary(attributes);
                RCPurchases.SharedPurchases.SetAttributes(nsDict);
                System.Diagnostics.Debug.WriteLine("[RevenueCat] User attributes set successfully");
            }
#else
            System.Diagnostics.Debug.WriteLine("[RevenueCat] User attributes would be set (iOS only)");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error setting user attributes: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets multiple user attributes at once
    /// </summary>
    public Task SetUserAttributesAsync(UserAttributes attributes)
    {
        try
        {
#if IOS
            var attributeDict = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(attributes.Email))
                attributeDict["$email"] = attributes.Email;
            
            if (!string.IsNullOrEmpty(attributes.DisplayName))
                attributeDict["$displayName"] = attributes.DisplayName;
            
            if (!string.IsNullOrEmpty(attributes.PhoneNumber))
                attributeDict["$phoneNumber"] = attributes.PhoneNumber;
            
            if (attributes.SignUpDate.HasValue)
                attributeDict["signup_date"] = attributes.SignUpDate.Value.ToString("yyyy-MM-dd");
            
            if (attributes.TotalMeditations.HasValue)
                attributeDict["total_meditations"] = attributes.TotalMeditations.Value.ToString();
            
            if (attributes.DaysStreakCount.HasValue)
                attributeDict["days_streak"] = attributes.DaysStreakCount.Value.ToString();
            
            if (!string.IsNullOrEmpty(attributes.FavoriteCategory))
                attributeDict["favorite_category"] = attributes.FavoriteCategory;
            
            if (attributes.LastActiveDate.HasValue)
                attributeDict["last_active"] = attributes.LastActiveDate.Value.ToString("yyyy-MM-dd");

            // Add custom attributes if provided
            if (attributes.CustomAttributes != null)
            {
                foreach (var customAttr in attributes.CustomAttributes)
                {
                    attributeDict[customAttr.Key] = customAttr.Value;
                }
            }

            if (attributeDict.Count > 0)
            {
                var nsDict = ConvertToNSDictionary(attributeDict);
                RCPurchases.SharedPurchases.SetAttributes(nsDict);
                System.Diagnostics.Debug.WriteLine("[RevenueCat] User attributes set successfully");
            }
#else
            System.Diagnostics.Debug.WriteLine("[RevenueCat] User attributes would be set (iOS only)");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error setting user attributes: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the user's email address for customer support
    /// </summary>
    public Task SetEmailAsync(string email)
    {
        if (string.IsNullOrEmpty(email)) return Task.CompletedTask;

        try
        {
#if IOS
            var attributes = new Dictionary<string, string>
            {
                ["$email"] = email
            };
            var nsDict = ConvertToNSDictionary(attributes);
            RCPurchases.SharedPurchases.SetAttributes(nsDict);
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Email set: {email}");
#else
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Email would be set: {email} (iOS only)");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error setting email: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the user's display name
    /// </summary>
    public Task SetDisplayNameAsync(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return Task.CompletedTask;

        try
        {
#if IOS
            var attributes = new Dictionary<string, string>
            {
                ["$displayName"] = displayName
            };
            var nsDict = ConvertToNSDictionary(attributes);
            RCPurchases.SharedPurchases.SetAttributes(nsDict);
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Display name set: {displayName}");
#else
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Display name would be set: {displayName} (iOS only)");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error setting display name: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the user's phone number
    /// </summary>
    public Task SetPhoneNumberAsync(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber)) return Task.CompletedTask;

        try
        {
#if IOS
            var attributes = new Dictionary<string, string>
            {
                ["$phoneNumber"] = phoneNumber
            };
            var nsDict = ConvertToNSDictionary(attributes);
            RCPurchases.SharedPurchases.SetAttributes(nsDict);
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Phone number set: {phoneNumber}");
#else
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Phone number would be set: {phoneNumber} (iOS only)");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error setting phone number: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs custom events for analytics using RevenueCat attributes
    /// Note: RevenueCat doesn't have direct event logging, but we can use attributes to track behavior
    /// </summary>
    public Task LogUserEventAsync(string eventName, Dictionary<string, object>? parameters = null)
    {
        try
        {
#if IOS
            var attributes = new Dictionary<string, string>();

            // Add timestamp for this event
            attributes[$"event_{eventName}_timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Add event parameters as attributes
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    attributes[$"event_{eventName}_{param.Key}"] = param.Value?.ToString() ?? "";
                }
            }

            var nsDict = ConvertToNSDictionary(attributes);
            RCPurchases.SharedPurchases.SetAttributes(nsDict);
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Event logged: {eventName}");
#else
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Event would be logged: {eventName} (iOS only)");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error logging event: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
}

/// <summary>
/// User attributes that can be sent to RevenueCat
/// </summary>
public class UserAttributes
{
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? SignUpDate { get; set; }
    public int? TotalMeditations { get; set; }
    public int? DaysStreakCount { get; set; }
    public string? FavoriteCategory { get; set; }
    public DateTime? LastActiveDate { get; set; }
    
    // You can add more custom attributes as needed
    public Dictionary<string, string>? CustomAttributes { get; set; }
}
