using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
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
        try
        {
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
                    System.Windows.MessageBox.Show("Time added locally, but failed to update Pixe.la graph.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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

            _logger.Information("Manual time added: {Duration} on {Date}", duration, date.ToShortDateString());
            
            System.Windows.MessageBox.Show($"Added {duration:hh\\:mm\\:ss} on {date:dd/MM/yyyy}", "Time Added",
                MessageBoxButton.OK, MessageBoxImage.Information);

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
            System.Windows.MessageBox.Show($"Failed to add time: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
            System.Windows.MessageBox.Show("Please select a date.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        date = DateInput.SelectedDate.Value;

        if (date > DateTime.Today)
        {
            System.Windows.MessageBox.Show("Date cannot be in the future.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // Validate duration
        if (!int.TryParse(HoursInput.Text, out var hours) || hours < 0 || hours > 24)
        {
            System.Windows.MessageBox.Show("Hours must be between 0 and 24.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(MinutesInput.Text, out var minutes) || minutes < 0 || minutes > 59)
        {
            System.Windows.MessageBox.Show("Minutes must be between 0 and 59.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(SecondsInput.Text, out var seconds) || seconds < 0 || seconds > 59)
        {
            System.Windows.MessageBox.Show("Seconds must be between 0 and 59.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        duration = new TimeSpan(hours, minutes, seconds);

        if (duration == TimeSpan.Zero)
        {
            System.Windows.MessageBox.Show("Duration must be greater than 0.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
    /// Get the time.csv path
    /// </summary>
    private string GetTimeCsvPath()
    {
        var basePath = GetKegomoDoroBasePath();
        if (string.IsNullOrEmpty(basePath)) return string.Empty;
        
        return Path.Combine(basePath, "dependencies", "texts", "Configurations", "time.csv");
    }

    /// <summary>
    /// Save entry to both journey file and time.csv in KEGOMODORO format
    /// For Manual Add: Sum hours if date exists, merge notes with \n\n
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
                var dateStr = date.ToString("MM/dd/yyyy");
                var content = File.Exists(journeyPath) ? File.ReadAllText(journeyPath, System.Text.Encoding.UTF8) : "";
                
                // Check if date already exists in the file
                var datePattern = new System.Text.RegularExpressions.Regex(
                    $@"\n\n{System.Text.RegularExpressions.Regex.Escape(dateStr)}\n(\d{{2}}:\d{{2}}:\d{{2}})(.*)(?=\n\n|\Z)", 
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                
                var match = datePattern.Match(content);
                
                if (match.Success)
                {
                    // Date exists - sum the hours and merge notes
                    var existingTimeStr = match.Groups[1].Value;
                    var existingNotes = match.Groups[2].Value.Trim();
                    
                    // Parse existing time
                    if (TimeSpan.TryParse(existingTimeStr, out var existingTime))
                    {
                        var newTotalTime = existingTime.Add(duration);
                        var newTimeStr = newTotalTime.ToString(@"hh\:mm\:ss");
                        
                        // Merge notes with proper line break
                        var mergedNotes = existingNotes;
                        if (!string.IsNullOrEmpty(note))
                        {
                            mergedNotes = string.IsNullOrEmpty(existingNotes) 
                                ? note 
                                : $"{existingNotes}\n\n{note}";
                        }
                        
                        var newEntry = $"\n\n{dateStr}\n{newTimeStr}";
                        if (!string.IsNullOrEmpty(mergedNotes))
                        {
                            newEntry += $" {mergedNotes}";
                        }
                        
                        // Replace the old entry with the new one
                        content = datePattern.Replace(content, newEntry, 1);
                        File.WriteAllText(journeyPath, content, System.Text.Encoding.UTF8);
                        _logger.Information("SUCCESS: Merged time for {Date} - now {Time}", dateStr, newTotalTime);
                    }
                    else
                    {
                        // Couldn't parse existing time - append as new
                        AppendNewJourneyEntry(journeyPath, dateStr, duration, note);
                    }
                }
                else
                {
                    // Date doesn't exist - append new entry
                    AppendNewJourneyEntry(journeyPath, dateStr, duration, note);
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

        // Save to time.csv (append new row with total time)
        var timeCsvPath = GetTimeCsvPath();
        _logger.Information("Time CSV path resolved to: {Path}, exists: {Exists}", timeCsvPath, !string.IsNullOrEmpty(timeCsvPath) && File.Exists(timeCsvPath));
        
        if (!string.IsNullOrEmpty(timeCsvPath))
        {
            try
            {
                // KEGOMODORO time.csv format: hours,minute,second (one row per entry)
                var line = $"{(int)duration.TotalHours},{duration.Minutes},{duration.Seconds}";
                File.AppendAllText(timeCsvPath, $"\n{line}");
                _logger.Information("SUCCESS: Saved to time.csv: {Path}", timeCsvPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save to time.csv: {Path}", timeCsvPath);
                System.Windows.MessageBox.Show($"Error saving to time.csv: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            _logger.Warning("Time CSV path is empty - cannot save!");
        }
    }
    
    private void AppendNewJourneyEntry(string journeyPath, string dateStr, TimeSpan duration, string note)
    {
        var entry = $"\n\n{dateStr}\n{duration:hh\\:mm\\:ss}";
        if (!string.IsNullOrEmpty(note))
        {
            entry += $" {note}";
        }
        File.AppendAllText(journeyPath, entry, System.Text.Encoding.UTF8);
        _logger.Information("SUCCESS: Appended new entry to journey file: {Path}", journeyPath);
    }
}
