#if IOS
using AuthenticationServices;
using Microsoft.Maui.Handlers;
using UIKit;
using MeditationApp.Controls;
using Microsoft.Maui;
using System.Linq;

namespace MeditationApp.Platforms.iOS.Handlers;

public class AppleSignInButtonHandler : ViewHandler<AppleSignInButton, UIView>
{
    public static IPropertyMapper<AppleSignInButton, AppleSignInButtonHandler> Mapper = new PropertyMapper<AppleSignInButton, AppleSignInButtonHandler>(ViewHandler.ViewMapper);

    // No static registration - the handler will be directly assigned in code

    public AppleSignInButtonHandler() : base(Mapper) { }

    ASAuthorizationAppleIdButton? _appleButton;
    AppleSignInDelegate? _delegate;
    AppleSignInPresentationContextProvider? _presentationContextProvider;

    protected override UIView CreatePlatformView()
    {
        _appleButton = new ASAuthorizationAppleIdButton(ASAuthorizationAppleIdButtonType.SignIn, ASAuthorizationAppleIdButtonStyle.Black);
        _appleButton.TouchUpInside += OnAppleSignInButtonTapped;
        return _appleButton;
    }

    private void OnAppleSignInButtonTapped(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("AppleSignInButtonHandler: OnAppleSignInButtonTapped called");
        var provider = new ASAuthorizationAppleIdProvider();
        var request = provider.CreateRequest();
        request.RequestedScopes = new[] { ASAuthorizationScope.FullName, ASAuthorizationScope.Email };
        _delegate = new AppleSignInDelegate(VirtualView);
        _presentationContextProvider = new AppleSignInPresentationContextProvider();
        var controller = new ASAuthorizationController(new[] { request });
        controller.Delegate = _delegate;
        controller.PresentationContextProvider = _presentationContextProvider;
        controller.PerformRequests();
    }

    private class AppleSignInDelegate : ASAuthorizationControllerDelegate
    {
        private readonly AppleSignInButton _button;
        public AppleSignInDelegate(AppleSignInButton button) => _button = button;
        public override void DidComplete(ASAuthorizationController controller, ASAuthorization authorization)
        {
            System.Diagnostics.Debug.WriteLine("AppleSignInButtonHandler: DidComplete called");
            if (authorization.GetCredential<ASAuthorizationAppleIdCredential>() is { } credential)
            {
                var idToken = credential.IdentityToken != null ? Foundation.NSString.FromData(credential.IdentityToken, Foundation.NSStringEncoding.UTF8) : null;
                _button.OnSignInCompleted(idToken);
            }
        }
        public void DidCompleteWithError(ASAuthorizationController controller, Foundation.NSError error)
        {
            System.Diagnostics.Debug.WriteLine($"AppleSignInButtonHandler: DidCompleteWithError called: {error?.LocalizedDescription}");
        }
    }

    private class AppleSignInPresentationContextProvider : Foundation.NSObject, IASAuthorizationControllerPresentationContextProviding
    {
        public UIWindow GetPresentationAnchor(ASAuthorizationController controller)
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                var windowScene = UIApplication.SharedApplication.ConnectedScenes
                    .OfType<UIWindowScene>()
                    .FirstOrDefault();
                return windowScene?.Windows.FirstOrDefault(w => w.IsKeyWindow) ?? new UIWindow();
            }
            return UIApplication.SharedApplication.KeyWindow ?? new UIWindow();
        }
    }
}
#endif
