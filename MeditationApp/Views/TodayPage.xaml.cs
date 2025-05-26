using MeditationApp.ViewModels;

namespace MeditationApp.Views;

public partial class TodayPage : ContentPage
{
    public TodayPage()
    {
        InitializeComponent();
        BindingContext = new TodayViewModel();
    }


}
