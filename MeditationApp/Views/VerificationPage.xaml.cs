using MeditationApp.Services;
using MeditationApp.ViewModels;
using UraniumUI.Pages;

namespace MeditationApp.Views;

public partial class VerificationPage : UraniumContentPage, IQueryAttributable
{
    private readonly VerificationViewModel _viewModel;

    public VerificationPage(CognitoAuthService cognitoAuthService, HybridAuthService hybridAuthService)
    {
        InitializeComponent();
        _viewModel = new VerificationViewModel(cognitoAuthService, hybridAuthService);
        BindingContext = _viewModel;
    }

    public VerificationPage() : this(
        Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService(typeof(CognitoAuthService)) as CognitoAuthService ?? throw new InvalidOperationException("CognitoAuthService not found"),
        Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService(typeof(HybridAuthService)) as HybridAuthService ?? throw new InvalidOperationException("HybridAuthService not found")
    )
    {
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
