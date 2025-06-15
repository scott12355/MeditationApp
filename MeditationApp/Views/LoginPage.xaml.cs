using MeditationApp.Services;
using MeditationApp.ViewModels;
using UraniumUI.Pages;

namespace MeditationApp.Views;

public partial class LoginPage : UraniumContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.ClearFields();
    }
}
