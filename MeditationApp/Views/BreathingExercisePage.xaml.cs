using System;
using Microsoft.Maui.Controls;
using UraniumUI.Pages;
using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Views
{
    public partial class BreathingExercisePage : UraniumContentPage
    {
        public BreathingExerciseViewModel ViewModel { get; }

        public BreathingExercisePage(BreathingExerciseViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            BindingContext = ViewModel;
        }

        // Fallback constructor for dependency injection
        public BreathingExercisePage() : this(new BreathingExerciseViewModel())
        {
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Always clean up any existing session state to prevent duplicates
            ViewModel.Cleanup();
            
            // Always start by showing the technique list and ensure no session is active
            ViewModel.ShowTechniqueSelector = true;
            
            // Belt and suspenders: also call CancelSessionCommand if needed
            if (ViewModel.IsSessionActive)
            {
                ViewModel.CancelSessionCommand.Execute(null);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Clean up any active sessions
            ViewModel?.Cleanup();
        }

        private void OnHamburgerClicked(object sender, EventArgs e)
        {
            Shell.Current.FlyoutIsPresented = true;
        }

        private void OnBackClicked(object sender, EventArgs e)
        {
            // Cancel any active breathing session and return to technique selection
            if (ViewModel.IsSessionActive)
            {
                ViewModel.CancelSessionCommand.Execute(null);
            }
            ViewModel.ShowTechniqueSelector = true;
        }
    }
}