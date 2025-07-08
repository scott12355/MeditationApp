using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MeditationApp.Models;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;

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
        private readonly BreathingDatabaseService _breathingDatabase;
        
        private bool _isSyncing = false;
        private DateTime _lastSyncAttempt = DateTime.MinValue;
        private readonly TimeSpan _syncCooldown = TimeSpan.FromMinutes(5); // Prevent too frequent sync attempts

        public event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;

        public DatabaseSyncService(
            MeditationSessionDatabase database,
            GraphQLService graphQLService,
            LocalAuthService localAuthService,
            CalendarDataService calendarDataService,
            CognitoAuthService cognitoAuthService,
            BreathingDatabaseService breathingDatabase)
        {
            _database = database;
            _graphQLService = graphQLService;
            _localAuthService = localAuthService;
            _calendarDataService = calendarDataService;
            _cognitoAuthService = cognitoAuthService;
            _breathingDatabase = breathingDatabase;
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

                OnSyncStatusChanged(new SyncStatusEventArgs { Status = SyncStatus.SyncingBreathing, Message = "Synchronizing breathing sessions" });
                tasks.Add(SyncBreathingSessionsAsync(userId));

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
        private Task<bool> IsOnlineAsync()
        {
            return Task.FromResult(Connectivity.NetworkAccess == NetworkAccess.Internet);
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

        /// <summary>
        /// Synchronizes breathing sessions bidirectionally (push unsynced local data, pull remote data)
        /// </summary>
        private async Task<SyncResult> SyncBreathingSessionsAsync(string userId)
        {
            try
            {
                Debug.WriteLine($"[DatabaseSync] Starting breathing sessions sync for user: {userId}");

                var enableBreathingSync = await IsBreathingSyncEnabledAsync();
                if (!enableBreathingSync)
                {
                    Debug.WriteLine("[DatabaseSync] Breathing sync disabled");
                    return new SyncResult 
                    { 
                        IsSuccess = true, 
                        Message = "Breathing sync disabled" 
                    };
                }

                // Step 1: Push unsynced local sessions to server
                var unsyncedSessions = await _breathingDatabase.GetUnsyncedSessionsAsync();
                Debug.WriteLine($"[DatabaseSync] Found {unsyncedSessions.Count} unsynced breathing sessions");
                
                var syncedCount = 0;
                
                foreach (var session in unsyncedSessions)
                {
                    try
                    {
                        Debug.WriteLine($"[DatabaseSync] Attempting to sync session {session.Id} ({session.TechniqueName})");
                        await PushBreathingSessionToServerAsync(session, userId);
                        syncedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DatabaseSync] Failed to push breathing session {session.Id}: {ex.Message}");
                        // Mark sync attempt but continue with other sessions
                        session.LastSyncAttempt = DateTime.UtcNow;
                        session.SyncError = ex.Message;
                        await _breathingDatabase.SaveSessionAsync(session);
                    }
                }

                // Step 2: Pull latest sessions from server
                Debug.WriteLine("[DatabaseSync] Pulling breathing sessions from server");
                try
                {
                    await PullBreathingSessionsFromServerAsync(userId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DatabaseSync] Failed to pull breathing sessions: {ex.Message}");
                    // Don't fail the entire sync if pulling fails - pushing is more important
                }

                Debug.WriteLine($"[DatabaseSync] Breathing sessions sync completed. Pushed {syncedCount} sessions");
                return new SyncResult 
                { 
                    IsSuccess = true, 
                    Message = $"Synced {syncedCount} breathing sessions" 
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSync] Breathing sessions sync failed: {ex.Message}");
                Debug.WriteLine($"[DatabaseSync] Stack trace: {ex.StackTrace}");
                return new SyncResult 
                { 
                    IsSuccess = false, 
                    Message = $"Breathing sync failed: {ex.Message}" 
                };
            }
        }

        /// <summary>
        /// Pushes a single breathing session to the remote server
        /// </summary>
        private async Task PushBreathingSessionToServerAsync(BreathingSession session, string userId)
        {
            string mutation = await Utils.GraphQLQueryLoader.LoadQueryAsync("CreateBreathingSession.graphql");
            if (string.IsNullOrWhiteSpace(mutation))
            {
                mutation = @"mutation CreateBreathingSession($input: CreateBreathingSessionInput!) {
                    createBreathingSession(input: $input) {
                        id
                        userID
                        startTime
                        endTime
                        techniqueId
                        techniqueName
                        completedCycles
                        totalCycles
                        duration
                        isCompleted
                        wasInterrupted
                    }
                }";
            }

            var input = new
            {
                userID = userId,
                startTime = session.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                endTime = session.EndTime?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                techniqueId = session.TechniqueId,
                techniqueName = session.TechniqueName,
                completedCycles = session.CompletedCycles,
                totalCycles = session.TotalCycles,
                duration = (int)session.Duration.TotalSeconds,
                isCompleted = session.IsCompleted,
                wasInterrupted = session.WasInterrupted,
                streakDay = session.StreakDay
            };

            var variables = new { input };
            Debug.WriteLine($"[DatabaseSync] Sending createBreathingSession mutation with input: {System.Text.Json.JsonSerializer.Serialize(input)}");
            var result = await _graphQLService.QueryAsync(mutation, variables);
            Debug.WriteLine($"[DatabaseSync] Raw GraphQL response: {result.RootElement}");

            // Check for GraphQL errors first
            if (result.RootElement.TryGetProperty("errors", out var errorsElem))
            {
                Debug.WriteLine($"[DatabaseSync] GraphQL errors in create breathing session: {errorsElem}");
                throw new Exception($"GraphQL mutation failed: {errorsElem}");
            }

            if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                dataElem.TryGetProperty("createBreathingSession", out var sessionElem))
            {
                // Check if createBreathingSession returned null
                if (sessionElem.ValueKind == System.Text.Json.JsonValueKind.Null)
                {
                    throw new Exception("createBreathingSession returned null");
                }

                if (sessionElem.TryGetProperty("id", out var idElem))
                {
                    // Mark as synced and store backend ID
                    session.IsSynced = true;
                    session.BackendId = idElem.GetString();
                    session.SyncError = null;
                    session.LastSyncAttempt = DateTime.UtcNow;
                    await _breathingDatabase.SaveSessionAsync(session);
                    
                    Debug.WriteLine($"[DatabaseSync] Successfully synced breathing session {session.Id}");
                }
                else
                {
                    throw new Exception("No ID returned from createBreathingSession");
                }
            }
            else
            {
                throw new Exception("Invalid response from server");
            }
        }

        /// <summary>
        /// Pulls breathing sessions from the remote server with pagination
        /// </summary>
        private async Task PullBreathingSessionsFromServerAsync(string userId)
        {
            Debug.WriteLine($"[DatabaseSync] Starting pull for user ID: {userId}");
            string? nextToken = null;
            var allSessionsCount = 0;

            do
            {
                Debug.WriteLine($"[DatabaseSync] Fetching breathing sessions page with nextToken: {nextToken ?? "null"}");
                
                string query = await Utils.GraphQLQueryLoader.LoadQueryAsync("ListUserBreathingSessions.graphql");
                if (string.IsNullOrWhiteSpace(query))
                {
                    query = @"query ListUserBreathingSessions($userID: ID!, $limit: Int, $nextToken: String) {
                        listUserBreathingSessions(userID: $userID, limit: $limit, nextToken: $nextToken) {
                            items {
                                id
                                userID
                                startTime
                                endTime
                                techniqueId
                                techniqueName
                                completedCycles
                                totalCycles
                                duration
                                isCompleted
                                wasInterrupted
                                streakDay
                                createdAt
                                updatedAt
                            }
                            nextToken
                        }
                    }";
                }

                var variables = new { 
                    userID = userId,
                    limit = 100, // Get up to 100 sessions per page
                    nextToken = nextToken
                };
                Debug.WriteLine($"[DatabaseSync] Sending listUserBreathingSessions query with userID: {userId}, limit: 100, nextToken: {nextToken ?? "null"}");
                var result = await _graphQLService.QueryAsync(query, variables);
                Debug.WriteLine($"[DatabaseSync] Raw GraphQL response: {result.RootElement}");

                // Check for GraphQL errors first
                if (result.RootElement.TryGetProperty("errors", out var errorsElem))
                {
                    Debug.WriteLine($"[DatabaseSync] GraphQL errors in breathing sessions query: {errorsElem}");
                    throw new Exception($"GraphQL query failed: {errorsElem}");
                }

                if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                    dataElem.TryGetProperty("listUserBreathingSessions", out var listElem))
                {
                    // Handle case where listUserBreathingSessions might be null
                    if (listElem.ValueKind == System.Text.Json.JsonValueKind.Null)
                    {
                        Debug.WriteLine("[DatabaseSync] listUserBreathingSessions returned null - no sessions found");
                        return; // No sessions to sync
                    }

                    // Update nextToken for pagination
                    nextToken = null;
                    if (listElem.TryGetProperty("nextToken", out var nextTokenElem) && 
                        nextTokenElem.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        nextToken = nextTokenElem.GetString();
                    }

                    if (listElem.TryGetProperty("items", out var itemsElem) &&
                        itemsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var pageCount = itemsElem.GetArrayLength();
                        Debug.WriteLine($"[DatabaseSync] Found {pageCount} breathing sessions in this page");
                        allSessionsCount += pageCount;

                        // Get existing sessions to avoid duplicates
                        var existingSessions = await _breathingDatabase.GetSessionsAsync();
                        var existingBackendIds = existingSessions
                            .Where(s => !string.IsNullOrEmpty(s.BackendId))
                            .Select(s => s.BackendId)
                            .ToHashSet();

                        foreach (var sessionElem in itemsElem.EnumerateArray())
                        {
                            if (!sessionElem.TryGetProperty("id", out var idElem))
                                continue;

                            var backendId = idElem.GetString();
                            if (string.IsNullOrEmpty(backendId))
                                continue;

                            if (existingBackendIds.Contains(backendId))
                            {
                                Debug.WriteLine($"[DatabaseSync] Skipping existing session {backendId}");
                                continue; // Skip if already exists
                            }

                            // Parse and create local session
                            Debug.WriteLine($"[DatabaseSync] Creating new local session from backend ID: {backendId}");
                            var session = ParseBreathingSessionFromServer(sessionElem);
                            session.IsSynced = true;
                            session.BackendId = backendId;
                            
                            await _breathingDatabase.SaveSessionAsync(session);
                            Debug.WriteLine($"[DatabaseSync] Saved session {session.Id} locally");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[DatabaseSync] No items array found in breathing sessions response");
                        break; // No items to process
                    }
                }
                else
                {
                    Debug.WriteLine("[DatabaseSync] No data found in breathing sessions response");
                    break;
                }

            } while (!string.IsNullOrEmpty(nextToken));

            Debug.WriteLine($"[DatabaseSync] Completed pulling breathing sessions. Total sessions processed: {allSessionsCount}");
        }

        private BreathingSession ParseBreathingSessionFromServer(System.Text.Json.JsonElement sessionElem)
        {
            var session = new BreathingSession();
            
            DateTime startTime = DateTime.MinValue;
            DateTime? endTime = null;
            
            if (sessionElem.TryGetProperty("startTime", out var startElem))
                DateTime.TryParse(startElem.GetString(), out startTime);
                
            if (sessionElem.TryGetProperty("endTime", out var endElem) && endElem.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                DateTime parsedEndTime;
                if (DateTime.TryParse(endElem.GetString(), out parsedEndTime))
                    endTime = parsedEndTime;
            }

            // Map all properties from server response
            session.StartTime = startTime;
            session.EndTime = endTime;
            session.TechniqueId = sessionElem.TryGetProperty("techniqueId", out var techIdElem) ? techIdElem.GetInt32() : 0;
            session.TechniqueName = sessionElem.TryGetProperty("techniqueName", out var techNameElem) ? techNameElem.GetString() ?? "" : "";
            session.CompletedCycles = sessionElem.TryGetProperty("completedCycles", out var cyclesElem) ? cyclesElem.GetInt32() : 0;
            session.TotalCycles = sessionElem.TryGetProperty("totalCycles", out var totalCyclesElem) ? totalCyclesElem.GetInt32() : 0;
            
            if (sessionElem.TryGetProperty("duration", out var durationElem))
                session.Duration = TimeSpan.FromSeconds(durationElem.GetInt32());
                
            session.IsCompleted = sessionElem.TryGetProperty("isCompleted", out var completedElem) && completedElem.GetBoolean();
            session.WasInterrupted = sessionElem.TryGetProperty("wasInterrupted", out var interruptedElem) && interruptedElem.GetBoolean();
            session.StreakDay = sessionElem.TryGetProperty("streakDay", out var streakElem) ? streakElem.GetInt32() : 0;

            return session;
        }

        /// <summary>
        /// Fires the SyncStatusChanged event
        /// </summary>
        private void OnSyncStatusChanged(SyncStatusEventArgs e)
        {
            SyncStatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Checks if breathing sync is enabled
        /// </summary>
        private Task<bool> IsBreathingSyncEnabledAsync()
        {
            // Allow disabling breathing sync via app preferences if needed
            var isEnabled = Preferences.Get("BreathingSyncEnabled", true);
            
            Debug.WriteLine($"[DatabaseSync] Breathing sync enabled: {isEnabled}");
            return Task.FromResult(isEnabled);
        }

        /// <summary>
        /// Syncs breathing sessions immediately without cooldown - used after session completion
        /// </summary>
        public async Task<SyncResult> SyncBreathingSessionsImmediatelyAsync()
        {
            try
            {
                Debug.WriteLine("[DatabaseSync] Starting immediate breathing sessions sync");

                // Check network connectivity
                if (!await IsOnlineAsync())
                {
                    Debug.WriteLine("[DatabaseSync] No internet connection available");
                    return new SyncResult { IsSuccess = false, Message = "No internet connection" };
                }

                // Check authentication
                var userId = await GetCurrentUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    Debug.WriteLine("[DatabaseSync] User not authenticated");
                    return new SyncResult { IsSuccess = false, Message = "User not authenticated" };
                }

                // Sync breathing sessions only
                return await SyncBreathingSessionsAsync(userId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSync] Immediate breathing sync failed: {ex.Message}");
                return new SyncResult { IsSuccess = false, Message = ex.Message };
            }
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
        SyncingBreathing,
        Completed,
        Failed
    }
}
