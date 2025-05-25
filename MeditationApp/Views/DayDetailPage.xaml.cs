using MeditationApp.ViewModels;

namespace MeditationApp.Views;

[QueryProperty(nameof(SelectedDateString), "date")]
public partial class DayDetailPage : ContentPage
{
    public DayDetailPage()
    {
        InitializeComponent();
    }

    public string SelectedDateString
    {
        set
        {
            if (DateTime.TryParse(value, out var date))
            {
                BindingContext = new DayDetailViewModel(date);
            }
        }
    }
}
