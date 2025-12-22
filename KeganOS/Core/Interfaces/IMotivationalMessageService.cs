using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for generating motivational messages from user's journal
/// </summary>
public interface IMotivationalMessageService
{
    /// <summary>
    /// Get a random motivational message based on user's history
    /// </summary>
    Task<MotivationalMessage> GetMessageAsync(User user);

    /// <summary>
    /// Check for "this day X years ago" milestones
    /// </summary>
    Task<IEnumerable<JournalEntry>> GetMilestonesForTodayAsync(User user);

    /// <summary>
    /// Extract meaningful quotes from journal entries
    /// </summary>
    Task<IEnumerable<string>> ExtractQuotesAsync(IEnumerable<JournalEntry> entries);
}

/// <summary>
/// Types of motivational messages
/// </summary>
public enum MotivationalMessageType
{
    OwnQuote,       // User's own words reflected back
    Milestone,      // "X years ago today..."
    AIInterpretation // AI-interpreted insight from user's journey
}

/// <summary>
/// A motivational message to display
/// </summary>
public class MotivationalMessage
{
    public MotivationalMessageType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SourceQuote { get; set; }
    public DateTime? SourceDate { get; set; }
}
