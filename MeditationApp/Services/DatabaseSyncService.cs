using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MeditationApp.Models;
using Microsoft.Maui.Networking;

namespace MeditationApp.Services
{
    /// <summary>
    /// Service responsible for synchronizing local database with remote server when internet connection is available
    /// </summary>
    public class DatabaseSyncService
    {
        private readonly MeditationSessionDatabase _database;
        private readonly GraphQLService _graphQLService;
        private readonly LocalAuthService _localAuthService;
        private readonly CalendarDataService _calendarDataService;
        private readonly CognitoAuthService _cognitoAuthService;
        
        private bool _isSyncing = false;
        private DateTime _lastSyncAttempt = DateTime.MinValue;
        private readonly TimeSpan _syncCooldown = TimeSpan.FromMinutes(5); // Prevent too frequent sync attempts

        public event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;

        public DatabaseSyncService(
            MeditationSessionDatabase database,
            GraphQLService graphQLService,
            LocalAuthService localAuthService,
            CalendarDataService calendarDataService,
            CognitoAuthService cognitoAuthService)
        {
            _database = database;
            _graphQLService = graphQLService;
            _localAuthService = localAuthService;
            _calendarDataService = calendarDataService;
            _cognitoAuthService = cognitoAuthService;
        }

        /// <summary>
        /// Performs a full synchronization of local data with remote server
        /// </summary>
        public async Task<SyncResult> SyncAllDataAsync(bool forceSync = false)
        {
            if (_isSyncing && !forceSync)
            {
                Debug.WriteLine("[DatabaseSync] Sync already in progress, skipping");
                return new SyncResult { IsSuccess = false, Message = "Sync already in progress" };
            }

            if (!forceSync && DateTime.UtcNow - _lastSyncAttempt < _syncCooldown)
            {
                Debug.WriteLine("[DatabaseSync] Sync cooldown period active, skipping");
                return new SyncResult { IsSuccess = false, Message = "Sync cooldown active" };
            }

            try
            {
                _isSyncing = true;
                _lastSyncAttempt = DateTime.UtcNow;
                OnSyncStatusChanged(new SyncStatusEventArgs { Status = SyncStatus.Starting, Message = "Starting database synchronization" });

                Debug.WriteLine("[DatabaseSync] Starting full database synchronization");

                // Check network connectivity
                if (!await IsOnlineAsync())
                {
                    Debug.WriteLine("[DatabaseSync] No internet connection available");
                    OnSyncStatusChanged(new SyncStatusEventArgs { Status = SyncStatus.Failed, Message = "No internet connection" });
                    return new SyncResult { IsSuccess = false, Message = "No internet connection" };
                }

                // Check authentication
                var userId = await GetCurrentUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    Debug.WriteLine("[DatabaseSync] User not authenticated");
                    OnSyncStatusChanged(new SyncStatusEventArgs { Status = SyncStatus.Failed, Message = "User not authenticated" });
                    return new SyncResult { IsSuccess = false, Message = "User not authenticated" };
                }

                var syncResult = new SyncResult { IsSuccess = true };
                var tasks = new List<Task<SyncResult>>();

                // Run synchronization tasks in parallel
                OnSyncStatusChanged(new SyncStatusEventArgs { Status = SyncStatus.SyncingSessions, Message = "Synchronizing meditation sessions" });
                tasks.Add(SyncMeditationSessionsAsync(userId));

                OnSyncStatusChanged(new SyncStatusEventArgs { Status = SyncStatus.SyncingInsights, Message = "Synchronizing daily insights" });
                tasks.Add(SyncDailyInsightsAsync(userId));

                var results = await Task.WhenAll(tasks);

                // Combine results
                foreach (var result in results)
                {
                    if (!result.IsSuccess)
                    {
                        syncResult.IsSuccess = false;
                        syncResult.Message += result.Message + "; ";
                    }
                    syncResult.SessionsUpdated += result.SessionsUpdated;
                    syncResult.InsightsUpdated += result.InsightsUpdated;
                }

                if (syncResult.IsSuccess)
                {
                    Debug.WriteLine($"[DatabaseSync] Synchronization completed successfully - {syncResult.SessionsUpdated} sessions, {syncResult.InsightsUpdated} insights updated");
                    OnSyncStatusChanged(new SyncStatusEventArgs 
                    { 
                        Status = SyncStatus.Completed, 
                        Message = $"Sync completed: {syncResult.SessionsUpdated} sessions, {syncResult.InsightsUpdated} insights updated" 
                    });
                    
                    // Clear caches to ensure fresh data
                    _database.ClearCache();
                    _calendarDataService.ClearCache();
                }
                else
                {
                    Debug.WriteLine($"[DatabaseSync] Synchronization completed with errors: {syncResult.Message}");
                    OnSyncStatusChanged(new SyncStatusEventArgs { Status = SyncStatus.Failed, Message = syncResult.Message });
                }

                return syncResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSync] Synchronization failed with exception: {ex.Message}");
                OnSyncStatusChanged(new SyncStatusEventArgs { Status = SyncStatus.Failed, Message = $"Sync failed: {ex.Message}" });
                return new SyncResult { IsSuccess = false, Message = ex.Message };
            }
            finally
            {
                _isSyncing = false;
            }
        }

        /// <summary>
        /// Synchronizes meditation sessions from remote server
        /// </summary>
        private async Task<SyncResult> SyncMeditationSessionsAsync(string userId)
        {
            try
            {
                Debug.WriteLine("[DatabaseSync] Starting meditation sessions sync");

                // Load GraphQL query
                string query = await Utils.GraphQLQueryLoader.LoadQueryAsync("ListUserMeditationSessions.graphql");
                if (string.IsNullOrWhiteSpace(query))
                {
                    Debug.WriteLine("[DatabaseSync] GraphQL query not found, using fallback");
                    query = @"query ListUserMeditationSessions($userID: ID!) { 
                        listUserMeditationSessions(userID: $userID) { 
                            sessionID 
                            userID 
                            timestamp 
                            audioPath 
                            status 
                        } 
                    }";
                }

                var variables = new { userID = userId };
                var result = await _graphQLService.QueryAsync(query, variables);

                int updatedCount = 0;

                if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                    dataElem.TryGetProperty("listUserMeditationSessions", out var sessionsElem) &&
                    sessionsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    Debug.WriteLine($"[DatabaseSync] Found {sessionsElem.GetArrayLength()} sessions from server");

                    // Get existing sessions to preserve download status
                    var existingSessions = await _database.GetSessionsAsync();
                    var existingSessionsDict = existingSessions.ToDictionary(s => s.Uuid);

                    foreach (var sessionElem in sessionsElem.EnumerateArray())
                    {
                        var sessionId = sessionElem.TryGetProperty("sessionID", out var idElem) ? idElem.GetString() ?? string.Empty : string.Empty;
                        
                        if (string.IsNullOrEmpty(sessionId)) continue;

                        // Parse timestamp
                        DateTime timestampVal = DateTime.MinValue;
                        if (sessionElem.TryGetProperty("timestamp", out var tsElem))
                        {
                            if (tsElem.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                long timestampMillis;
                                if (!tsElem.TryGetInt64(out timestampMillis))
                                {
                                    var timestampDouble = tsElem.GetDouble();
                                    timestampMillis = Convert.ToInt64(timestampDouble);
                                }
                                timestampVal = DateTimeOffset.FromUnixTimeMilliseconds(timestampMillis).LocalDateTime;
                            }
                        }

                        var session = new MeditationSession
                        {
                            Uuid = sessionId,
                            UserID = sessionElem.TryGetProperty("userID", out var userElem) ? userElem.GetString() ?? string.Empty : string.Empty,
                            Timestamp = timestampVal,
                            AudioPath = sessionElem.TryGetProperty("audioPath", out var audioElem) ? audioElem.GetString() ?? string.Empty : string.Empty,
                            Status = sessionElem.TryGetProperty("status", out var statusElem) ? 
                                Utils.MeditationSessionStatusHelper.ParseSessionStatus(statusElem.GetString() ?? MeditationSessionStatus.REQUESTED.ToString()) : 
                                MeditationSessionStatus.REQUESTED
                        };

                        // Preserve download status from existing session if available
                        if (existingSessionsDict.TryGetValue(sessionId, out var existingSession))
                        {
                            session.IsDownloaded = existingSession.IsDownloaded;
                            session.LocalAudioPath = existingSession.LocalAudioPath;
                            session.DownloadedAt = existingSession.DownloadedAt;
                            session.FileSizeBytes = existingSession.FileSizeBytes;
                            
                            // Preserve higher status (don't downgrade COMPLETED to REQUESTED)
                            if (existingSession.Status == MeditationSessionStatus.COMPLETED && 
                                session.Status == MeditationSessionStatus.REQUESTED)
                            {
                                Debug.WriteLine($"[DatabaseSync] Preserving COMPLETED status for session {sessionId}");
                                session.Status = existingSession.Status;
                            }
                        }

                        await _database.SaveSessionAsync(session);
                        updatedCount++;
                    }

                    Debug.WriteLine($"[DatabaseSync] Saved {updatedCount} sessions to local database");
                }

                return new SyncResult { IsSuccess = true, SessionsUpdated = updatedCount };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSync] Error syncing meditation sessions: {ex.Message}");
                return new SyncResult { IsSuccess = false, Message = $"Sessions sync failed: {ex.Message}" };
            }
        }

        /// <summary>
        /// Synchronizes daily insights bidirectionally (push unsynced local data, pull remote data)
        /// </summary>
        private async Task<SyncResult> SyncDailyInsightsAsync(string userId)
        {
            try
            {
                Debug.WriteLine("[DatabaseSync] Starting daily insights sync");
                int updatedCount = 0;

                // Step 1: Push unsynced local insights to server
                var unsyncedInsights = await _database.GetUnsyncedInsightsAsync();
                Debug.WriteLine($"[DatabaseSync] Found {unsyncedInsights.Count} unsynced local insights");

                foreach (var insight in unsyncedInsights)
                {
                    try
                    {
                        await PushInsightToServerAsync(insight);
                        await _database.MarkInsightsAsSyncedAsync(insight);
                        Debug.WriteLine($"[DatabaseSync] Successfully pushed insight for {insight.Date:yyyy-MM-dd}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DatabaseSync] Failed to push insight for {insight.Date:yyyy-MM-dd}: {ex.Message}");
                        // Continue with other insights
                    }
                }

                // Step 2: Pull all insights from server
                string query = await Utils.GraphQLQueryLoader.LoadQueryAsync("ListUserDailyInsights.graphql");
                if (string.IsNullOrWhiteSpace(query))
                {
                    Debug.WriteLine("[DatabaseSync] GraphQL query not found, using fallback");
                    query = @"query ListUserDailyInsights($userID: ID!) {
                        listUserDailyInsights(userID: $userID) {
                            userID
                            date
                            notes
                            mood
                        }
                    }";
                }

                var variables = new { userID = userId };
                var result = await _graphQLService.QueryAsync(query, variables);

                if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                    dataElem.TryGetProperty("listUserDailyInsights", out var insightsElem) &&
                    insightsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    Debug.WriteLine($"[DatabaseSync] Found {insightsElem.GetArrayLength()} insights from server");

                    foreach (var insightElem in insightsElem.EnumerateArray())
                    {
                        if (!insightElem.TryGetProperty("date", out var dateElem) || dateElem.ValueKind != System.Text.Json.JsonValueKind.Number)
                            continue;

                        var date = DateTimeOffset.FromUnixTimeMilliseconds(dateElem.GetInt64()).LocalDateTime.Date;
                        var notes = insightElem.TryGetProperty("notes", out var notesElem) ? notesElem.GetString() ?? string.Empty : string.Empty;
                        int? mood = insightElem.TryGetProperty("mood", out var moodElem) && moodElem.TryGetInt32(out var moodValue) ? moodValue : null;

                        var existingInsight = await _database.GetDailyInsightsAsync(userId, date);

                        if (existingInsight == null)
                        {
                            // Create new insight
                            var newInsight = new UserDailyInsights
                            {
                                UserID = userId,
                                Date = date,
                                Notes = notes,
                                Mood = mood,
                                IsSynced = true,
                                LastUpdated = DateTime.UtcNow
                            };
                            await _database.SaveDailyInsightsAsync(newInsight);
                            updatedCount++;
                        }
                        else if (!existingInsight.IsSynced || 
                                 existingInsight.LastUpdated < DateTime.UtcNow.AddMinutes(-5))
                        {
                            // Update existing insight if it's not synced or is older than 5 minutes
                            existingInsight.Notes = notes;
                            existingInsight.Mood = mood;
                            existingInsight.IsSynced = true;
                            existingInsight.LastUpdated = DateTime.UtcNow;
                            await _database.SaveDailyInsightsAsync(existingInsight);
                            updatedCount++;
                        }
                    }

                    Debug.WriteLine($"[DatabaseSync] Updated {updatedCount} insights from server");
                }

                return new SyncResult { IsSuccess = true, InsightsUpdated = updatedCount };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSync] Error syncing daily insights: {ex.Message}");
                return new SyncResult { IsSuccess = false, Message = $"Insights sync failed: {ex.Message}" };
            }
        }

        /// <summary>
        /// Pushes a single insight to the remote server
        /// </summary>
        private async Task PushInsightToServerAsync(UserDailyInsights insight)
        {
            string mutation = await Utils.GraphQLQueryLoader.LoadQueryAsync("AddUserDailyInsights.graphql");
            if (string.IsNullOrWhiteSpace(mutation))
            {
                mutation = @"mutation AddUserDailyInsights($UserDailyInsightsInput: UserDailyInsightsInput!) {
                    addUserDailyInsights(UserDailyInsightsInput: $UserDailyInsightsInput) {
                        userID
                        date
                        notes
                        mood
                    }
                }";
            }

            var dateMilliseconds = ((DateTimeOffset)insight.Date).ToUnixTimeMilliseconds();
            var variables = new
            {
                UserDailyInsightsInput = new
                {
                    userID = insight.UserID,
                    date = dateMilliseconds,
                    notes = insight.Notes,
                    mood = insight.Mood
                }
            };

            await _graphQLService.QueryAsync(mutation, variables);
        }

        /// <summary>
        /// Checks if the app is currently online
        /// </summary>
        private async Task<bool> IsOnlineAsync()
        {
            try
            {
                return await _localAuthService.IsNetworkAvailableAsync();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current authenticated user ID
        /// </summary>
        private async Task<string?> GetCurrentUserIdAsync()
        {
            try
            {
                var accessToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("access_token");
                if (string.IsNullOrEmpty(accessToken))
                    return null;

                var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
                return attributes.FirstOrDefault(a => a.Name == "sub")?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Triggers sync if conditions are met (network available, not already syncing, etc.)
        /// </summary>
        public async Task<SyncResult> TriggerSyncIfNeededAsync()
        {
            if (!await IsOnlineAsync())
            {
                return new SyncResult { IsSuccess = false, Message = "No internet connection" };
            }

            if (_isSyncing)
            {
                return new SyncResult { IsSuccess = false, Message = "Sync already in progress" };
            }

            if (DateTime.UtcNow - _lastSyncAttempt < _syncCooldown)
            {
                return new SyncResult { IsSuccess = false, Message = "Sync cooldown active" };
            }

            return await SyncAllDataAsync();
        }

        /// <summary>
        /// Performs a quick sync of only unsynced local data
        /// </summary>
        public async Task<SyncResult> SyncUnsyncedDataAsync()
        {
            if (!await IsOnlineAsync())
            {
                return new SyncResult { IsSuccess = false, Message = "No internet connection" };
            }

            try
            {
                var userId = await GetCurrentUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    return new SyncResult { IsSuccess = false, Message = "User not authenticated" };
                }

                // Only push unsynced insights
                var unsyncedInsights = await _database.GetUnsyncedInsightsAsync();
                foreach (var insight in unsyncedInsights)
                {
                    try
                    {
                        await PushInsightToServerAsync(insight);
                        await _database.MarkInsightsAsSyncedAsync(insight);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DatabaseSync] Failed to sync insight: {ex.Message}");
                    }
                }

                return new SyncResult { IsSuccess = true, InsightsUpdated = unsyncedInsights.Count };
            }
            catch (Exception ex)
            {
                return new SyncResult { IsSuccess = false, Message = ex.Message };
            }
        }

        protected virtual void OnSyncStatusChanged(SyncStatusEventArgs e)
        {
            SyncStatusChanged?.Invoke(this, e);
        }
    }

    public class SyncResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SessionsUpdated { get; set; }
        public int InsightsUpdated { get; set; }
    }

    public class SyncStatusEventArgs : EventArgs
    {
        public SyncStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public enum SyncStatus
    {
        Starting,
        SyncingSessions,
        SyncingInsights,
        Completed,
        Failed
    }
}
