using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication;

namespace MeditationApp.Services;

public class CognitoAuthService
{
    private readonly string _clientId;
    private readonly string _userPoolId;
    private readonly string _region;
    private readonly AmazonCognitoIdentityProviderClient _provider;
    private CognitoUserPool _userPool;

    public CognitoAuthService(string clientId, string userPoolId, string region)
    {
        _clientId = clientId;
        _userPoolId = userPoolId;
        _region = region;

        // Initialize the Amazon Cognito Provider Client
        _provider = new AmazonCognitoIdentityProviderClient(RegionEndpoint.GetBySystemName(_region));
        // Initialize the Cognito User Pool
        _userPool = new CognitoUserPool(_userPoolId, _clientId, _provider);
    }

    public async Task<SignUpResult> SignUpAsync(string username, string email, string password, string firstName, string secondName)
    {
        try
        {
            var signUpRequest = new SignUpRequest
            {
                ClientId = _clientId,
                Username = username,
                Password = password,
                UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "email", Value = email },
                    new AttributeType { Name = "given_name", Value = firstName },
                    new AttributeType { Name = "family_name", Value = secondName }
                }
            };

            // Sign up the user
            var response = await _provider.SignUpAsync(signUpRequest);
            return new SignUpResult 
            { 
                IsSuccess = response.HttpStatusCode == System.Net.HttpStatusCode.OK 
            };
        }
        catch (InvalidPasswordException ex)
        {
            Console.WriteLine($"Password policy error: {ex.Message}");
            return new SignUpResult 
            { 
                IsSuccess = false, 
                ErrorMessage = "Password does not meet requirements. Please ensure it's at least 8 characters long and contains at least one special character.",
                ErrorCode = "InvalidPasswordException"
            };
        }
        catch (UsernameExistsException ex)
        {
            Console.WriteLine($"Username exists error: {ex.Message}");
            return new SignUpResult 
            { 
                IsSuccess = false, 
                ErrorMessage = "An account with this email already exists. Please try signing in instead.",
                ErrorCode = "UsernameExistsException"
            };
        }
        catch (InvalidParameterException ex)
        {
            Console.WriteLine($"Invalid parameter error: {ex.Message}");
            return new SignUpResult 
            { 
                IsSuccess = false, 
                ErrorMessage = "Please check that your email address is valid.",
                ErrorCode = "InvalidParameterException"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during sign up: {ex.Message}");
            return new SignUpResult 
            { 
                IsSuccess = false, 
                ErrorMessage = "Failed to create account. Please try again.",
                ErrorCode = "GeneralException"
            };
        }
    }

    public async Task<bool> ConfirmSignUpAsync(string username, string confirmationCode)
    {
        try
        {
            var confirmSignUpRequest = new ConfirmSignUpRequest
            {
                ClientId = _clientId,
                Username = username,
                ConfirmationCode = confirmationCode
            };

            // Confirm the sign-up
            var response = await _provider.ConfirmSignUpAsync(confirmSignUpRequest);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during confirmation: {ex.Message}");
            return false;
        }
    }

    public async Task<AuthenticationResponseType> SignInAsync(string username, string password)
    {
        try
        {
            var cognitoUser = new CognitoUser(username, _clientId, _userPool, _provider);
            var authRequest = new InitiateSrpAuthRequest
            {
                Password = password
            };

            // Sign in the user
            var authResponse = await cognitoUser.StartWithSrpAuthAsync(authRequest);

            return new AuthenticationResponseType
            {
                IsSuccess = true,
                AccessToken = authResponse.AuthenticationResult.AccessToken,
                IdToken = authResponse.AuthenticationResult.IdToken,
                RefreshToken = authResponse.AuthenticationResult.RefreshToken,
                ExpiresIn = (int)(authResponse.AuthenticationResult.ExpiresIn ?? 0),
            };
        }
        catch (UserNotConfirmedException)
        {
            return new AuthenticationResponseType
            {
                IsSuccess = false,
                ErrorMessage = "User is not confirmed."
            };
        }
        catch (NotAuthorizedException)
        {
            return new AuthenticationResponseType
            {
                IsSuccess = false,
                ErrorMessage = "Invalid username or password."
            };
        }
        catch (Exception ex)
        {
            return new AuthenticationResponseType
            {
                IsSuccess = false,
                ErrorMessage = $"Error during sign-in: {ex.Message}"
            };
        }
    }

    public async Task<bool> ForgotPasswordAsync(string username)
    {
        try
        {
            var forgotPasswordRequest = new ForgotPasswordRequest
            {
                ClientId = _clientId,
                Username = username
            };

            var response = await _provider.ForgotPasswordAsync(forgotPasswordRequest);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during forgot password: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ConfirmForgotPasswordAsync(string username, string confirmationCode, string newPassword)
    {
        try
        {
            var confirmForgotPasswordRequest = new ConfirmForgotPasswordRequest
            {
                ClientId = _clientId,
                Username = username,
                ConfirmationCode = confirmationCode,
                Password = newPassword
            };

            var response = await _provider.ConfirmForgotPasswordAsync(confirmForgotPasswordRequest);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during confirm forgot password: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SignOutAsync(string accessToken)
    {
        try
        {
            var signOutRequest = new GlobalSignOutRequest
            {
                AccessToken = accessToken
            };

            var response = await _provider.GlobalSignOutAsync(signOutRequest);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during sign out: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResendConfirmationCodeAsync(string username)
    {
        try
        {
            var resendConfirmationCodeRequest = new ResendConfirmationCodeRequest
            {
                ClientId = _clientId,
                Username = username
            };

            var response = await _provider.ResendConfirmationCodeAsync(resendConfirmationCodeRequest);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during resend confirmation code: {ex.Message}");
            return false;
        }
    }

    public async Task<List<AttributeType>> GetUserAttributesAsync(string accessToken)
    {
        try
        {
            var getUserRequest = new GetUserRequest
            {
                AccessToken = accessToken
            };

            var response = await _provider.GetUserAsync(getUserRequest);
            
            // Debug: Log all available user data
            Console.WriteLine($"[CognitoAuthService] GetUser response Username (UUID): {response.Username}");
            Console.WriteLine($"[CognitoAuthService] UserAttributes count: {response.UserAttributes.Count}");
            foreach (var attr in response.UserAttributes)
            {
                Console.WriteLine($"[CognitoAuthService] Attribute: {attr.Name} = {attr.Value}");
            }
            
            return response.UserAttributes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting user attributes: {ex.Message}");
            throw;
        }
    }

    public async Task<(string UserId, List<AttributeType> Attributes)> GetUserInfoAsync(string accessToken)
    {
        try
        {
            var getUserRequest = new GetUserRequest
            {
                AccessToken = accessToken
            };

            var response = await _provider.GetUserAsync(getUserRequest);
            
            Console.WriteLine($"[CognitoAuthService] GetUser response Username (UUID): {response.Username}");
            Console.WriteLine($"[CognitoAuthService] UserAttributes count: {response.UserAttributes.Count}");
            
            return (response.Username, response.UserAttributes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting user info: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> IsTokenValidAsync(string accessToken)
    {
        try
        {
            if (string.IsNullOrEmpty(accessToken))
                return false;

            var getUserRequest = new GetUserRequest
            {
                AccessToken = accessToken
            };

            await _provider.GetUserAsync(getUserRequest);
            return true;
        }
        catch (NotAuthorizedException)
        {
            // Token is expired or revoked
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating token: {ex.Message}");
            return false;
        }
    }

    public async Task<AuthenticationResponseType> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                return new AuthenticationResponseType
                {
                    IsSuccess = false,
                    ErrorMessage = "No refresh token provided"
                };
            }

            var refreshRequest = new InitiateAuthRequest
            {
                ClientId = _clientId,
                AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                AuthParameters = new Dictionary<string, string>
                {
                    { "REFRESH_TOKEN", refreshToken }
                }
            };

            var response = await _provider.InitiateAuthAsync(refreshRequest);

            return new AuthenticationResponseType
            {
                IsSuccess = true,
                AccessToken = response.AuthenticationResult.AccessToken,
                IdToken = response.AuthenticationResult.IdToken,
                RefreshToken = response.AuthenticationResult.RefreshToken ?? refreshToken, // Use existing refresh token if new one isn't provided
                ExpiresIn = (int)(response.AuthenticationResult.ExpiresIn ?? 0)
            };
        }
        catch (NotAuthorizedException)
        {
            return new AuthenticationResponseType
            {
                IsSuccess = false,
                ErrorMessage = "Refresh token is invalid or expired"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing token: {ex.Message}");
            return new AuthenticationResponseType
            {
                IsSuccess = false,
                ErrorMessage = $"Error refreshing token: {ex.Message}"
            };
        }
    }
}

public class AuthenticationResponseType
{
    public bool IsSuccess { get; set; }
    public string? AccessToken { get; set; }
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SignUpResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
}
