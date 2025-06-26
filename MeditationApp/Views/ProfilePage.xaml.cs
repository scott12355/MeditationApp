using MeditationApp.ViewModels;

namespace MeditationApp.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // Subscribe to ViewModel events
        _viewModel.ShowAlert += OnShowAlert;
        _viewModel.ShowConfirmationDialog += OnShowConfirmationDialog;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadUserProfileAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Unsubscribe from events to prevent memory leaks
        _viewModel.ShowAlert -= OnShowAlert;
        _viewModel.ShowConfirmationDialog -= OnShowConfirmationDialog;
    }

    private async void OnShowAlert(object? sender, string message)
    {
        await DisplayAlert("Information", message, "OK");
    }

    private async void OnShowConfirmationDialog(object? sender, ConfirmationDialogEventArgs args)
    {
        bool result = await DisplayAlert(args.Title, args.Message, args.ConfirmText, args.CancelText);
        args.TaskCompletionSource.SetResult(result);
    }
}
