using Serilog;
using System;
using System.IO;
using System.Windows.Media.Imaging;
using KeganOS.Core.Models;

namespace KeganOS.Views;

/// <summary>
/// KEGOMODORO timer settings window
/// </summary>
public partial class KegomoDoroSettingsWindow : System.Windows.Window
{
    private readonly ILogger _logger = Log.ForContext<KegomoDoroSettingsWindow>();
    private readonly string _configPath;
    private readonly User? _currentUser;
    
    public bool SettingsChanged { get; private set; }
    public bool ImageChanged { get; private set; }
    public Action? OnImageChanged { get; set; }

    public KegomoDoroSettingsWindow(User? currentUser = null)
    {
        InitializeComponent();
        _currentUser = currentUser;
        
        // Find KEGOMODORO paths
        var possiblePaths = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "kegomodoro")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "kegomodoro")),
            @"C:\Users\ariba\OneDrive\Documenti\Software Projects\AI Projects\personal-os\personal-os\kegomodoro"
        };

        _configPath = "";
        
        // Find the base path first
        string? activeBasePath = null;
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(Path.Combine(path, "dependencies")))
            {
                activeBasePath = path;
                break;
            }
        }

        if (activeBasePath != null)
        {
            if (_currentUser != null)
            {
                var userTextsDir = Path.Combine(activeBasePath, "dependencies", "texts", "Users", _currentUser.DisplayName);
                Directory.CreateDirectory(userTextsDir);
                _configPath = Path.Combine(userTextsDir, "configuration.csv");
                
                _logger.Information("Using user-specific settings for {User}: {Path}", _currentUser.DisplayName, _configPath);
                
                // If user config doesn't exist yet, copy from global
                if (!File.Exists(_configPath))
                {
                    var globalConfig = Path.Combine(activeBasePath, "dependencies", "texts", "Configurations", "configuration.csv");
                    if (File.Exists(globalConfig))
                    {
                        File.Copy(globalConfig, _configPath);
                        _logger.Information("Copied global config to user folder: {Path}", _configPath);
                    }
                }
            }
            else
            {
                // Fallback to global config
                _configPath = Path.Combine(activeBasePath, "dependencies", "texts", "Configurations", "configuration.csv");
                _logger.Information("No user logged in, using global settings: {Path}", _configPath);
            }
        }
        
        _logger.Information("KegomoDoroSettingsWindow initialized, config path: {Path}", _configPath);
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath))
        {
            _logger.Warning("Config file not found, using defaults");
            return;
        }

        try
        {
            var lines = File.ReadAllLines(_configPath);
            if (lines.Length >= 2)
            {
                var values = lines[1].Split(',');
                if (values.Length >= 3)
                {
                    WorkDurationInput.Text = values[0].Trim();
                    ShortBreakInput.Text = values[1].Trim();
                    LongBreakInput.Text = values[2].Trim();
                }
            }
            _logger.Information("Settings loaded from config file");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load settings");
        }
    }

    private void SaveSettings()
    {
        if (string.IsNullOrEmpty(_configPath))
        {
            _logger.Error("Cannot save - config path not set");
            System.Windows.MessageBox.Show("KEGOMODORO configuration file not found.", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            var mainWindow = Owner as MainWindow;

            // Validate inputs
            if (!int.TryParse(WorkDurationInput.Text, out var workDuration) || workDuration <= 0)
            {
                mainWindow?.ShowToast("Invalid work duration.", "⚠️", "#FFCC00", "Warning");
                return;
            }

            if (!int.TryParse(ShortBreakInput.Text, out var shortBreak) || shortBreak <= 0)
            {
                mainWindow?.ShowToast("Invalid short break duration.", "⚠️", "#FFCC00", "Warning");
                return;
            }

            if (!int.TryParse(LongBreakInput.Text, out var longBreak) || longBreak <= 0)
            {
                mainWindow?.ShowToast("Invalid long break duration.", "⚠️", "#FFCC00", "Warning");
                return;
            }

            // Write to CSV with KEGOMODORO expected format
            var content = $"WORK_MIN,SHORT_BREAK_MIN,LONG_BREAK_MIN,NOTEPAD_MODE\n{workDuration},{shortBreak},{longBreak},False";
            File.WriteAllText(_configPath, content);
            
            SettingsChanged = true;
            _logger.Information("Settings saved: Work={Work}, ShortBreak={Short}, LongBreak={Long}",
                workDuration, shortBreak, longBreak);

            mainWindow?.ShowToast("Settings saved successfully!", "⚙️", "#44CC44", "Settings Saved");
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save settings");
            System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

    private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

    private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e) => SaveSettings();

    private void OpenThemeGallery_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _logger.Information("Opening Theme Gallery from KEGOMODORO settings...");
        
        // Get the theme service from App's service provider
        var app = (App)System.Windows.Application.Current;
        var themeService = app.Services.GetService(typeof(KeganOS.Core.Interfaces.IThemeService)) as KeganOS.Core.Interfaces.IThemeService;
        
        if (themeService != null)
        {
            var gallery = new ThemeGalleryWindow(themeService, _currentUser);
            gallery.Owner = this;
            gallery.ShowDialog();
            
            if (gallery.ImageChanged)
            {
                this.ImageChanged = true;
                OnImageChanged?.Invoke();
            }
        }
        else
        {
            System.Windows.MessageBox.Show("Theme service not available.", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }
}
