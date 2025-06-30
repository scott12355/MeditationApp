using Microsoft.Maui.Controls;
using UraniumUI.Pages;
using MeditationApp.ViewModels;

namespace MeditationApp.Views
{
    public partial class BreathingSettingsPage : UraniumContentPage
    {
        public BreathingSettingsPage(BreathingExerciseViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Auto-save settings when leaving the page
            if (BindingContext is BreathingExerciseViewModel viewModel)
            {
                await viewModel.SaveSettingsAsync();
            }
        }
    }
}
