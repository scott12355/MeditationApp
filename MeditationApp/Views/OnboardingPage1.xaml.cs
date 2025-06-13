using Microsoft.Maui.Controls;

namespace MeditationApp.Views
{
    public partial class OnboardingPage1 : ContentPage
    {
        public OnboardingPage1()
        {
            InitializeComponent();
        }

        private async void OnNextClicked(object sender, EventArgs e)
        {
            // Mark onboarding as complete
            Microsoft.Maui.Storage.Preferences.Set("HasLaunchedBefore", true);
            // Navigate to the splash or main page
            await Shell.Current.GoToAsync("//SplashPage");
        }
    }
}
