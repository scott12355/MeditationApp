using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Controls;

public partial class CalendarView : ContentView
{
    public CalendarView(MeditationSessionDatabase? database = null)
    {
        InitializeComponent();
        BindingContext = new CalendarControlViewModel(database);
    }
}
