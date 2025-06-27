using System;
using Microsoft.Maui.Controls;
using UraniumUI.Pages;
using System.Threading.Tasks;

namespace MeditationApp.Views
{
    public partial class BreathingExercisePage : UraniumContentPage
    {
        private bool _isBreathing;
        public BreathingExercisePage()
        {
            InitializeComponent();
        }

        private async void OnStartButtonClicked(object sender, EventArgs e)
        {
            if (_isBreathing) return;
            _isBreathing = true;
            StartButton.IsEnabled = false;
            while (_isBreathing)
            {
                // Breathe In
                BreathingPhaseLabel.Text = "Breathe In";
                await BreathingCircle.ScaleTo(1.4, 4000, Easing.CubicInOut);
                // Hold
                BreathingPhaseLabel.Text = "Hold";
                await Task.Delay(2000);
                // Breathe Out
                BreathingPhaseLabel.Text = "Breathe Out";
                await BreathingCircle.ScaleTo(1.0, 4000, Easing.CubicInOut);
                // Hold
                BreathingPhaseLabel.Text = "Hold";
                await Task.Delay(2000);
            }
            StartButton.IsEnabled = true;
            BreathingPhaseLabel.Text = "Ready to begin?";
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isBreathing = false;
        }

        private void OnHamburgerClicked(object sender, EventArgs e)
        {
            Shell.Current.FlyoutIsPresented = true;
        }
    }
}