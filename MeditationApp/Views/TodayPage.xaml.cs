using MeditationApp.ViewModels;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using MeditationApp.Drawing;
using Microsoft.Maui.Graphics;
using MeditationApp.Models; // Import the Models namespace for MeditationSessionStatus

namespace MeditationApp.Views;

public partial class TodayPage : ContentPage
{
    private TodayViewModel _viewModel;
    private MediaElement _audioPlayer;
    private IDispatcherTimer animationTimer; // Timer for the orb animation
    private Animation textPulseAnimation; // Animation for the text label

    public TodayPage(TodayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Set the drawable for the GraphicsViews
        GlowingOrbGraphicsView.Drawable = new GlowingOrbDrawable(Color.FromArgb("#FFA500")); // Orange for Generating
        CompletedOrbGraphicsView.Drawable = new GlowingOrbDrawable(Color.FromArgb("#00FF00")); // Green for Completed

        // Set up orb animation timer
        animationTimer = Dispatcher.CreateTimer();
        animationTimer.Interval = TimeSpan.FromMilliseconds(30);
        animationTimer.Tick += (sender, e) =>
        {
            // Request both GraphicsViews to redraw if they are visible
            if (GlowingOrbGraphicsView.IsVisible)
            {
                 GlowingOrbGraphicsView.Invalidate();
            }
             if (CompletedOrbGraphicsView.IsVisible)
            {
                 CompletedOrbGraphicsView.Invalidate();
            }
        };
        // The orb timer will be started/stopped in UpdateAnimations based on visibility

        // Set up text pulsation animation
        textPulseAnimation = new Animation();
        textPulseAnimation.Add(0.0, 0.5, new Animation(v => GeneratingStatusLabel.Scale = v, 1.0, 1.05, Easing.CubicInOut)); // Scale up
        textPulseAnimation.Add(0.5, 1.0, new Animation(v => GeneratingStatusLabel.Scale = v, 1.05, 1.0, Easing.CubicInOut)); // Scale down

        // Hook into BindingContext changes to manage animations based on status
        BindingContextChanged += TodayPage_BindingContextChanged;
    }

    private void TodayPage_BindingContextChanged(object sender, EventArgs e)
    {
        if (BindingContext is TodayViewModel vm)
        {
            // Subscribe to property changes that affect visibility/status
            vm.PropertyChanged += ViewModel_PropertyChanged;
            // Initial check of status to start animations if needed
            UpdateAnimations(vm.TodaySession?.Status);
        }
    }

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is TodayViewModel vm)
        {
            // Check if the TodaySession property itself or the Status property within TodaySession has changed,
            // or if the IsPolling property has changed.
            if (e.PropertyName == nameof(TodayViewModel.TodaySession) || 
                (e.PropertyName == nameof(MeditationSession.Status) && vm.TodaySession != null) ||
                e.PropertyName == nameof(TodayViewModel.IsPolling))
            {
                 UpdateAnimations(vm.TodaySession?.Status);
            }
        }
    }


    private void UpdateAnimations(MeditationSessionStatus? status)
    {
        // Start/stop orb timer based on visibility of either orb GraphicsView
        // Visibility is controlled by XAML bindings to TodaySession.Status and IsPolling
        if (GlowingOrbGraphicsView.IsVisible || CompletedOrbGraphicsView.IsVisible)
        {
            animationTimer.Start();
        } else {
             animationTimer.Stop();
        }

        // Start/stop text animation based on visibility of the GeneratingStatusLabel
        // Visibility is controlled by XAML binding to TodaySession.Status
        if (GeneratingStatusLabel.IsVisible)
        {
             // Check if animation is not already running
            if (!GeneratingStatusLabel.AnimationIsRunning("TextPulseAnimation"))
            {
                 textPulseAnimation.Commit(GeneratingStatusLabel, "TextPulseAnimation", 16, 1000, repeat: () => true);
            }
        } else {
             // Stop animation if it's running
            if (GeneratingStatusLabel.AnimationIsRunning("TextPulseAnimation"))
            {
                 GeneratingStatusLabel.AbortAnimation("TextPulseAnimation");
                 GeneratingStatusLabel.Scale = 1.0; // Reset scale
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel != null)
        {
            _audioPlayer = this.FindByName<MediaElement>(nameof(AudioPlayer));
            if (_audioPlayer != null)
            {
                _viewModel.SetMediaElement(_audioPlayer);
            }
        }
        // Animations are now managed by ViewModel PropertyChanged and UpdateAnimations
        // We can call UpdateAnimations here on appearing just in case the status was set before the page appeared
        UpdateAnimations(_viewModel?.TodaySession?.Status);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_viewModel != null)
        {
            _viewModel.CleanupMediaElement();
            _audioPlayer = null;
        }
        // Ensure animations are stopped when the page is not visible
        animationTimer?.Stop();
        if (GeneratingStatusLabel.AnimationIsRunning("TextPulseAnimation"))
        {
            GeneratingStatusLabel.AbortAnimation("TextPulseAnimation");
             GeneratingStatusLabel.Scale = 1.0; // Reset scale
        }
    }
}
