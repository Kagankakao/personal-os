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
        _logger.Information("Appending entry to: {Path}", filePath);

        try
        {
            var date = DateTime.Now.ToString("MM/dd/yyyy");
            var time = timeWorked?.ToString(@"hh\:mm\:ss") ?? "00:00:00";
            
            var entry = $"\n\n{date}\n{time} {note}";
            
            await File.AppendAllTextAsync(filePath, entry);
            _logger.Information("Entry appended successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to append entry");
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
