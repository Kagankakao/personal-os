using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for managing journal text file operations
/// </summary>
public interface IJournalService
{
    /// <summary>
    /// Get the full path to the user's journal file
    /// </summary>
    string GetJournalFilePath(User user);

    /// <summary>
    /// Read all entries from the journal text file
    /// </summary>
    Task<IEnumerable<JournalEntry>> ReadEntriesAsync(User user);

    /// <summary>
    /// Append a new entry to the journal text file
    /// </summary>
    Task AppendEntryAsync(User user, string note, TimeSpan? timeWorked = null);

    /// <summary>
    /// Append only a note to the journal (no time) - for Save to Journal button
    /// If date exists, appends note to existing entry without touching time
    /// If date doesn't exist, creates new entry with just date + note
    /// </summary>
    Task AppendNoteOnlyAsync(User user, string note);

    /// <summary>
    /// Open the journal file in Notepad
    /// </summary>
    void OpenInNotepad(User user);
}
