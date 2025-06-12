using System.Diagnostics;
using System.IO;
using System.Net.Http;
using MeditationApp.Models;

namespace MeditationApp.Services
{
    public interface IAudioDownloadService
    {
        Task<bool> DownloadSessionAudioAsync(MeditationSession session, string presignedUrl);
        Task<string?> GetPresignedUrlAsync(string sessionId);
        // Optionally, provide a method to get the local audio path for a session
        string? GetLocalAudioPath(MeditationSession session);
    }

    public class AudioDownloadDownloadService : IAudioDownloadService
    {
        private readonly HttpClient _httpClient;
        private readonly GraphQLService _graphQLService;
        private readonly string _audioDirectory;

        public AudioDownloadDownloadService(HttpClient httpClient, GraphQLService graphQLService)
        {
            _httpClient = httpClient;
            _graphQLService = graphQLService;
            _audioDirectory = Path.Combine(FileSystem.AppDataDirectory, "audio");
            if (!Directory.Exists(_audioDirectory))
            {
                Directory.CreateDirectory(_audioDirectory);
            }
        }

        public async Task<bool> DownloadSessionAudioAsync(MeditationSession session, string presignedUrl)
        {
            try
            {
                Debug.WriteLine($"Checking download status for session {session.Uuid}:");
                Debug.WriteLine($"- IsDownloaded: {session.IsDownloaded}");
                Debug.WriteLine($"- LocalAudioPath: {session.LocalAudioPath}");
                Debug.WriteLine($"- Audio directory: {_audioDirectory}");
                
                // Check if already downloaded
                if (session.IsDownloaded && !string.IsNullOrEmpty(session.LocalAudioPath))
                {
                    var fileExists = File.Exists(session.LocalAudioPath);
                    Debug.WriteLine($"- File exists check: {fileExists}");
                    
                    if (fileExists)
                    {
                        var existingFileInfo = new FileInfo(session.LocalAudioPath);
                        Debug.WriteLine($"- File details - Size: {existingFileInfo.Length} bytes, Last modified: {existingFileInfo.LastWriteTime}");
                        Debug.WriteLine($"Session {session.Uuid} already downloaded and verified");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"Session {session.Uuid} marked as downloaded but file not found at {session.LocalAudioPath}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Session {session.Uuid} not downloaded or missing path");
                }

                Debug.WriteLine($"Starting download for session {session.Uuid}");

                // Extract the file name from the presigned URL and decode it
                var downloadFileName = Path.GetFileName(new Uri(presignedUrl).AbsolutePath);
                downloadFileName = Uri.UnescapeDataString(downloadFileName);
                var localPath = Path.Combine(_audioDirectory, downloadFileName);
                Debug.WriteLine($"Will save to: {localPath}");

                // Download the file
                using var response = await _httpClient.GetAsync(presignedUrl);
                response.EnsureSuccessStatusCode();
                Debug.WriteLine($"Download response status: {response.StatusCode}");

                using var fileStream = new FileStream(localPath, FileMode.Create);
                await response.Content.CopyToAsync(fileStream);
                Debug.WriteLine($"File downloaded and saved to {localPath}");

                // Extra check: verify file exists and log size
                if (!File.Exists(localPath))
                {
                    Debug.WriteLine($"ERROR: File was not saved at {localPath}");
                    return false;
                }
                var downloadedFileInfo = new FileInfo(localPath);
                Debug.WriteLine($"Downloaded file details - Size: {downloadedFileInfo.Length} bytes, Last modified: {downloadedFileInfo.LastWriteTime}");
                
                // Update session with local file info
                session.LocalAudioPath = localPath;
                session.IsDownloaded = true;
                session.DownloadedAt = DateTime.Now;
                session.FileSizeBytes = downloadedFileInfo.Length;
                Debug.WriteLine($"Updated session with download info - Path: {localPath}, Size: {downloadedFileInfo.Length} bytes");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading session {session.Uuid}: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public string? GetLocalAudioPath(MeditationSession session)
        {
            Debug.WriteLine($"GetLocalAudioPath called for session {session.Uuid}:");
            Debug.WriteLine($"- IsDownloaded: {session.IsDownloaded}");
            Debug.WriteLine($"- LocalAudioPath: {session.LocalAudioPath}");
            
            if (session.IsDownloaded && !string.IsNullOrEmpty(session.LocalAudioPath))
            {
                var fileExists = File.Exists(session.LocalAudioPath);
                Debug.WriteLine($"- File exists check: {fileExists}");
                
                if (fileExists)
                {
                    var fileInfo = new FileInfo(session.LocalAudioPath);
                    Debug.WriteLine($"- File details - Size: {fileInfo.Length} bytes, Last modified: {fileInfo.LastWriteTime}");
                    return session.LocalAudioPath;
                }
                else
                {
                    Debug.WriteLine($"File not found at {session.LocalAudioPath}");
                }
            }
            return null;
        }

        public async Task<string?> GetPresignedUrlAsync(string sessionId)
        {
            try
            {
                var query = await LoadGraphQLQueryFromAssetAsync("GraphQL/Queries/GetMeditationSessionPresignedUrl.graphql");
                if (string.IsNullOrWhiteSpace(query))
                {
                    Debug.WriteLine("Failed to load GraphQL query from asset using default query");
                    // Use a clean, valid GraphQL query string with real newlines
                    query = @"query GetMeditationSessionPresignedUrl($sessionID: ID!) {
  getMeditationSessionPresignedUrl(sessionID: $sessionID) {
    presignedUrl
  }
}";
                }
                var variables = new { sessionID = sessionId };
                var result = await _graphQLService.QueryAsync(query, variables);
                Debug.WriteLine($"GraphQL presigned URL response: {result.RootElement}");
                if (result.RootElement.TryGetProperty("errors", out var errorsElem))
                {
                    Debug.WriteLine($"GraphQL errors: {errorsElem}");
                    return null;
                }
                if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                    dataElem.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    dataElem.TryGetProperty("getMeditationSessionPresignedUrl", out var urlElem) &&
                    urlElem.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    urlElem.TryGetProperty("presignedUrl", out var presignedUrlElem) &&
                    presignedUrlElem.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return presignedUrlElem.GetString();
                }
                Debug.WriteLine("Failed to get presigned URL from GraphQL response (data or presignedUrl missing or null)");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting presigned URL for session {sessionId}: {ex.Message}");
                return null;
            }
        }

        private async Task<string> LoadGraphQLQueryFromAssetAsync(string fileName)
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading GraphQL query from {fileName}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
