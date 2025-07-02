using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MeditationApp.Services;

#if IOS
using RevenueCat;
#endif

namespace MeditationApp.ViewModels;

public class PaywallViewModel : INotifyPropertyChanged
{
    private readonly IRevenueCatUserService _revenueCatUserService;
    private bool _isLoading = true;
    private string _errorMessage = string.Empty;
    private bool _hasError = false;
    private SubscriptionPackage? _selectedPackage;

    public ObservableCollection<SubscriptionPackage> AvailablePackages { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsContentVisible));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            _hasError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsContentVisible));
        }
    }

    public bool IsContentVisible => !IsLoading && !HasError;

    public SubscriptionPackage? SelectedPackage
    {
        get => _selectedPackage;
        set
        {
            _selectedPackage = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand SelectPackageCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand RestorePurchasesCommand { get; }

    public event EventHandler? PaywallClosed;
    public event EventHandler<SubscriptionPackage>? PackageSelected;
    public event EventHandler<PurchaseResult>? PurchaseCompleted;

    public PaywallViewModel(IRevenueCatUserService revenueCatUserService)
    {
        _revenueCatUserService = revenueCatUserService;
        CloseCommand = new Command(OnClose);
        SelectPackageCommand = new Command<SubscriptionPackage>(OnSelectPackage);
        RetryCommand = new Command(async () => await LoadOfferingsAsync());
        RestorePurchasesCommand = new Command(async () => await OnRestorePurchasesAsync());
        
        _ = LoadOfferingsAsync();
        
        // Debug what's stored
        _ = DebugStoredUserDataAsync();
    }

    private void OnClose()
    {
        PaywallClosed?.Invoke(this, EventArgs.Empty);
    }

    private async void OnSelectPackage(SubscriptionPackage package)
    {
        SelectedPackage = package;
        PackageSelected?.Invoke(this, package);
        
        // Immediately start purchase flow when package is selected
        await OnPurchaseAsync();
    }

    private async Task OnPurchaseAsync()
    {
        if (SelectedPackage == null) return;

        try
        {
            IsLoading = true;
            var result = await PurchasePackageAsync(SelectedPackage.PackageId);
            PurchaseCompleted?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Purchase error: {ex.Message}");
            PurchaseCompleted?.Invoke(this, new PurchaseResult 
            { 
                IsSuccess = false, 
                ErrorMessage = ex.Message 
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OnRestorePurchasesAsync()
    {
        try
        {
            IsLoading = true;
#if IOS
            var tcs = new TaskCompletionSource<bool>();
            
            RCPurchases.SharedPurchases.RestorePurchases((customerInfo, error) =>
            {
                if (error != null)
                {
                    PurchaseCompleted?.Invoke(this, new PurchaseResult 
                    { 
                        IsSuccess = false, 
                        ErrorMessage = $"Error restoring purchases: {error.LocalizedDescription}" 
                    });
                    tcs.SetException(new Exception(error.LocalizedDescription));
                }
                else if (customerInfo.Entitlements.Active.Count > 0)
                {
                    PurchaseCompleted?.Invoke(this, new PurchaseResult 
                    { 
                        IsSuccess = true, 
                        IsRestore = true,
                        Message = "Purchases restored successfully!" 
                    });
                    tcs.SetResult(true);
                }
                else
                {
                    PurchaseCompleted?.Invoke(this, new PurchaseResult 
                    { 
                        IsSuccess = false, 
                        ErrorMessage = "No active subscriptions found to restore." 
                    });
                    tcs.SetResult(false);
                }
            });
            
            await tcs.Task;
#else
            await Task.Delay(100); // Placeholder for other platforms
            PurchaseCompleted?.Invoke(this, new PurchaseResult 
            { 
                IsSuccess = false, 
                ErrorMessage = "Restore purchases not available on this platform." 
            });
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Restore purchases error: {ex.Message}");
            PurchaseCompleted?.Invoke(this, new PurchaseResult 
            { 
                IsSuccess = false, 
                ErrorMessage = $"Error restoring purchases: {ex.Message}" 
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadOfferingsAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            AvailablePackages.Clear();

#if IOS
            var tcs = new TaskCompletionSource<RCOfferings>();
            
            RCPurchases.SharedPurchases.GetOfferings((offerings, error) =>
            {
                if (error != null)
                {
                    tcs.SetException(new Exception(error.LocalizedDescription));
                }
                else
                {
                    tcs.SetResult(offerings);
                }
            });
            
            var offerings = await tcs.Task;
            
            if (offerings?.Current == null)
            {
                HasError = true;
                ErrorMessage = "No subscription plans available at this time.";
                return;
            }

            foreach (var package in offerings.Current.AvailablePackages)
            {
                var subscriptionPackage = new SubscriptionPackage
                {
                    PackageId = package.Identifier,
                    Title = GetPackageTitle(package.Identifier),
                    Price = (decimal)package.StoreProduct.Price.DoubleValue,
                    PriceString = package.StoreProduct.LocalizedPriceString ?? package.StoreProduct.Price.ToString(),
                    Description = GetPackageDescription(package.Identifier),
                    IsPopular = IsPopularPackage(package.Identifier),
                    PeriodString = GetPackagePeriod(package.Identifier),
                    RevenueCatPackage = package
                };
                
                AvailablePackages.Add(subscriptionPackage);
            }

            // Sort packages: popular first, then by price (monthly before annual for better UX)
            var sortedPackages = AvailablePackages
                .OrderByDescending(p => p.IsPopular)
                .ThenBy(p => p.Price)
                .ToList();
            
            AvailablePackages.Clear();
            foreach (var package in sortedPackages)
            {
                AvailablePackages.Add(package);
            }

            // Auto-select the first (popular/recommended) package
            if (AvailablePackages.Count > 0)
            {
                SelectedPackage = AvailablePackages.First();
            }
#else
            // Placeholder for other platforms
            await Task.Delay(1000);
            HasError = true;
            ErrorMessage = "Subscriptions are not available on this platform.";
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading offerings: {ex.Message}");
            HasError = true;
            ErrorMessage = $"Failed to load subscription plans: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<PurchaseResult> PurchasePackageAsync(string packageId)
    {
#if IOS
        try
        {
            var tcs = new TaskCompletionSource<RCOfferings>();
            
            RCPurchases.SharedPurchases.GetOfferings((offerings, error) =>
            {
                if (error != null)
                {
                    tcs.SetException(new Exception(error.LocalizedDescription));
                }
                else
                {
                    tcs.SetResult(offerings);
                }
            });
            
            var offerings = await tcs.Task;
            var packageToPurchase = offerings?.Current?.AvailablePackages
                .FirstOrDefault(p => p.Identifier == packageId);
                
            if (packageToPurchase == null)
            {
                return new PurchaseResult 
                { 
                    IsSuccess = false, 
                    ErrorMessage = "Selected package not found." 
                };
            }
            
            var purchaseTcs = new TaskCompletionSource<bool>();
            
            RCPurchases.SharedPurchases.PurchasePackage(packageToPurchase, (transaction, customerInfo, error, userCancelled) =>
            {
                if (error != null)
                {
                    purchaseTcs.SetException(new Exception(error.LocalizedDescription));
                }
                else if (userCancelled)
                {
                    purchaseTcs.SetException(new OperationCanceledException("Purchase was cancelled"));
                }
                else
                {
                    purchaseTcs.SetResult(true);
                }
            });
            
            await purchaseTcs.Task;
            
            // After successful purchase, set user attributes from secure storage
            _ = Task.Run(async () =>
            {
                try
                {
                    await SetUserAttributesFromSecureStorageAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting user attributes after purchase: {ex.Message}");
                }
            });
            
            return new PurchaseResult 
            { 
                IsSuccess = true, 
                Message = "Subscription purchased successfully!" 
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Purchase exception: {ex.Message}");
            
            // Check if this is a user cancellation (RevenueCat error code 1)
            if (ex.Message.Contains("Code = 1") || ex.Message.Contains("cancelled") || ex.Message.Contains("canceled"))
            {
                return new PurchaseResult 
                { 
                    IsSuccess = false,
                    IsCancellation = true,
                    ErrorMessage = "Purchase was cancelled"
                };
            }
            
            return new PurchaseResult 
            { 
                IsSuccess = false, 
                ErrorMessage = ex.Message 
            };
        }
#else
        await Task.Delay(100);
        return new PurchaseResult 
        { 
            IsSuccess = false, 
            ErrorMessage = "Purchase not available on this platform." 
        };
#endif
    }

    /// <summary>
    /// Sets user attributes from secure storage after successful purchase
    /// </summary>
    private async Task SetUserAttributesFromSecureStorageAsync()
    {
        try
        {
            // Get basic user info from secure storage/preferences
            var userEmail = await SecureStorage.Default.GetAsync("user_email");
            var userId = await SecureStorage.Default.GetAsync("user_id");
            var firstName = Preferences.Get("user_first_name", string.Empty);
            var lastName = Preferences.Get("user_last_name", string.Empty);

            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Retrieved from storage - Email: {userEmail}, UserID: {userId}, FirstName: '{firstName}', LastName: '{lastName}'");

            if (string.IsNullOrEmpty(userEmail))
            {
                System.Diagnostics.Debug.WriteLine("[RevenueCat] No user email found in secure storage");
                return;
            }

            // Set user ID (use the actual user ID if available, otherwise fall back to email)
            var revenueCatUserId = !string.IsNullOrEmpty(userId) ? userId : userEmail;
            await _revenueCatUserService.SetUserIdAsync(revenueCatUserId);
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] User ID set to: {revenueCatUserId}");

            // Set user attributes
            var attributes = new Dictionary<string, string>
            {
                ["$email"] = userEmail,
                ["last_purchase"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            
            // Add user ID as custom attribute as well
            if (!string.IsNullOrEmpty(userId))
            {
                attributes["user_id"] = userId;
                System.Diagnostics.Debug.WriteLine($"[RevenueCat] Adding user_id: {userId}");
            }
            
            if (!string.IsNullOrEmpty(firstName))
            {
                attributes["first_name"] = firstName;  // Use custom attribute name
                System.Diagnostics.Debug.WriteLine($"[RevenueCat] Adding firstName: {firstName}");
            }
            
            if (!string.IsNullOrEmpty(lastName))
            {
                attributes["last_name"] = lastName;   // Use custom attribute name
                System.Diagnostics.Debug.WriteLine($"[RevenueCat] Adding lastName: {lastName}");
            }
            
            if (!string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
            {
                attributes["$displayName"] = $"{firstName} {lastName}";  // This is a standard attribute
                System.Diagnostics.Debug.WriteLine($"[RevenueCat] Adding displayName: {firstName} {lastName}");
            }

            await _revenueCatUserService.SetUserAttributesAsync(attributes);
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Set {attributes.Count} user attributes after purchase: {string.Join(", ", attributes.Keys)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RevenueCat] Error setting user attributes: {ex.Message}");
        }
    }

    /// <summary>
    /// Debug method to check what's stored in preferences and secure storage
    /// </summary>
    private async Task DebugStoredUserDataAsync()
    {
        try
        {
            var userEmail = await SecureStorage.Default.GetAsync("user_email");
            var userId = await SecureStorage.Default.GetAsync("user_id");
            var firstName = Preferences.Get("user_first_name", string.Empty);
            var lastName = Preferences.Get("user_last_name", string.Empty);
            
            System.Diagnostics.Debug.WriteLine("=== DEBUG: Stored User Data ===");
            System.Diagnostics.Debug.WriteLine($"Email from SecureStorage: '{userEmail ?? "NULL"}'");
            System.Diagnostics.Debug.WriteLine($"UserID from SecureStorage: '{userId ?? "NULL"}'");
            System.Diagnostics.Debug.WriteLine($"FirstName from Preferences: '{firstName}'");
            System.Diagnostics.Debug.WriteLine($"LastName from Preferences: '{lastName}'");
            
            // Check if userId is actually null/empty
            if (string.IsNullOrEmpty(userId))
            {
                System.Diagnostics.Debug.WriteLine("WARNING: UserID is null or empty!");
                // Try using email as fallback for user ID
                if (!string.IsNullOrEmpty(userEmail))
                {
                    System.Diagnostics.Debug.WriteLine($"Will use email as user ID: {userEmail}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine("================================");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading stored data: {ex.Message}");
        }
    }

    private static string GetPackageTitle(string packageId)
    {
        if (packageId.Contains("monthly") || packageId.Contains("month"))
            return "Monthly Premium";
        if (packageId.Contains("yearly") || packageId.Contains("year") || packageId.Contains("annual"))
            return "Annual Premium";
        
        return "Premium Subscription";
    }

    private static string GetPackageDescription(string packageId)
    {
        if (packageId.Contains("monthly") || packageId.Contains("month"))
            return "Full access to all meditation content, renewed monthly";
        if (packageId.Contains("yearly") || packageId.Contains("year") || packageId.Contains("annual"))
            return "Full access to all meditation content, best value";
        
        return "Full access to all premium meditation content";
    }

    private static string GetPackagePeriod(string packageId)
    {
        if (packageId.Contains("monthly") || packageId.Contains("month"))
            return "per month";
        if (packageId.Contains("yearly") || packageId.Contains("year") || packageId.Contains("annual"))
            return "per year";
        
        return "subscription";
    }

    private static bool IsPopularPackage(string packageId)
    {
        // Mark annual packages as popular (best value)
        return packageId.Contains("yearly") || packageId.Contains("year") || packageId.Contains("annual");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class SubscriptionPackage
{
    public string PackageId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PriceString { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPopular { get; set; }
    public string PeriodString { get; set; } = string.Empty;
    
#if IOS
    public RevenueCat.RCPackage? RevenueCatPackage { get; set; }
#endif
}

public class PurchaseResult
{
    public bool IsSuccess { get; set; }
    public bool IsRestore { get; set; }
    public bool IsCancellation { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
