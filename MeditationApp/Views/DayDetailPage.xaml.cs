using MeditationApp.ViewModels;
using MeditationApp.Models;
using MeditationApp.Controls;
using UraniumUI.Pages;

namespace MeditationApp.Views;

[QueryProperty(nameof(Date), "date")]
public partial class DayDetailPage : UraniumContentPage
{
    private string _date = string.Empty;
    public string Date
    {
        get => _date;
        set
        {
            _date = value;
            if (BindingContext is DayDetailViewModel viewModel && DateTime.TryParse(_date, out DateTime parsedDate))
            {
                viewModel.LoadDayData(parsedDate);
            }
        }
    }

    public DayDetailPage(DayDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        
        // Set up audio player bottom sheet
        if (viewModel is IAudioPlayerViewModel audioViewModel)
        {
            AudioPlayerBottomSheet.BindingContext = audioViewModel;
        }
        
        // Load DayData if available
        var dayData = MeditationApp.ViewModels.SimpleCalendarViewModel.SelectedDayData;
        if (dayData != null)
        {
            viewModel.LoadFromDayData(dayData);
        }
    }

    public void LoadFromDayData(DayData dayData)
    {
        if (BindingContext is DayDetailViewModel viewModel && dayData != null)
        {
            viewModel.LoadFromDayData(dayData);
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
}
