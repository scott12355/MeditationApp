using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace MeditationApp.Views
{
    public partial class OnboardingPage1 : ContentPage
    {
        public OnboardingPage1()
        {
            InitializeComponent();
        }

        private void OnNextClicked(object sender, EventArgs e)
        {
            // Navigate to second onboarding page
            if (Application.Current != null)
            {
                var onboardingPage2 = new OnboardingPage2();
                Application.Current.MainPage = onboardingPage2;
            }
        }
    }
}
