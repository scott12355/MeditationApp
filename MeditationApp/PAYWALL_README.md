# Custom Paywall Implementation

This document explains how to use the custom paywall system built for the Meditation App. The paywall integrates with RevenueCat to provide a seamless premium subscription experience.

## Overview

The paywall system consists of:
- **PaywallModal**: A beautiful, custom-designed modal page matching your app's design
- **PaywallViewModel**: Handles RevenueCat integration and subscription logic
- **PaywallService**: Service for managing paywall display and subscription checks
- **PremiumFeatureHelper**: Utility methods for easy integration throughout the app

## Features

✨ **Custom Design**: Matches your app's glassy, nature-themed design  
🛒 **Multiple Packages**: Displays all available subscription options  
⭐ **Popular Badge**: Highlights recommended packages (typically annual)  
💳 **Native Purchasing**: Uses RevenueCat for App Store integration  
🔄 **Restore Purchases**: Built-in restore functionality  
📱 **Cross-Platform**: Works on iOS (Android support can be added)  
🎨 **Animated**: Includes floating orbs and smooth animations  

## Quick Start

### 1. Basic Usage - From any ContentPage

```csharp
// Simple check - shows paywall if user doesn't have premium
bool hasPremium = await this.CheckPremiumAccessAsync("Advanced Analytics");

if (hasPremium)
{
    // User has premium access - proceed with feature
    await NavigateToPremiumFeature();
}
```

### 2. With Description - Show locked feature message first

```csharp
bool upgraded = await this.ShowPremiumLockedAsync(
    "Offline Downloads", 
    "Download meditations and listen anywhere, even without internet."
);

if (upgraded)
{
    await EnableOfflineFeature();
}
```

### 3. In ViewModels - Using Dependency Injection

```csharp
public class MyViewModel
{
    private readonly IPaywallService _paywallService;

    public MyViewModel(IPaywallService paywallService)
    {
        _paywallService = paywallService;
    }

    public async Task AccessPremiumFeature()
    {
        var result = await _paywallService.ShowPaywallAsync();
        
        if (result.WasPurchased || result.IsRestore)
        {
            // User now has premium access
            LoadPremiumContent();
        }
    }
}
```

## Integration Examples

### Button Click Handler

```csharp
private async void OnPremiumButtonClicked(object sender, EventArgs e)
{
    bool hasPremium = await this.CheckPremiumAccessAsync("Premium Meditations");
    
    if (hasPremium)
    {
        await Shell.Current.GoToAsync("PremiumMeditationsPage");
    }
}
```

### Command in ViewModel

```csharp
public ICommand AccessPremiumCommand => new Command(async () =>
{
    var hasAccess = await PremiumFeatureHelper.CheckPremiumAccessAsync("Premium Feature");
    if (hasAccess)
    {
        // Proceed with premium feature
    }
});
```

### Meditation Creation Feature (Example Implementation)

The meditation creation feature in TodayViewModel shows how to integrate premium checks:

```csharp
[RelayCommand]
private async Task RequestNewSession()
{
    // Check for premium access first
    var hasPremium = await PremiumFeatureHelper.CheckPremiumAccessAsync("Personalized Meditation Sessions");
    if (!hasPremium)
    {
        // User doesn't have premium or cancelled upgrade
        return;
    }

    // Continue with premium feature logic
    if (string.IsNullOrEmpty(SessionNotes))
    {
        // Show validation message
        return;
    }
    
    // Create the meditation session...
}
```

### Alternative: Show Feature Locked Dialog First

```csharp
private async void OnAdvancedFeatureClicked(object sender, EventArgs e)
{
    bool upgraded = await this.ShowPremiumLockedAsync(
        "Advanced Analytics", 
        "Get detailed insights about your meditation progress and personalized recommendations."
    );
    
    if (upgraded)
    {
        await EnableAdvancedAnalytics();
    }
}
```

## Implemented Premium Features

### Personalized Meditation Creation
- **Location**: TodayViewModel.RequestNewSession()
- **Feature**: Users can create personalized meditation sessions based on their mood and notes
- **UI Indicator**: Button shows "(Premium)" suffix
- **Behavior**: Shows paywall when non-premium users try to create sessions

### Future Premium Features
You can easily add premium restrictions to other features using the same pattern:

```csharp
// Example: Premium breathing exercises
[RelayCommand]
private async Task AccessAdvancedBreathing()
{
    var hasPremium = await PremiumFeatureHelper.CheckPremiumAccessAsync("Advanced Breathing Exercises");
    if (!hasPremium) return;
    
    // Show advanced breathing exercises
}

// Example: Premium meditation library
private async void OnPremiumLibraryTapped(object sender, EventArgs e)
{
    bool hasAccess = await this.ShowPremiumLockedAsync(
        "Premium Meditation Library",
        "Access hundreds of exclusive guided meditations and ambient soundscapes."
    );
    
    if (hasAccess)
    {
        await NavigateToPremiumLibrary();
    }
}
```

## Paywall Design

The paywall features:
- **Header**: Premium icon with app-matching background
- **Benefits**: List of premium features with emojis
- **Packages**: Beautiful cards showing subscription options
- **Popular Badge**: Highlights best value option
- **Purchase Button**: Large, prominent call-to-action
- **Restore**: Secondary button for existing customers
- **Legal**: Terms and Privacy links

## Customization

### Colors and Styling
The paywall uses your app's existing color scheme:
- `{StaticResource Primary}` - For accent colors and buttons
- `{StaticResource GlassyWhite}` - For card backgrounds
- `{StaticResource toneGray}` - For secondary text

### Package Display
The system automatically:
- Detects monthly/annual packages from RevenueCat identifiers
- Marks annual packages as "MOST POPULAR"
- Formats prices using store localization
- Sorts packages (popular first, then by price)

### Animation
Includes floating orb animations matching your TodayPage design.

## RevenueCat User Metadata

### Overview
The `RevenueCatUserService` allows you to send user metadata to RevenueCat for better analytics, customer support, and personalization. This data helps you understand your users and provide better support when needed.

### Setting User ID and Basic Info

```csharp
// Inject the service in your ViewModel/Service
private readonly IRevenueCatUserService _revenueCatService;

// After successful login
await _revenueCatService.SetUserIdAsync(userId);
await _revenueCatService.SetEmailAsync(user.Email);
await _revenueCatService.SetDisplayNameAsync($"{user.FirstName} {user.LastName}");
```

### Setting Multiple Attributes at Once

```csharp
var attributes = new UserAttributes
{
    Email = user.Email,
    DisplayName = $"{user.FirstName} {user.LastName}",
    SignUpDate = user.CreatedAt,
    TotalMeditations = userStats.TotalSessions,
    DaysStreakCount = userStats.CurrentStreak,
    FavoriteCategory = "Mindfulness",
    LastActiveDate = DateTime.UtcNow
};

await _revenueCatService.SetUserAttributesAsync(attributes);
```

### Practical Integration Examples

#### In LoginViewModel (After Successful Authentication)
```csharp
public async Task HandleSuccessfulLogin(string userId, string email, string displayName)
{
    // Set up RevenueCat user
    await _revenueCatService.SetupNewUserAsync(userId, email, displayName);
    
    // Continue with app navigation...
}
```

#### In TodayViewModel (After Session Completion)
```csharp
private async Task OnSessionCompleted()
{
    // Update your local stats
    var totalSessions = await _database.GetTotalSessionsAsync();
    var currentStreak = await _database.GetCurrentStreakAsync();
    
    // Update RevenueCat with latest stats
    await _revenueCatService.UpdateActivityAsync(totalSessions, currentStreak);
    
    // Log the session completion event
    var eventData = new Dictionary<string, object>
    {
        ["session_type"] = "personalized_meditation",
        ["duration_minutes"] = 10
    };
    await _revenueCatService.LogUserEventAsync("session_completed", eventData);
}
```

#### In SettingsViewModel (Profile Updates)
```csharp
private async Task UpdateProfile(string newEmail, string newDisplayName)
{
    // Update in your backend/local storage
    await UpdateUserProfile(newEmail, newDisplayName);
    
    // Sync to RevenueCat
    await _revenueCatService.SetEmailAsync(newEmail);
    await _revenueCatService.SetDisplayNameAsync(newDisplayName);
}
```

### Available User Attributes

#### Standard RevenueCat Attributes
- `$email` - User's email address (for customer support)
- `$displayName` - User's display name
- `$phoneNumber` - User's phone number

#### Custom Meditation App Attributes
- `signup_date` - When user created account
- `total_meditations` - Total completed meditation sessions
- `days_streak` - Current consecutive days streak
- `favorite_category` - User's preferred meditation category
- `last_active` - Last time user opened the app

### Benefits of User Metadata

1. **Customer Support**: When users contact support, you can see their meditation history and subscription status
2. **Analytics**: Understand user behavior and engagement patterns
3. **Personalization**: Use data to offer targeted promotions or content
4. **Cohort Analysis**: Group users by signup date, usage patterns, etc.
5. **Churn Prevention**: Identify users at risk and target them with special offers

### Best Practices

1. **Set User ID Early**: Call `SetUserIdAsync()` immediately after authentication
2. **Update Incrementally**: Don't send all attributes every time, update as needed
3. **Handle Errors Gracefully**: RevenueCat metadata is supplementary, don't break app flow if it fails
4. **Respect Privacy**: Only send data that improves user experience or support
5. **Regular Updates**: Update activity data periodically (daily/weekly) not after every action

## RevenueCat Setup

Ensure your RevenueCat offerings include packages with identifiers containing:
- `"monthly"` or `"month"` for monthly subscriptions
- `"yearly"`, `"year"`, or `"annual"` for annual subscriptions

## Error Handling

The system gracefully handles:
- Network errors when loading offerings
- Purchase failures or cancellations
- Missing RevenueCat configuration
- Platform availability (iOS-focused)

## Testing

### Testing the Paywall UI
```csharp
// Show paywall regardless of subscription status
var paywallModal = new PaywallModal();
var result = await paywallModal.ShowAsync();
```

### Testing Subscription Status
```csharp
var paywallService = new PaywallService();
bool hasSubscription = await paywallService.CheckSubscriptionStatusAsync();
```

## Platform Support

- **iOS**: Full support with RevenueCat integration
- **Android**: UI ready, RevenueCat integration can be added
- **Other platforms**: Graceful fallback with appropriate messaging

## Troubleshooting

### Common Issues

1. **No offerings available**: Check RevenueCat dashboard configuration
2. **Purchase not working**: Verify App Store Connect setup
3. **Styling issues**: Ensure color resources are properly defined
4. **DI errors**: Verify PaywallService is registered in MauiProgram.cs

### Debug Information

The system logs debug information for:
- Available packages and pricing
- Purchase attempts and results
- Subscription status checks
- Error conditions

Check the debug console for detailed information during development.

## Migration from Old System

The old RevenueCat implementation in SettingsViewModel has been replaced with the new PaywallModal system. The new system provides:
- Better user experience with custom UI
- Consistent design matching your app
- Easier integration throughout the app
- Better error handling and loading states

## Support

For issues related to:
- **RevenueCat integration**: Check RevenueCat documentation
- **App Store setup**: Verify App Store Connect configuration
- **UI customization**: Modify PaywallModal.xaml
- **Logic changes**: Update PaywallViewModel.cs
