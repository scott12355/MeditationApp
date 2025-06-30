using Microsoft.Maui.Controls;
using UraniumUI.Pages;
using MeditationApp.Views;

namespace MeditationApp.Views
{
    public partial class BreathingTutorialPage : UraniumContentPage
    {
        public BreathingTutorialPage()
        {
            InitializeComponent();
        }

        private async void OnStartFirstSessionClicked(object sender, EventArgs e)
        {
            // Navigate to the breathing exercise page
            await Shell.Current.GoToAsync("//BreathingExercisePage");
        }
    }
}
