using MeditationApp.ViewModels;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using System.Diagnostics;

namespace MeditationApp.Views;

public partial class TodayPage : ContentPage
{
    private readonly TodayViewModel _viewModel;

    public TodayPage(TodayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        
        // Configure MediaElement
        AudioPlayer.ShouldAutoPlay = false;
        AudioPlayer.ShouldShowPlaybackControls = false;
        AudioPlayer.ShouldKeepScreenOn = true;
        AudioPlayer.ShouldMute = false;

        // Set up MediaElement in ViewModel
        _viewModel.SetMediaElement(AudioPlayer);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.CleanupMediaElement();
    }
}
