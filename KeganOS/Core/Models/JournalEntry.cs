namespace KeganOS.Core.Models;

/// <summary>
/// Represents a parsed journal entry from the user's text file
/// </summary>
public class JournalEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? TimeWorked { get; set; }  // HH:MM:SS format
    public string NoteText { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;  // Original text from file
    public string? MoodDetected { get; set; }  // AI: positive/neutral/negative
    public bool IsMilestone { get; set; }
    public bool IsArchived { get; set; }  // True if deleted from text file
    public byte[]? Embedding { get; set; }  // Vector for RAG
    public DateTime SyncedAt { get; set; } = DateTime.Now;
}
