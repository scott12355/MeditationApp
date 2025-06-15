using MeditationApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace MeditationApp.Services
{
    public class MoodChartService
    {
        private readonly MeditationSessionDatabase _database;
        private readonly CognitoAuthService _cognitoAuthService;

        public MoodChartService(MeditationSessionDatabase database, CognitoAuthService cognitoAuthService)
        {
            _database = database;
            _cognitoAuthService = cognitoAuthService;
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