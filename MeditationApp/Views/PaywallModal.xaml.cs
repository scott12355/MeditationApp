using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Views;

public partial class PaywallModal : ContentPage
{
    private readonly PaywallViewModel _viewModel;
    private TaskCompletionSource<PaywallResult>? _resultTaskCompletionSource;

    public PaywallModal()
    {
        InitializeComponent();
        
        // Get RevenueCatUserService from DI container
        var serviceProvider = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services;
        var revenueCatUserService = serviceProvider?.GetService(typeof(IRevenueCatUserService)) as IRevenueCatUserService 
            ?? throw new InvalidOperationException("RevenueCatUserService not found in DI container");
        
        _viewModel = new PaywallViewModel(revenueCatUserService);
        BindingContext = _viewModel;
        
        // Subscribe to ViewModel events
        _viewModel.PaywallClosed += OnPaywallClosed;
        _viewModel.PurchaseCompleted += OnPurchaseCompleted;
    }

    /// <summary>
    /// Shows the paywall modal and returns the result
    /// </summary>
    public async Task<PaywallResult> ShowAsync()
    {
        _resultTaskCompletionSource = new TaskCompletionSource<PaywallResult>();
        
        // Present the modal
        if (Application.Current?.MainPage != null)
        {
            await Application.Current.MainPage.Navigation.PushModalAsync(this);
        }
        
        // Wait for the result
        return await _resultTaskCompletionSource.Task;
    }

    private async void OnPaywallClosed(object? sender, EventArgs e)
    {
        await CloseModal(new PaywallResult { WasCancelled = true });
    }

    private async void OnPurchaseCompleted(object? sender, PurchaseResult e)
    {
        if (e.IsSuccess)
        {
            // Show success message
            await DisplayAlert("Success", e.Message, "OK");
            await CloseModal(new PaywallResult 
            { 
                WasPurchased = true, 
                IsRestore = e.IsRestore 
            });
        }
        else if (e.IsCancellation)
        {
            // User cancelled the purchase - don't show error, just log it
            System.Diagnostics.Debug.WriteLine("Purchase was cancelled by user");
            // Don't close the modal, let user try again or close manually
        }
        else
        {
            // Show actual error message (not cancellation)
            await DisplayAlert("Error", e.ErrorMessage, "OK");
            // Don't close the modal, let user try again
        }
    }

    private async Task CloseModal(PaywallResult result)
    {
        try
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.Navigation.PopModalAsync();
            }
            
            _resultTaskCompletionSource?.SetResult(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error closing paywall modal: {ex.Message}");
            _resultTaskCompletionSource?.SetException(ex);
        }
    }

    private async void OnTermsTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // Navigate to terms of service
            await Browser.OpenAsync("https://your-app-terms-url.com", BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening terms: {ex.Message}");
            await DisplayAlert("Error", "Unable to open Terms of Service", "OK");
        }
    }

    private async void OnPrivacyTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // Navigate to privacy policy
            await Browser.OpenAsync("https://your-app-privacy-url.com", BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening privacy policy: {ex.Message}");
            await DisplayAlert("Error", "Unable to open Privacy Policy", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Clean up event subscriptions
        if (_viewModel != null)
        {
            _viewModel.PaywallClosed -= OnPaywallClosed;
            _viewModel.PurchaseCompleted -= OnPurchaseCompleted;
        }
    }

    // Handle hardware back button on Android
    protected override bool OnBackButtonPressed()
    {
        _ = Task.Run(async () =>
        {
            await CloseModal(new PaywallResult { WasCancelled = true });
        });
        
        return true; // Handled
    }
}

public class PaywallResult
{
    public bool WasPurchased { get; set; }
    public bool WasCancelled { get; set; }
    public bool IsRestore { get; set; }
}
