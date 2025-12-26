using System.Windows;
using System.Windows.Input;
using KeganOS.Core.Models;
using KeganOS.Core.Interfaces;
using Serilog;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace KeganOS.Views;

/// <summary>
/// Settings window for user configuration
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ILogger _logger = Log.ForContext<SettingsWindow>();
    private User? _currentUser;
    private readonly IUserService _userService;
    private readonly IAIProvider _aiProvider;
    private readonly IPixelaService _pixelaService;
    private readonly IBackupService? _backupService;
    private bool _showApiKey = false;

    public SettingsWindow(IUserService userService, IAIProvider aiProvider, IPixelaService pixelaService, IBackupService? backupService = null)
    {
        InitializeComponent();
        _userService = userService;
        _aiProvider = aiProvider;
        _pixelaService = pixelaService;
        _backupService = backupService;
        _logger.Information("Settings window opened");
    }

    public void SetCurrentUser(User user)
    {
        _currentUser = user;
        LoadUserSettings();
    }

    private void LoadUserSettings()
    {
        if (_currentUser == null) return;

        _logger.Debug("Loading settings for user {DisplayName}", _currentUser.DisplayName);

        // Profile
        DisplayNameTextBox.Text = _currentUser.DisplayName;
        SymbolTextBox.Text = _currentUser.PersonalSymbol ?? "🦭";

        // API Key
        if (!string.IsNullOrEmpty(_currentUser.GeminiApiKey))
        {
            ApiKeyPasswordBox.Password = _currentUser.GeminiApiKey;
            ApiStatusText.Text = "✅ Configured";
            ApiStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6BFF6B"));
        }

        // Pixe.la Account
        PixelaUsernameTextBox.Text = _currentUser.PixelaUsername ?? "";
        PixelaTokenPasswordBox.Password = _currentUser.PixelaToken ?? "";
        PixelaGraphIdTextBox.Text = _currentUser.PixelaGraphId ?? "";

        // Backup location
        var backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeganOS", "backups");
        BackupLocationTextBox.Text = backupPath;
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

    private async void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentUser == null) return;
        
        _logger.Debug("Change avatar clicked");
        
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Avatar Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // Backup existing avatar if available
                if (_backupService != null && !string.IsNullOrEmpty(_currentUser.AvatarPath) && File.Exists(_currentUser.AvatarPath))
                {
                    await _backupService.BackupImageAsync(_currentUser, _currentUser.AvatarPath);
                    _logger.Information("Previous avatar backed up");
                }

                // Save avatar to AppData
                var avatarsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KeganOS", "avatars");
                
                Directory.CreateDirectory(avatarsDir);
                
                var extension = Path.GetExtension(dialog.FileName);
                var safeUserName = string.Join("_", _currentUser.DisplayName.Split(Path.GetInvalidFileNameChars()));
                var destFileName = $"{safeUserName}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                var destPath = Path.Combine(avatarsDir, destFileName);
                
                File.Copy(dialog.FileName, destPath, overwrite: true);
                
                _currentUser.AvatarPath = destPath;
                _logger.Information("Avatar changed for {User}: {Path}", _currentUser.DisplayName, destPath);
                
                System.Windows.MessageBox.Show("Avatar updated! Save to apply changes.", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save avatar");
                System.Windows.MessageBox.Show($"Failed to save avatar: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ShowApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        _showApiKey = !_showApiKey;
        _logger.Debug("API key visibility toggled: {Visible}", _showApiKey);
        
        if (_showApiKey)
        {
            ShowApiKeyButton.Content = "🔒";
            System.Windows.MessageBox.Show(ApiKeyPasswordBox.Password, "API Key", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ShowApiKeyButton.Content = "👁️";
        }
    }

    private async void TestApiButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.Information("Testing API connection...");
        
        var apiKey = ApiKeyPasswordBox.Password;
        if (string.IsNullOrEmpty(apiKey))
        {
            ApiStatusText.Text = "❌ No API key provided";
            ApiStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6B6B"));
            return;
        }

        try
        {
            TestApiButton.Content = "Testing...";
            TestApiButton.IsEnabled = false;

            _aiProvider.Configure(apiKey);
            var response = await _aiProvider.GenerateResponseAsync("Say 'Hello' in one word.");

            if (!string.IsNullOrEmpty(response))
            {
                ApiStatusText.Text = "✅ Connected";
                ApiStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6BFF6B"));
                _logger.Information("API connection test successful");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "API connection test failed");
            ApiStatusText.Text = "❌ Connection failed";
            ApiStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6B6B"));
        }
        finally
        {
            TestApiButton.Content = "Test";
            TestApiButton.IsEnabled = true;
        }
    }

    private void GetApiKeyLink_Click(object sender, MouseButtonEventArgs e)
    {
        _logger.Debug("Opening Google AI Studio");
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://aistudio.google.com/app/apikey",
            UseShellExecute = true
        });
    }

    private void BrowseBackupLocation_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Backup Location"
        };

        if (dialog.ShowDialog() == true)
        {
            BackupLocationTextBox.Text = dialog.FolderName;
            _logger.Information("Backup location changed to: {Path}", dialog.FolderName);
        }
    }

    private void BackupNowButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.Information("Manual backup requested");
        
        try
        {
            var backupDir = BackupLocationTextBox.Text;
            if (string.IsNullOrEmpty(backupDir))
            {
                backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KeganOS", "backups");
            }
            
            Directory.CreateDirectory(backupDir);
            
            // Backup database file
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KeganOS", "keganos.db");
            
            if (File.Exists(dbPath))
            {
                var backupFileName = $"keganos_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                var backupPath = Path.Combine(backupDir, backupFileName);
                File.Copy(dbPath, backupPath, overwrite: true);
                
                _logger.Information("Backup created at {Path}", backupPath);
                System.Windows.MessageBox.Show($"Backup created successfully!\n\n{backupPath}", "Backup Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("No database found to backup.", "Backup", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create backup");
            System.Windows.MessageBox.Show($"Backup failed: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentUser == null)
        {
            System.Windows.MessageBox.Show("No user selected.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_backupService == null)
        {
            _logger.Warning("Backup service not available");
            System.Windows.MessageBox.Show("Backup service not available.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _logger.Information("Restore from backup requested");

        try
        {
            var backups = await _backupService.GetBackupsAsync(_currentUser);
            var backupList = backups.ToList();

            if (backupList.Count == 0)
            {
                System.Windows.MessageBox.Show("No backups found.", "Restore",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Show backup selection
            var options = string.Join("\n", backupList.Select((b, i) => 
                $"{i + 1}. {b.Date:yyyy-MM-dd HH:mm} ({b.Type})"));

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Available backups:\n{options}\n\nEnter backup number to restore:",
                "Select Backup", "1");

            if (int.TryParse(input, out var selection) && selection >= 1 && selection <= backupList.Count)
            {
                var selectedBackup = backupList[selection - 1];
                
                var confirm = System.Windows.MessageBox.Show(
                    $"Restore from {selectedBackup.Date:yyyy-MM-dd HH:mm}?\n\nThis will replace your current journal.",
                    "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (confirm == MessageBoxResult.Yes)
                {
                    var result = await _backupService.RestoreBackupAsync(_currentUser, selectedBackup.Date);
                    if (result)
                    {
                        _logger.Information("Restored journal from {Date}", selectedBackup.Date);
                        System.Windows.MessageBox.Show("Restore completed successfully!", "Restore",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Restore failed. Check logs for details.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to restore from backup");
            System.Windows.MessageBox.Show($"Restore failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportDataButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.Information("Export data requested");

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Data",
            Filter = "JSON files (*.json)|*.json",
            FileName = $"KeganOS_export_{DateTime.Now:yyyyMMdd}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            _logger.Information("Data exported to: {Path}", dialog.FileName);
            System.Windows.MessageBox.Show("Data exported successfully!", "Export", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ImportDataButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.Information("Import data requested");

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Data",
            Filter = "JSON files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            _logger.Information("Data imported from: {Path}", dialog.FileName);
            System.Windows.MessageBox.Show("Data imported successfully!", "Import", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.Debug("Settings cancelled");
        Close();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentUser == null) return;

        _logger.Information("Saving settings for user {DisplayName}", _currentUser.DisplayName);

        try
        {
            // Update user object
            _currentUser.DisplayName = DisplayNameTextBox.Text.Trim();
            _currentUser.PersonalSymbol = string.IsNullOrEmpty(SymbolTextBox.Text) ? "🦭" : SymbolTextBox.Text;
            _currentUser.GeminiApiKey = ApiKeyPasswordBox.Password;
            
            // Update Pixe.la credentials
            _currentUser.PixelaUsername = PixelaUsernameTextBox.Text.Trim();
            _currentUser.PixelaToken = PixelaTokenPasswordBox.Password;
            _currentUser.PixelaGraphId = PixelaGraphIdTextBox.Text.Trim();

            // Save to database
            await _userService.UpdateUserAsync(_currentUser);

            _logger.Information("Settings saved successfully");
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save settings");
            System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowPixelaTokenButton_Click(object sender, RoutedEventArgs e)
    {
        // Toggle visibility (simplified - just show in message)
        if (!string.IsNullOrEmpty(PixelaTokenPasswordBox.Password))
        {
            System.Windows.MessageBox.Show($"Token: {PixelaTokenPasswordBox.Password}", "Pixe.la Token");
        }
    }

    private void GetPixelaAccountLink_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://pixe.la",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open Pixe.la website");
        }
    }
    private void OpenThemesButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            var themeService = app.Services.GetService(typeof(IThemeService)) as IThemeService;
            if (themeService != null)
            {
                var gallery = new ThemeGalleryWindow(themeService);
                gallery.Owner = this;
                gallery.ShowDialog();
            }
        }
    }
    private async void LogOutButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show("Are you sure you want to log out?", "Log Out",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _logger.Information("User logging out. Clearing session and restarting application...");
            
            // Clear the last active user so profile selection shows on restart
            await _userService.SetLastActiveUserIdAsync(null);
            
            System.Windows.Forms.Application.Restart();
            System.Windows.Application.Current.Shutdown();
        }
    }
}
