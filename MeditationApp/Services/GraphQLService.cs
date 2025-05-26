using System.Diagnostics;
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
        private const string Endpoint = "https://lhr6w6nilbfovmfs5lt77v7bx4.appsync-api.eu-west-1.amazonaws.com/graphql";

        public GraphQLService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<JsonDocument> QueryAsync(string query, object? variables = null)
        {
            // Get the Cognito access token from SecureStorage
            var accessToken = await SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException("User is not authenticated. No access token found.");

            // Remove any previous Authorization header
            if (_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var requestBody = new
            {
                query,
                variables
            };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            Debug.WriteLine($"GraphQL Query: {query}");
            var response = await _httpClient.PostAsync(Endpoint, content);
            response.EnsureSuccessStatusCode();
            var responseStream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(responseStream);
        }
    }
}
