using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Views;

[QueryProperty(nameof(SelectedDateString), "date")]
public partial class DayDetailPage : ContentPage
{
    private readonly MeditationSessionDatabase _database;

    public DayDetailPage(MeditationSessionDatabase database)
    {
        InitializeComponent();
        _database = database;
    }

    public string SelectedDateString
    {
        set
        {
            if (DateTime.TryParse(value, out var date))
            {
                BindingContext = new DayDetailViewModel(date, _database);
            }
        }
    }
}
