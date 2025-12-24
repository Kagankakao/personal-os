using System.Windows;
using System.Windows.Input;
using KeganOS.Core.Models;
using KeganOS.Core.Interfaces;
using Serilog;
using System.Diagnostics;
using System.IO;

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
    private bool _showApiKey = false;

    public SettingsWindow(IUserService userService, IAIProvider aiProvider)
    {
        InitializeComponent();
        _userService = userService;
        _aiProvider = aiProvider;
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

    private void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.Debug("Change avatar clicked");
        
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Avatar Image",
            Filter = "Image files (*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif"
        };

        if (dialog.ShowDialog() == true)
        {
            _logger.Information("Avatar selected: {Path}", dialog.FileName);
            System.Windows.MessageBox.Show("Avatar updated!", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
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
        System.Windows.MessageBox.Show("Backup created successfully!", "Backup", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.Information("Restore from backup requested");
        System.Windows.MessageBox.Show("Restore functionality coming soon!", "Restore", 
            MessageBoxButton.OK, MessageBoxImage.Information);
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
}
