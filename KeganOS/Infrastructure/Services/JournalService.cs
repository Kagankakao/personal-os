using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace KeganOS.Infrastructure.Services;

/// <summary>
/// Service for reading/writing journal text files
/// </summary>
public class JournalService : IJournalService
{
    private readonly ILogger _logger = Log.ForContext<JournalService>();
    private readonly string _kegomoDoroPath;

    public JournalService()
    {
        _kegomoDoroPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro");
        _logger.Debug("KEGOMODORO base path: {Path}", _kegomoDoroPath);
    }

    public string GetJournalFilePath(User user)
    {
        var path = Path.Combine(_kegomoDoroPath, "dependencies", "texts", user.JournalFileName);
        _logger.Debug("Journal file path for user {User}: {Path}", user.DisplayName, path);
        return path;
    }

    public async Task<IEnumerable<JournalEntry>> ReadEntriesAsync(User user)
    {
        var filePath = GetJournalFilePath(user);
        _logger.Information("Reading journal entries from: {Path}", filePath);

        var entries = new List<JournalEntry>();

        if (!File.Exists(filePath))
        {
            _logger.Warning("Journal file not found: {Path}", filePath);
            return entries;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var lines = content.Split('\n', StringSplitOptions.None);

            JournalEntry? currentEntry = null;
            var datePattern = new Regex(@"^(\d{1,2}/\d{1,2}/\d{4}|\d{1,2}\.\d{1,2}\.\d{4})");
            var timePattern = new Regex(@"^(\d{1,2}:\d{2}:\d{2}(?:\.\d+)?)\s*(.*)$");

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                // Check if it's a date line
                var dateMatch = datePattern.Match(trimmedLine);
                if (dateMatch.Success)
                {
                    // Save previous entry if exists
                    if (currentEntry != null)
                    {
                        entries.Add(currentEntry);
                    }

                    // Parse date
                    var dateStr = dateMatch.Groups[1].Value;
                    if (DateTime.TryParse(dateStr, out var date))
                    {
                        currentEntry = new JournalEntry
                        {
                            UserId = user.Id,
                            Date = date,
                            RawText = trimmedLine
                        };
                    }
                    continue;
                }

                // Check if it's a time line (part of current entry)
                if (currentEntry != null)
                {
                    var timeMatch = timePattern.Match(trimmedLine);
                    if (timeMatch.Success)
                    {
                        var timeStr = timeMatch.Groups[1].Value;
                        var note = timeMatch.Groups[2].Value.Trim();
                        
                        // Parse time worked
                        if (TimeSpan.TryParse(timeStr.Split('.')[0], out var timeWorked))
                        {
                            currentEntry.TimeWorked = timeWorked;
                        }
                        
                        currentEntry.NoteText = note;
                        currentEntry.RawText += "\n" + trimmedLine;
                    }
                    else
                    {
                        // It's additional note text
                        currentEntry.NoteText += (string.IsNullOrEmpty(currentEntry.NoteText) ? "" : " ") + trimmedLine;
                        currentEntry.RawText += "\n" + trimmedLine;
                    }
                }
            }

            // Don't forget the last entry
            if (currentEntry != null)
            {
                entries.Add(currentEntry);
            }

            _logger.Information("Parsed {Count} journal entries", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read journal entries");
        }

        return entries;
    }

    public async Task AppendEntryAsync(User user, string note, TimeSpan? timeWorked = null)
    {
        var filePath = GetJournalFilePath(user);
        _logger.Information("Saving entry to: {Path}", filePath);

        try
        {
            var today = DateTime.Now.Date;
            var dateStr = today.ToString("MM/dd/yyyy");
            
            // If no timeWorked provided, read from time.csv (KEGOMODORO's stopwatch data)
            var hoursFromTimeCsv = await GetTodayHoursFromTimeCsvAsync(user);
            var finalTime = timeWorked ?? hoursFromTimeCsv;
            var timeStr = finalTime.ToString(@"hh\:mm\:ss");
            
            _logger.Debug("Final time for entry: {Time} (from time.csv: {FromCsv})", 
                timeStr, timeWorked == null ? "yes" : "no");
            
            // Read existing file content
            var existingContent = File.Exists(filePath) ? await File.ReadAllTextAsync(filePath) : "";
            
            // Check if today's date already exists
            var lines = existingContent.Split('\n').ToList();
            var todayIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().StartsWith(dateStr))
                {
                    todayIndex = i;
                    break;
                }
            }
            
            if (todayIndex >= 0)
            {
                // Today exists - merge notes and update time
                _logger.Information("Merging with existing entry for {Date}", dateStr);
                
                // Find existing time and note on the next line
                if (todayIndex + 1 < lines.Count)
                {
                    var existingTimeLine = lines[todayIndex + 1].Trim();
                    var existingNote = "";
                    
                    // Extract existing note from time line
                    var spaceIdx = existingTimeLine.IndexOf(' ');
                    if (spaceIdx > 0)
                    {
                        existingNote = existingTimeLine.Substring(spaceIdx + 1).Trim();
                    }
                    
                    // Merge notes if new note provided and different
                    string mergedNote;
                    if (string.IsNullOrWhiteSpace(existingNote))
                    {
                        mergedNote = note;
                    }
                    else if (existingNote.Contains(note))
                    {
                        mergedNote = existingNote; // Note already exists
                    }
                    else
                    {
                        mergedNote = existingNote + "\n" + note;
                    }
                    
                    // Update the line with new time (always overwrite with latest) and merged note
                    lines[todayIndex + 1] = $"{timeStr} {mergedNote}";
                }
                
                // Write back
                await File.WriteAllTextAsync(filePath, string.Join('\n', lines));
                _logger.Information("Entry merged successfully");
            }
            else
            {
                // New date - append new entry
                var entry = $"\n\n{dateStr}\n{timeStr} {note}";
                await File.AppendAllTextAsync(filePath, entry);
                _logger.Information("New entry appended for {Date}", dateStr);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save entry");
            throw;
        }
    }
    
    /// <summary>
    /// Reads the last entry from time.csv to get today's accumulated hours
    /// </summary>
    private async Task<TimeSpan> GetTodayHoursFromTimeCsvAsync(User? user = null)
    {
        try
        {
            // Try user-specific folder first
            string timeCsvPath;
            if (user != null)
            {
                var userPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Users", user.DisplayName, "time.csv");
                if (File.Exists(userPath))
                {
                    timeCsvPath = userPath;
                }
                else
                {
                    timeCsvPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Configurations", "time.csv");
                }
            }
            else
            {
                timeCsvPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Configurations", "time.csv");
            }
            
            if (!File.Exists(timeCsvPath))
            {
                _logger.Debug("time.csv not found at {Path}", timeCsvPath);
                return TimeSpan.Zero;
            }
            
            var lines = await File.ReadAllLinesAsync(timeCsvPath);
            
            // Find last non-empty line with valid format
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                
                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    if (int.TryParse(parts[0], out var hours) &&
                        int.TryParse(parts[1], out var minutes) &&
                        int.TryParse(parts[2].Split(',')[0], out var seconds)) // Handle malformed lines like "10,25,00,0,0"
                    {
                        var result = new TimeSpan(hours, minutes, seconds);
                        _logger.Debug("Read time from time.csv: {Time}", result);
                        return result;
                    }
                }
            }
            
            return TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to read time.csv");
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Append only a note to the journal (no time modification)
    /// Used by Save to Journal button - never writes time, only notes
    /// </summary>
    public async Task AppendNoteOnlyAsync(User user, string note)
    {
        var filePath = GetJournalFilePath(user);
        _logger.Information("Saving note-only entry to: {Path}", filePath);

        try
        {
            var today = DateTime.Now.Date;
            var dateStrSlash = today.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);  // Forces 12/28/2025
            var dateStrDot = today.ToString("MM.dd.yyyy");    // 12.28.2025 (for searching old entries)
            
            // Read existing file content
            var existingContent = File.Exists(filePath) ? await File.ReadAllTextAsync(filePath) : "";
            var lines = existingContent.Split('\n').ToList();
            
            // Find today's date (check BOTH formats for cross-app compatibility)
            var todayIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                var lineTrim = lines[i].Trim();
                if (lineTrim == dateStrSlash || lineTrim == dateStrDot)
                {
                    todayIndex = i;
                    break;
                }
            }
            
            if (todayIndex >= 0 && todayIndex + 1 < lines.Count)
            {
                // Find where the NEXT DATE ENTRY starts (or end of file)
                var entryEndIndex = todayIndex + 2;
                while (entryEndIndex < lines.Count)
                {
                    var line = lines[entryEndIndex].Trim();
                    // Only stop at the next date entry (not at empty lines)
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{2}[/\.]\d{2}[/\.]\d{4}$"))
                    {
                        break;
                    }
                    entryEndIndex++;
                }
                // Check if time line has a note beside it
                var timeLine = lines[todayIndex + 1];
                var hasInlineNote = timeLine.Contains(' ');  // If space exists, note is inline
                var hasNotesBelow = entryEndIndex > todayIndex + 2;  // If lines exist below time line
                
                // Append note
                if (!lines.Any(l => l.Contains(note)))  // Check if note already exists
                {
                    // Only add inline if NO notes exist at all (neither inline nor below)
                    if (!hasInlineNote && !hasNotesBelow)
                    {
                        // No notes at all - add inline with time
                        lines[todayIndex + 1] = timeLine + " " + note;
                    }
                    else
                    {
                        // Notes exist (inline or below) - append at end
                        lines.Insert(entryEndIndex, "");  // Blank line before new note
                        lines.Insert(entryEndIndex + 1, note);
                    }
                    await File.WriteAllTextAsync(filePath, string.Join('\n', lines));
                    _logger.Information("Note appended to existing entry for {Date}", dateStrSlash);
                }
                else
                {
                    _logger.Information("Note already exists for {Date}, skipping", dateStrSlash);
                }
            }
            else
            {
                // No entry for today - add date + note (NO time)
                var entry = $"\n\n{dateStrSlash}\n{note}";
                await File.AppendAllTextAsync(filePath, entry);
                _logger.Information("New note-only entry created for {Date}", dateStrSlash);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save note-only entry");
            throw;
        }
    }

    public void OpenInNotepad(User user)
    {
        var filePath = GetJournalFilePath(user);
        _logger.Information("Opening journal in Notepad: {Path}", filePath);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open Notepad");
        }
    }
}
