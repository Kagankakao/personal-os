using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;

namespace KeganOS.Infrastructure.Services;

/// <summary>
/// Service for generating motivational messages from user's journal
/// </summary>
public class MotivationalMessageService : IMotivationalMessageService
{
    private readonly ILogger _logger = Log.ForContext<MotivationalMessageService>();
    private readonly IJournalService _journalService;
    private readonly IAIProvider? _aiProvider;

    // Fallback quotes for when AI is not available
    private static readonly string[] FallbackQuotes = 
    [
        "Every expert was once a beginner.",
        "The only way to do great work is to love what you do.",
        "Small steps lead to big changes.",
        "Progress, not perfection.",
        "You're doing better than you think.",
        "Discipline is choosing between what you want now and what you want most.",
        "Success is the sum of small efforts repeated day in and day out.",
        "The journey of a thousand miles begins with a single step."
    ];

    public MotivationalMessageService(IJournalService journalService, IAIProvider? aiProvider = null)
    {
        _journalService = journalService;
        _aiProvider = aiProvider;
        _logger.Debug("MotivationalMessageService initialized");
    }

    public async Task<MotivationalMessage> GetMessageAsync(User user)
    {
        _logger.Information("Getting motivational message for {User}", user.DisplayName);

        try
        {
            // First, check for milestones ("this day X years ago")
            var milestones = await GetMilestonesForTodayAsync(user);
            if (milestones.Any())
            {
                var milestone = milestones.First();
                var yearsAgo = DateTime.Today.Year - milestone.Date.Year;
                var previewText = milestone.NoteText.Length > 50 ? milestone.NoteText[..50] + "..." : milestone.NoteText;
                
                return new MotivationalMessage
                {
                    Type = MotivationalMessageType.Milestone,
                    Message = $"{yearsAgo} year{(yearsAgo > 1 ? "s" : "")} ago today: \"{previewText}\"",
                    SourceQuote = milestone.NoteText,
                    SourceDate = milestone.Date
                };
            }

            // Try to get a quote from user's journal
            var entries = await _journalService.ReadEntriesAsync(user);
            if (entries.Any())
            {
                var quotes = await ExtractQuotesAsync(entries);
                if (quotes.Any())
                {
                    var randomQuote = quotes.ElementAt(Random.Shared.Next(quotes.Count()));
                    
                    // If AI is available, interpret the quote
                    if (_aiProvider != null && _aiProvider.IsAvailable)
                    {
                        var interpretation = await _aiProvider.InterpretQuoteAsync(
                            randomQuote, 
                            $"From {user.DisplayName}'s personal journal");
                        
                        return new MotivationalMessage
                        {
                            Type = MotivationalMessageType.AIInterpretation,
                            Message = interpretation,
                            SourceQuote = randomQuote
                        };
                    }

                    return new MotivationalMessage
                    {
                        Type = MotivationalMessageType.OwnQuote,
                        Message = $"\"{randomQuote}\"",
                        SourceQuote = randomQuote
                    };
                }
            }

            // Fallback to generic motivational quote
            var fallbackQuote = FallbackQuotes[Random.Shared.Next(FallbackQuotes.Length)];
            return new MotivationalMessage
            {
                Type = MotivationalMessageType.OwnQuote,
                Message = $"\"{fallbackQuote}\""
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get motivational message");
            return new MotivationalMessage
            {
                Type = MotivationalMessageType.OwnQuote,
                Message = $"\"{FallbackQuotes[0]}\""
            };
        }
    }

    public async Task<IEnumerable<JournalEntry>> GetMilestonesForTodayAsync(User user)
    {
        _logger.Debug("Checking milestones for today");

        try
        {
            var entries = await _journalService.ReadEntriesAsync(user);
            var today = DateTime.Today;
            
            // Find entries from same day in previous years (minimum 1 year ago)
            var milestones = entries
                .Where(e => e.Date.Month == today.Month && 
                            e.Date.Day == today.Day && 
                            e.Date.Year < today.Year)
                .OrderByDescending(e => e.Date)
                .ToList();

            _logger.Information("Found {Count} milestones for today", milestones.Count);
            return milestones;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get milestones");
            return [];
        }
    }

    public Task<IEnumerable<string>> ExtractQuotesAsync(IEnumerable<JournalEntry> entries)
    {
        _logger.Debug("Extracting quotes from {Count} entries", entries.Count());

        var quotes = new List<string>();

        foreach (var entry in entries)
        {
            // Extract meaningful quotes from NoteText (lines that look like reflections)
            if (string.IsNullOrEmpty(entry.NoteText)) continue;
            
            var lines = entry.NoteText.Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Where(l => l.Length > 20 && l.Length < 200) // Reasonable quote length
                .Where(l => !l.StartsWith("##") && !l.StartsWith("-")) // Not headers or lists
                .Where(l => !l.Contains("http") && !l.Contains("@")) // Not links/emails
                .Where(l => l.Length > 0 && char.IsLetter(l[0])); // Starts with letter

            quotes.AddRange(lines);
        }

        // Shuffle and take top quotes
        var selectedQuotes = quotes
            .OrderBy(_ => Random.Shared.Next())
            .Take(10)
            .AsEnumerable();

        _logger.Debug("Extracted {Count} quotes", selectedQuotes.Count());
        return Task.FromResult(selectedQuotes);
    }
}
