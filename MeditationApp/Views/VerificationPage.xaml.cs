using MeditationApp.Services;
using MeditationApp.ViewModels;

namespace MeditationApp.Views;

public partial class VerificationPage : ContentPage, IQueryAttributable
{
    private readonly VerificationViewModel _viewModel;

    public VerificationPage(CognitoAuthService cognitoAuthService)
    {
        InitializeComponent();
        _viewModel = new VerificationViewModel(cognitoAuthService);
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("Username") && query["Username"] != null)
        {
            _viewModel.Username = query["Username"].ToString() ?? string.Empty;
        }
        if (query.ContainsKey("Password") && query["Password"] != null)
        {
            _viewModel.Password = query["Password"].ToString() ?? string.Empty;
        }
    }
}
