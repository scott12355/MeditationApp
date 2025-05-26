using MeditationApp.ViewModels;

namespace MeditationApp.Views;

public partial class TodayPage : ContentPage
{
    public TodayPage(TodayViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }


}
