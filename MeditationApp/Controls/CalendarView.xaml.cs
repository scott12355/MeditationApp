using MeditationApp.ViewModels;

namespace MeditationApp.Controls;

public partial class CalendarView : ContentView
{
    public CalendarView()
    {
        InitializeComponent();
        BindingContext = new CalendarControlViewModel();
    }
}
