using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Text;

namespace KeganOS.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ILogger _logger = Log.ForContext<AnalyticsService>();
    private readonly IJournalService _journalService;
    private readonly IAIProvider _aiProvider;

    public AnalyticsService(IJournalService journalService, IAIProvider aiProvider)
    {
        _journalService = journalService;
        _aiProvider = aiProvider;
    }

    public async Task<Dictionary<DayOfWeek, double>> GetWeeklyDataAsync(User user, DateTime weekStartDate)
    {
        // Ensure we start on Monday
        var start = weekStartDate.Date;
        while (start.DayOfWeek != DayOfWeek.Monday)
        {
            start = start.AddDays(-1);
        }
        var end = start.AddDays(7); // Exclusive

        var weeklyData = new Dictionary<DayOfWeek, double>
        {
            { DayOfWeek.Monday, 0 },
            { DayOfWeek.Tuesday, 0 },
            { DayOfWeek.Wednesday, 0 },
            { DayOfWeek.Thursday, 0 },
            { DayOfWeek.Friday, 0 },
            { DayOfWeek.Saturday, 0 },
            { DayOfWeek.Sunday, 0 }
        };

        try
        {
            var entries = await _journalService.ReadEntriesAsync(user);
            var weeklyEntries = entries.Where(e => e.Date >= start && e.Date < end);

            foreach (var entry in weeklyEntries)
            {
                if (entry.TimeWorked.HasValue)
                {
                    weeklyData[entry.Date.DayOfWeek] += entry.TimeWorked.Value.TotalHours;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error calculating weekly data");
        }

        return weeklyData;
    }

    public async Task<string> GenerateInsightAsync(User user, DateTime weekStartDate, string insightType)
    {
        try
        {
            var entries = await _journalService.ReadEntriesAsync(user);
            // Get last 7 days from the specified week
            var start = weekStartDate.Date;
            var end = start.AddDays(7);
            var periodEntries = entries.Where(e => e.Date >= start && e.Date < end).ToList();

            if (!periodEntries.Any())
                return "No journal entries found for this week to generate insights.";

            var sb = new StringBuilder();
            sb.AppendLine($"User: {user.DisplayName}");
            sb.AppendLine($"Week: {start:d} to {end.AddDays(-1):d}");
            sb.AppendLine("Journal Entries:");
            foreach (var entry in periodEntries)
            {
                sb.AppendLine($"- {entry.Date:d}: {entry.NoteText} ({entry.TimeWorked?.TotalHours:F1} hrs)");
            }

            string prompt = insightType switch
            {
                "Notes" => "Summarize the key topics and projects I worked on this week based on my journal entries. Bullet points preferred.",
                "Focus" => "Analyze my productivity and focus this week. Identify patterns in when I worked and what I accomplished. Give constructive feedback.",
                "Life" => "Based on my journal, offer some philosophical or life advice relevant to my current journey. Be encouraging and wise.",
                _ => "Give me query-specific insights."
            };

            var fullPrompt = $"{prompt}\n\nContext:\n{sb}";
            return await _aiProvider.GenerateResponseAsync(fullPrompt);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error generating AI insight");
            return "Unable to generate insights at this time. Please check your internet connection and API key.";
        }
    }

    public async Task<int> CalculateCurrentStreakAsync(User user)
    {
        try
        {
            var entries = await _journalService.ReadEntriesAsync(user);
            if (!entries.Any()) return 0;

            var dates = entries.Select(e => e.Date.Date).Distinct().OrderByDescending(d => d).ToList();
            if (!dates.Any()) return 0;

            int streak = 0;
            DateTime checkDate = DateTime.Today;

            // If no entry today, check if there was one yesterday (streak still active)
            if (dates[0] < checkDate)
            {
                if (dates[0] == checkDate.AddDays(-1))
                {
                    checkDate = checkDate.AddDays(-1);
                }
                else
                {
                    return 0; // Streak broken
                }
            }

            foreach (var date in dates)
            {
                if (date == checkDate)
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }
                else
                {
                    break;
                }
            }

            return streak;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error calculating streak");
            return 0;
        }
    }
}
