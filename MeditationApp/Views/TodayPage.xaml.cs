using MeditationApp.ViewModels;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using MeditationApp.Drawing;
using Microsoft.Maui.Graphics;
using MeditationApp.Models; // Import the Models namespace for MeditationSessionStatus
using UraniumUI.Pages;

namespace MeditationApp.Views;

public partial class TodayPage : UraniumContentPage
{
    private TodayViewModel _viewModel;
    // private MediaElement? _audioPlayer; // Will be initialized in OnAppearing
    private IDispatcherTimer animationTimer; // Timer for the orb animation
    private Animation textPulseAnimation; // Animation for the text label
    private DateTime _lastBackgroundTap = DateTime.MinValue; // Prevent rapid tapping

    public TodayPage(TodayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        // _audioPlayer = null; // Will be set in OnAppearing

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

    private void TodayPage_BindingContextChanged(object? sender, EventArgs e)
    {
        if (BindingContext is TodayViewModel vm)
        {
            // Subscribe to property changes that affect visibility/status
            vm.PropertyChanged += ViewModel_PropertyChanged;
            // Initial check of status to start animations if needed
            UpdateAnimations(vm.TodaySession?.Status);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
            
            // Handle mood selector expansion/collapse animation
            if (e.PropertyName == nameof(TodayViewModel.IsMoodSelectorExpanded))
            {
                _ = AnimateMoodSelector(vm.IsMoodSelectorExpanded);
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

    private async Task AnimateMoodSelector(bool isExpanded)
    {
        // Ensure the XAML elements exist before animating
        if (CollapsedMoodFrame == null || ExpandedMoodSelector == null)
            return;

        // Cancel any ongoing animations to prevent conflicts
        CollapsedMoodFrame.AbortAnimation("MoodAnimation");
        ExpandedMoodSelector.AbortAnimation("MoodAnimation");
            
        if (isExpanded)
        {
            // Make expanded selector visible but invisible for smooth transition
            ExpandedMoodSelector.IsVisible = true;
            ExpandedMoodSelector.Opacity = 0;
            ExpandedMoodSelector.Scale = 0.6; // Start smaller for more dramatic effect
            ExpandedMoodSelector.TranslationY = -5; // Reduced translation for fixed height container
            
            // Collapse animation with spring easing
            var collapseAnimation = new Animation();
            collapseAnimation.Add(0, 0.7, new Animation(v => CollapsedMoodFrame.Opacity = v, 1, 0, Easing.CubicOut));
            collapseAnimation.Add(0, 0.7, new Animation(v => CollapsedMoodFrame.Scale = v, 1, 0.6, Easing.CubicOut));
            collapseAnimation.Add(0, 0.7, new Animation(v => CollapsedMoodFrame.TranslationY = v, 0, 3, Easing.CubicOut));
            
            // Expand animation with spring easing - starts a bit before collapse finishes for overlap
            collapseAnimation.Add(0.3, 1, new Animation(v => ExpandedMoodSelector.Opacity = v, 0, 1, Easing.SpringOut));
            collapseAnimation.Add(0.3, 1, new Animation(v => ExpandedMoodSelector.Scale = v, 0.6, 1, Easing.SpringOut));
            collapseAnimation.Add(0.3, 1, new Animation(v => ExpandedMoodSelector.TranslationY = v, -5, 0, Easing.SpringOut));
            
            var tcs = new TaskCompletionSource<bool>();
            collapseAnimation.Commit(this, "MoodAnimation", 16, 600, 
                finished: (v, c) => {
                    CollapsedMoodFrame.IsVisible = false;
                    tcs.SetResult(true);
                });
            
            await tcs.Task;
        }
        else
        {
            // Make collapsed frame visible but invisible for smooth transition
            CollapsedMoodFrame.IsVisible = true;
            CollapsedMoodFrame.Opacity = 0;
            CollapsedMoodFrame.Scale = 0.6;
            CollapsedMoodFrame.TranslationY = 3;
            
            // Collapse expanded selector and expand collapsed frame with spring easing
            var expandAnimation = new Animation();
            expandAnimation.Add(0, 0.7, new Animation(v => ExpandedMoodSelector.Opacity = v, 1, 0, Easing.CubicOut));
            expandAnimation.Add(0, 0.7, new Animation(v => ExpandedMoodSelector.Scale = v, 1, 0.6, Easing.CubicOut));
            expandAnimation.Add(0, 0.7, new Animation(v => ExpandedMoodSelector.TranslationY = v, 0, -5, Easing.CubicOut));
            
            // Show collapsed frame with spring effect
            expandAnimation.Add(0.3, 1, new Animation(v => CollapsedMoodFrame.Opacity = v, 0, 1, Easing.SpringOut));
            expandAnimation.Add(0.3, 1, new Animation(v => CollapsedMoodFrame.Scale = v, 0.6, 1, Easing.SpringOut));
            expandAnimation.Add(0.3, 1, new Animation(v => CollapsedMoodFrame.TranslationY = v, 3, 0, Easing.SpringOut));
            
            var tcs = new TaskCompletionSource<bool>();
            expandAnimation.Commit(this, "MoodAnimation", 16, 600,
                finished: (v, c) => {
                    ExpandedMoodSelector.IsVisible = false;
                    // Reset transform properties
                    ExpandedMoodSelector.TranslationY = 0;
                    CollapsedMoodFrame.TranslationY = 0;
                    tcs.SetResult(true);
                });
            
            await tcs.Task;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel != null)
        {
            // _audioPlayer = this.FindByName<MediaElement>(nameof(AudioPlayer));
            // if (_audioPlayer != null)
            // {
            //     _viewModel.SetMediaElement(_audioPlayer);
            // }
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
            // _viewModel.CleanupMediaElement();
            // _audioPlayer = null;
        }
        // Ensure animations are stopped when the page is not visible
        animationTimer?.Stop();
        if (GeneratingStatusLabel.AnimationIsRunning("TextPulseAnimation"))
        {
            GeneratingStatusLabel.AbortAnimation("TextPulseAnimation");
             GeneratingStatusLabel.Scale = 1.0; // Reset scale
        }
    }

    private void OnHamburgerClicked(object sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    // Handle background tap to dismiss keyboard
    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
    {
        // Prevent rapid tapping
        if (DateTime.Now - _lastBackgroundTap < TimeSpan.FromMilliseconds(300))
            return;
        _lastBackgroundTap = DateTime.Now;

        // Don't handle background taps if the audio player bottom sheet is open
        if (_viewModel is IAudioPlayerViewModel audioViewModel && audioViewModel.IsAudioPlayerSheetOpen)
            return;

        // Collapse mood selector if expanded
        if (_viewModel.IsMoodSelectorExpanded)
        {
            _viewModel.IsMoodSelectorExpanded = false;
        }
    }

    private void OnHeaderPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Completed && e.TotalY > 50)
        {
            // Swipe down detected, close the bottom sheet
            if (BindingContext is IAudioPlayerViewModel audioViewModel)
            {
                audioViewModel.IsAudioPlayerSheetOpen = false;
            }
        }
    }

    // Dismiss keyboard programmatically
    private void DismissKeyboard()
    {
        if (SessionNotesEditor != null)
        {
            SessionNotesEditor.Unfocus();
        }
    }

}
