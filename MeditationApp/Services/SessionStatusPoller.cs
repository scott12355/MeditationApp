// MeditationApp/Services/SessionStatusPoller.cs
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MeditationApp.Models;
using MeditationApp.Utils;

namespace MeditationApp.Services;

public class SessionStatusPoller
{
    private readonly GraphQLService _graphQLService;
    private readonly MeditationSessionDatabase _sessionDatabase;
    private CancellationTokenSource? _cts;

    public SessionStatusPoller(GraphQLService graphQLService, MeditationSessionDatabase sessionDatabase)
    {
        _graphQLService = graphQLService;
        _sessionDatabase = sessionDatabase;
    }

    public async Task PollSessionStatusAsync(
        MeditationSession session,
        Func<MeditationSessionStatus, string?, Task>? onStatusChanged = null,
        int pollingIntervalMs = 5000,
        int maxPollingDurationMs = 300000)
    {
        _cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;
        int pollCount = 0;

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                pollCount++;
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                if (elapsed > maxPollingDurationMs)
                {
                    if (onStatusChanged != null)
                        await onStatusChanged(MeditationSessionStatus.FAILED, "Session generation timed out.");
                    break;
                }

                string query = await GraphQLQueryLoader.LoadQueryAsync("GetMeditationSessionStatus.graphql");
                if (string.IsNullOrWhiteSpace(query))
                {
                    query = "query GetMeditationSessionStatus($sessionID: ID!) { getMeditationSessionStatus(sessionID: $sessionID) { status errorMessage } }";
                }

                var variables = new { sessionID = session.Uuid };
                var result = await _graphQLService.QueryAsync(query, variables);

                if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                    dataElem.TryGetProperty("getMeditationSessionStatus", out var statusElem))
                {
                    var statusStr = statusElem.GetProperty("status").GetString();
                    var errorMessage = statusElem.GetProperty("errorMessage").GetString();
                    var newStatus = MeditationSessionStatusHelper.ParseSessionStatus(statusStr ?? MeditationSessionStatus.REQUESTED.ToString());

                    if (onStatusChanged != null)
                        await onStatusChanged(newStatus, errorMessage);

                    if (newStatus == MeditationSessionStatus.COMPLETED || newStatus == MeditationSessionStatus.FAILED)
                        break;
                }

                await Task.Delay(pollingIntervalMs, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[SessionStatusPoller] Polling cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionStatusPoller] Error: {ex.Message}");
            if (onStatusChanged != null)
                await onStatusChanged(MeditationSessionStatus.FAILED, "Error checking session status.");
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}