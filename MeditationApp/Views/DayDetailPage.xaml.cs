using MeditationApp.ViewModels;
using MeditationApp.Models;

namespace MeditationApp.Views;

[QueryProperty(nameof(Date), "date")]
public partial class DayDetailPage : ContentPage
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
}
