using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace KeganOS.Views;

/// <summary>
/// Dialog for adding manual focus time entries
/// </summary>
public partial class AddManualTimeWindow : Window
{
    private readonly ILogger _logger = Log.ForContext<AddManualTimeWindow>();
    private readonly IPixelaService _pixelaService;
    private readonly IAchievementService _achievementService;
    private readonly User _currentUser;

    public AddManualTimeWindow(IPixelaService pixelaService, IAchievementService achievementService, User currentUser)
    {
        InitializeComponent();
        _pixelaService = pixelaService;
        _achievementService = achievementService;
        _currentUser = currentUser;
        
        DateInput.SelectedDate = DateTime.Today;
        _logger.Information("Add Manual Time dialog opened");
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DialogResult = false;
        }
        catch (InvalidOperationException)
        {
            // Window not opened as dialog
        }
        Close();
    }

    private async void AddTimeButton_Click(object sender, RoutedEventArgs e)
    {
        // Prevent multiple clicks
        AddTimeButton.IsEnabled = false;
        
        try
        {
            // Check if KEGOMODORO is running - prevent data conflicts
            var kegomoDoroService = ((App)System.Windows.Application.Current).Services.GetRequiredService<IKegomoDoroService>();
            if (kegomoDoroService.IsAnyInstanceRunning)
            {
                ShowToast("Close KEGOMODORO first!", "⚠️", "#FFBD2E");
                return;
            }

            // Validate and get values
            if (!ValidateInputs(out var date, out var duration, out var note))
                return;

            _logger.Information("Adding manual time: {Duration} on {Date}", duration, date.ToShortDateString());

            // Save to KEGOMODORO files (journey + time.csv)
            SaveToKegomoDoroFiles(date, duration, note);

            // Update Pixe.la if checked
            if (UpdatePixelaCheckBox.IsChecked == true && _pixelaService.IsConfigured(_currentUser))
            {
                var hours = duration.TotalHours;
                // Use IncrementPixelAsync to add to existing value for the day
                var result = await _pixelaService.IncrementPixelAsync(_currentUser, date, hours);
                
                if (result)
                {
                    _logger.Information("Pixe.la updated: Added {Hours}h to {Date}", hours, date.ToShortDateString());
                }
                else
                {
                    _logger.Warning("Failed to update Pixe.la");
                    ShowToast("Local sync OK, Pixe.la failed.", "⚠️", "#FFBD2E");
                }
            }
            else if (UpdatePixelaCheckBox.IsChecked == true)
            {
                _logger.Warning("Pixe.la update skipped - not configured for user {User}", _currentUser.DisplayName);
            }

            // XP and Achievement logic
            var totalHours = duration.TotalHours;
            int xpEarned = (int)(totalHours * 10); // 10 XP per hour
            if (xpEarned < 1) xpEarned = 1;

            _logger.Information("Awarding {XP} XP for {Hours}h", xpEarned, totalHours);
            await _achievementService.AddXpAsync(_currentUser, xpEarned);
            
            // Check for unlocks
            _currentUser.TotalHours += totalHours; // Update local tracker
            await _achievementService.CheckAchievementsAsync(_currentUser);

            _logger.Information("Manual time added successfully: {Duration} on {Date}", duration, date.ToShortDateString());
            
            // Show modern toast on MainWindow
            if (Owner is MainWindow main)
            {
                main.ShowToast($"Added {duration.TotalHours:F1}h to your journal!", "📖", "#44CC44");
            }

            // No popup - just close the window immediately
            try
            {
                DialogResult = true;
            }
            catch (InvalidOperationException)
            {
                // Window not opened as dialog
            }
            Close();

        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add manual time");
            ShowToast($"Error: {ex.Message}", "❌", "#FF4444");
        }
        finally
        {
            // Re-enable button in case of early return or error
            AddTimeButton.IsEnabled = true;
        }
    }

    private bool ValidateInputs(out DateTime date, out TimeSpan duration, out string note)
    {
        date = DateTime.Today;
        duration = TimeSpan.Zero;
        note = NoteInput.Text?.Trim() ?? "";

        // Validate date
        if (!DateInput.SelectedDate.HasValue)
        {
            ShowToast("Select a date.", "📅", "#FFBD2E");
            return false;
        }

        date = DateInput.SelectedDate.Value;

        if (date > DateTime.Today)
        {
            ShowToast("No future dates!", "📅", "#FFBD2E");
            return false;
        }

        // Validate duration
        if (!int.TryParse(HoursInput.Text, out var hours) || hours < 0 || hours > 24)
        {
            ShowToast("Hours: 0-24", "🕒", "#FFBD2E");
            return false;
        }

        if (!int.TryParse(MinutesInput.Text, out var minutes) || minutes < 0 || minutes > 59)
        {
            ShowToast("Minutes: 0-59", "🕒", "#FFBD2E");
            return false;
        }

        if (!int.TryParse(SecondsInput.Text, out var seconds) || seconds < 0 || seconds > 59)
        {
            ShowToast("Seconds: 0-59", "🕒", "#FFBD2E");
            return false;
        }

        duration = new TimeSpan(hours, minutes, seconds);

        if (duration == TimeSpan.Zero)
        {
            ShowToast("Time cannot be zero.", "🕒", "#FFBD2E");
            return false;
        }

        // Validate note length
        if (note.Length > 500)
        {
            System.Windows.MessageBox.Show("Note must be 500 characters or less.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void ShowToast(string message, string icon, string color)
    {
        if (Owner is MainWindow main)
        {
            main.ShowToast(message, icon, color);
        }
    }

    /// <summary>
    /// Get the KEGOMODORO base directory (relative to KeganOS location)
    /// </summary>
    private string GetKegomoDoroBasePath()
    {
        // Navigate from KeganOS bin folder to kegomodoro folder
        var possiblePaths = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "kegomodoro")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro")),
            @"C:\Users\ariba\OneDrive\Documenti\Software Projects\AI Projects\personal-os\personal-os\kegomodoro"
        };

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                _logger.Debug("Found KEGOMODORO at {Path}", path);
                return path;
            }
        }

        _logger.Warning("KEGOMODORO directory not found");
        return string.Empty;
    }

    /// <summary>
    /// Get the journey file path by finding the first .txt file in texts folder (skips .lnk files)
    /// </summary>
    private string GetJourneyFilePath()
    {
        var basePath = GetKegomoDoroBasePath();
        if (string.IsNullOrEmpty(basePath)) return string.Empty;
        
        // Try user-specific folder first
        if (_currentUser != null)
        {
            var userTextsPath = Path.Combine(basePath, "dependencies", "texts", "Users", _currentUser.DisplayName);
            _logger.Debug("Looking for user journey in: {Path}, exists: {Exists}", userTextsPath, Directory.Exists(userTextsPath));
            
            if (Directory.Exists(userTextsPath))
            {
                try
                {
                    // Exclude known non-journey files
                    var excludedFiles = new[] { "floating_window_checker.txt", "configuration.csv", "time.csv" };
                    var userTxtFiles = Directory.GetFiles(userTextsPath, "*.txt")
                        .Where(f => !f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        .Where(f => !excludedFiles.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    
                    if (userTxtFiles.Count > 0)
                    {
                        _logger.Information("Found user journey file: {Path}", userTxtFiles[0]);
                        return userTxtFiles[0];
                    }
                    else
                    {
                        _logger.Warning("No journey file found in user folder, only excluded files");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error finding user journey file");
                }
            }
        }
        
        // Fallback to global texts folder
        var textsPath = Path.Combine(basePath, "dependencies", "texts");
        if (!Directory.Exists(textsPath))
        {
            _logger.Warning("Texts directory not found: {Path}", textsPath);
            return string.Empty;
        }
        
        // Find the first .txt file (not .lnk) - this is the journey file
        try
        {
            var txtFiles = Directory.GetFiles(textsPath, "*.txt")
                .Where(f => !f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            if (txtFiles.Count > 0)
            {
                _logger.Debug("Found journey file: {Path}", txtFiles[0]);
                return txtFiles[0];
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error finding journey file");
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Get the time.csv path - prefers user-specific folder
    /// </summary>
    private string GetTimeCsvPath()
    {
        var basePath = GetKegomoDoroBasePath();
        if (string.IsNullOrEmpty(basePath)) return string.Empty;
        
        // Try user-specific folder first
        if (_currentUser != null)
        {
            var userPath = Path.Combine(basePath, "dependencies", "texts", "Users", _currentUser.DisplayName, "time.csv");
            if (File.Exists(userPath) || Directory.Exists(Path.GetDirectoryName(userPath)))
            {
                return userPath;
            }
        }
        
        // Fallback to global path
        return Path.Combine(basePath, "dependencies", "texts", "Configurations", "time.csv");
    }

    /// <summary>
    /// Save entry to both journey file and time.csv in KEGOMODORO format
    /// For Manual Add: Sum hours if date exists, append note on new line
    /// </summary>
    private void SaveToKegomoDoroFiles(DateTime date, TimeSpan duration, string note)
    {
        _logger.Information("SaveToKegomoDoroFiles called for {Date} with duration {Duration}", date, duration);
        
        // Save to journey file with smart merging
        var journeyPath = GetJourneyFilePath();
        _logger.Information("Journey path resolved to: {Path}, exists: {Exists}", journeyPath, !string.IsNullOrEmpty(journeyPath) && File.Exists(journeyPath));
        
        if (!string.IsNullOrEmpty(journeyPath))
        {
            try
            {
                var dateStrSlash = date.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);  // Forces 12/28/2025
                var dateStrDot = date.ToString("MM.dd.yyyy");    // 12.28.2025 (for searching old entries)
                var content = File.Exists(journeyPath) ? File.ReadAllText(journeyPath, System.Text.Encoding.UTF8) : "";
                var lines = content.Split('\n').ToList();
                
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
                    // Date exists - sum time and append note
                    var existingTimeLine = lines[todayIndex + 1];
                    
                    // Parse existing time (first part before space)
                    var spaceIdx = existingTimeLine.IndexOf(' ');
                    var existingTimeStr = spaceIdx > 0 ? existingTimeLine.Substring(0, spaceIdx) : existingTimeLine.Trim();
                    var existingFirstNote = spaceIdx > 0 ? existingTimeLine.Substring(spaceIdx + 1) : "";
                    
                    // Parse and sum times
                    TimeSpan newTotalTime = duration;
                    if (TimeSpan.TryParse(existingTimeStr, out var existingTime))
                    {
                        newTotalTime = existingTime.Add(duration);
                    }
                    
                    // Rebuild the time line - use total hours (not wrapped at 24)
                    var totalHours = (int)newTotalTime.TotalHours;
                    var newTimeStr = $"{totalHours:D2}:{newTotalTime.Minutes:D2}:{newTotalTime.Seconds:D2}";
                    var newLine = newTimeStr;
                    if (!string.IsNullOrEmpty(existingFirstNote))
                    {
                        newLine += " " + existingFirstNote;
                    }
                    
                    lines[todayIndex + 1] = newLine;
                    
                    // Find where the NEXT DATE ENTRY starts (or end of file)
                    // Skip past all notes and blank lines
                    var entryEndIndex = todayIndex + 2;
                    while (entryEndIndex < lines.Count)
                    {
                        var line = lines[entryEndIndex].Trim();
                        // Only stop at the next date entry (not at empty lines)
                        if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{2}[/\.]\d{2}[/\.]\d{4}$"))
                        {
                            break;  // Found next date, stop here (insert before this)
                        }
                        entryEndIndex++;
                    }
                    
                    // Append note at the end of this entry
                    if (!string.IsNullOrEmpty(note))
                    {
                        // Check if there are ANY notes: inline OR below the time line
                        var hasInlineNote = !string.IsNullOrEmpty(existingFirstNote);
                        var hasNotesBelow = entryEndIndex > todayIndex + 2;  // If entryEndIndex > date+time+1, there are lines below
                        
                        // Only add inline if there are NO notes at all (neither inline nor below)
                        if (!hasInlineNote && !hasNotesBelow)
                        {
                            lines[todayIndex + 1] = newLine + " " + note;
                        }
                        else
                        {
                            // Notes already exist (inline or below) - append at end with blank line
                            lines.Insert(entryEndIndex, "");  // Blank line before new note
                            lines.Insert(entryEndIndex + 1, note);
                        }
                    }
                    
                    // Write back
                    File.WriteAllText(journeyPath, string.Join('\n', lines), System.Text.Encoding.UTF8);
                    _logger.Information("SUCCESS: Merged time for {Date} - now {Time}", dateStrSlash, newTotalTime);
                }
                else
                {
                    // Date doesn't exist - append new entry
                    AppendNewJourneyEntry(journeyPath, dateStrSlash, duration, note);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save to journey file: {Path}", journeyPath);
                System.Windows.MessageBox.Show($"Error saving to journey: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            _logger.Warning("Journey path is empty - cannot save!");
        }

        // Save to time.csv - read existing, add manual time, overwrite
        // This ensures KEGOMODORO will continue from the correct accumulated time
        var timeCsvPath = GetTimeCsvPath();
        if (!string.IsNullOrEmpty(timeCsvPath))
        {
            try
            {
                // Read existing time from CSV
                TimeSpan existingTime = TimeSpan.Zero;
                if (File.Exists(timeCsvPath))
                {
                    var lines = File.ReadAllLines(timeCsvPath);
                    if (lines.Length >= 2)
                    {
                        var parts = lines[1].Split(',');
                        if (parts.Length >= 3 &&
                            int.TryParse(parts[0], out int h) &&
                            int.TryParse(parts[1], out int m) &&
                            int.TryParse(parts[2].Split(',')[0], out int s))
                        {
                            existingTime = new TimeSpan(h, m, s);
                        }
                    }
                }
                
                // Add manual time to existing
                var newTotal = existingTime.Add(duration);
                
                // Overwrite with new total (proper CSV format)
                var csvContent = $"hours,minute,second\n{(int)newTotal.TotalHours},{newTotal.Minutes},{newTotal.Seconds}\n";
                File.WriteAllText(timeCsvPath, csvContent);
                
                _logger.Information("SUCCESS: Updated time.csv: {OldTime} + {Added} = {NewTotal}", 
                    existingTime, duration, newTotal);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update time.csv");
            }
        }
    }
    
    private void AppendNewJourneyEntry(string journeyPath, string dateStr, TimeSpan duration, string note)
    {
        // Use total hours (not wrapped at 24)
        var totalHours = (int)duration.TotalHours;
        var timeStr = $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        var entry = $"\n\n{dateStr}\n{timeStr}";
        if (!string.IsNullOrEmpty(note))
        {
            entry += $" {note}";
        }
        File.AppendAllText(journeyPath, entry, System.Text.Encoding.UTF8);
        _logger.Information("SUCCESS: Appended new entry to journey file: {Path}", journeyPath);
    }
}
