using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for analyzing user data and generating insights
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Gets daily hours for a specific week (Monday to Sunday)
    /// </summary>
    /// <param name="user">The user to analyze</param>
    /// <param name="weekStartDate">The Monday of the requested week</param>
    /// <returns>Dictionary where Key is DayOfWeek (Mon-Sun) and Value is hours worked</returns>
    Task<Dictionary<DayOfWeek, double>> GetWeeklyDataAsync(User user, DateTime weekStartDate);

    /// <summary>
    /// Generates an AI insight for a specific category
    /// </summary>
    Task<string> GenerateInsightAsync(User user, DateTime weekStartDate, string insightType);

    /// <summary>
    /// Calculates the current active streak in days
    /// </summary>
    Task<int> CalculateCurrentStreakAsync(User user);
}
