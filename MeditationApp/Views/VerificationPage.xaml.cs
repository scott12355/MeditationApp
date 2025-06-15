using MeditationApp.Services;
using MeditationApp.ViewModels;
using UraniumUI.Pages;

namespace MeditationApp.Views;

public partial class VerificationPage : UraniumContentPage, IQueryAttributable
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
        if (query.ContainsKey("Email") && query["Email"] != null)
        {
            _viewModel.Email = query["Email"].ToString() ?? string.Empty;
        }
        if (query.ContainsKey("FirstName") && query["FirstName"] != null)
        {
            _viewModel.FirstName = query["FirstName"].ToString() ?? string.Empty;
        }
    }
}
