using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace MeditationApp.Services
{
    public class GraphQLService
    {
        private readonly HttpClient _httpClient;
        private readonly CognitoAuthService _cognitoAuthService;
        private const string Endpoint = "https://lhr6w6nilbfovmfs5lt77v7bx4.appsync-api.eu-west-1.amazonaws.com/graphql";

        public GraphQLService(HttpClient httpClient, CognitoAuthService cognitoAuthService)
        {
            _httpClient = httpClient;
            _cognitoAuthService = cognitoAuthService;
        }

        public async Task<JsonDocument> QueryAsync(string query, object? variables = null)
        {
            return await QueryWithRefreshAsync(query, variables, true);
        }

        private async Task<JsonDocument> QueryWithRefreshAsync(string query, object? variables, bool allowRefresh)
        {
            var accessToken = await SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException("User is not authenticated. No access token found.");

            if (_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var requestBody = new { query, variables };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            Debug.WriteLine($"GraphQL Query: {query}");
            var response = await _httpClient.PostAsync(Endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseStream = await response.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(responseStream);
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized && allowRefresh)
            {
                // Try to refresh token and retry once
                var refreshToken = await SecureStorage.GetAsync("refresh_token");
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var refreshResult = await _cognitoAuthService.RefreshTokenAsync(refreshToken);
                    if (refreshResult.IsSuccess && !string.IsNullOrEmpty(refreshResult.AccessToken))
                    {
                        await SecureStorage.SetAsync("access_token", refreshResult.AccessToken);
                        if (!string.IsNullOrEmpty(refreshResult.IdToken))
                            await SecureStorage.SetAsync("id_token", refreshResult.IdToken);
                        if (!string.IsNullOrEmpty(refreshResult.RefreshToken))
                            await SecureStorage.SetAsync("refresh_token", refreshResult.RefreshToken);
                        // Retry the request once
                        return await QueryWithRefreshAsync(query, variables, false);
                    }
                    else
                    {
                        // Check if refresh token is expired/invalid - if so, clear tokens for logout
                        if (IsRefreshTokenExpiredError(refreshResult.ErrorMessage))
                        {
                            Debug.WriteLine("Refresh token is expired or invalid - clearing all tokens for logout");
                            SecureStorage.Remove("access_token");
                            SecureStorage.Remove("id_token");
                            SecureStorage.Remove("refresh_token");
                            throw new UnauthorizedAccessException("Refresh token expired. User must log in again.");
                        }
                    }
                }
                
                // No refresh token or refresh failed with non-expiry error
                throw new InvalidOperationException("Session expired. Please log in again.");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"GraphQL request failed: {response.StatusCode} - {errorContent}");
            }
        }
        
        /// <summary>
        /// Helper method to determine if an error message indicates refresh token expiry
        /// </summary>
        private static bool IsRefreshTokenExpiredError(string? errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return false;
                
            var message = errorMessage.ToLowerInvariant();
            return message.Contains("refresh token") && message.Contains("expired") ||
                   message.Contains("refresh token") && message.Contains("invalid") ||
                   message.Contains("token_expired") ||
                   message.Contains("refresh_token_expired") ||
                   message.Contains("notauthorized") ||
                   message.Contains("invalid_grant");
        }
    }
}
