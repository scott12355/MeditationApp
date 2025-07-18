using MeditationApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System.Text.Json;
using Microsoft.Maui.Networking;
using MeditationApp.Utils;

namespace MeditationApp.Services
{
    public class MoodChartService
    {
        private readonly MeditationSessionDatabase _database;
        private readonly CognitoAuthService _cognitoAuthService;
        private readonly GraphQLService _graphQLService;

        public MoodChartService(MeditationSessionDatabase database, CognitoAuthService cognitoAuthService, GraphQLService graphQLService)
        {
            _database = database;
            _cognitoAuthService = cognitoAuthService;
            _graphQLService = graphQLService;
        }

        public async Task<List<MoodDataPoint>> GetLastSevenDaysMoodDataAsync()
        {
            try
            {
                var userId = await GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return new List<MoodDataPoint>();
                }

                // Pull existing insights from server and upsert local DB
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    string query = await GraphQLQueryLoader.LoadQueryAsync("ListUserDailyInsights.graphql");
                    if (string.IsNullOrWhiteSpace(query))
                        query = @"query ListUserDailyInsights($userID: ID!) { listUserDailyInsights(userID: $userID) { date notes mood } }";
                    var result = await _graphQLService.QueryAsync(query, new { userID = userId });
                    if (result.RootElement.TryGetProperty("data", out var dataElem) &&
                        dataElem.TryGetProperty("listUserDailyInsights", out var insightsElem) &&
                        insightsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var elem in insightsElem.EnumerateArray())
                        {
                            // Parse date (ISO-8601 string)
                            DateTime date;
                            var dateStr = elem.GetProperty("date").GetString() ?? string.Empty;
                            DateTime.TryParse(dateStr, out date);
                            var notes = elem.GetProperty("notes").GetString();
                            int? mood = null;
                            if (elem.TryGetProperty("mood", out var moodElem) && moodElem.ValueKind == JsonValueKind.Number)
                                mood = moodElem.GetInt32();

                            var insight = new Models.UserDailyInsights
                            {
                                UserID = userId,
                                Date = date.Date,
                                Notes = notes ?? string.Empty,
                                Mood = mood,
                                IsSynced = true
                            };
                            await _database.SaveDailyInsightsAsync(insight);
                        }
                    }
                }

                var endDate = DateTime.Now.Date;
                var startDate = endDate.AddDays(-6); // Last 7 days including today

                var moodData = new List<MoodDataPoint>();

                // Generate data points for each of the last 7 days
                for (int i = 0; i < 7; i++)
                {
                    var date = endDate.AddDays(-i);
                    var insights = await _database.GetDailyInsightsAsync(userId, date);

                    moodData.Add(new MoodDataPoint
                    {
                        Date = date,
                        Mood = insights?.Mood,
                        HasData = insights?.Mood.HasValue == true
                    });
                }

                // Reverse to show oldest to newest
                moodData.Reverse();
                return moodData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching mood data: {ex.Message}");
                return new List<MoodDataPoint>();
            }
        }

        private async Task<string> GetCurrentUserId()
        {
            try
            {
                var accessToken = await SecureStorage.Default.GetAsync("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var attributes = await _cognitoAuthService.GetUserAttributesAsync(accessToken);
                    return attributes.FirstOrDefault(a => a.Name == "sub")?.Value ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting user ID: {ex.Message}");
            }
            return string.Empty;
        }
    }

    public class MoodDataPoint
    {
        public DateTime Date { get; set; }
        public int? Mood { get; set; }
        public bool HasData { get; set; }

        public string DayName => Date.ToString("ddd");
        public string DateString => Date.ToString("MMM d");

        public string MoodEmoji => Mood switch
        {
            1 => "😢",
            2 => "😕",
            3 => "😐",
            4 => "😊",
            5 => "😄",
            _ => ""
        };

        public string MoodDescription => Mood switch
        {
            1 => "Very Sad",
            2 => "Sad",
            3 => "Neutral",
            4 => "Happy",
            5 => "Very Happy",
            _ => "No Data"
        };
    }
}