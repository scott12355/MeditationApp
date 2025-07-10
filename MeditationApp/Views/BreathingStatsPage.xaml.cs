using Microsoft.Maui.Controls;
using UraniumUI.Pages;
using MeditationApp.ViewModels;

namespace MeditationApp.Views
{
    public partial class BreathingStatsPage : UraniumContentPage
    {
        private readonly BreathingExerciseViewModel _viewModel;
        
        public BreathingStatsPage(BreathingExerciseViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Refresh stats when page appears
            _viewModel.LoadBreathingStats();
        }
    }
}
