using Serilog;
using System;
using System.IO;

namespace KeganOS.Views;

/// <summary>
/// KEGOMODORO timer settings window
/// </summary>
public partial class KegomoDoroSettingsWindow : System.Windows.Window
{
    private readonly ILogger _logger = Log.ForContext<KegomoDoroSettingsWindow>();
    private readonly string _configPath;
    
    public bool SettingsChanged { get; private set; }

    public KegomoDoroSettingsWindow()
    {
        InitializeComponent();
        
        // Find KEGOMODORO config path
        var possiblePaths = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "kegomodoro")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "kegomodoro")),
            @"C:\Users\ariba\OneDrive\Documenti\Software Projects\AI Projects\personal-os\personal-os\kegomodoro"
        };

        _configPath = "";
        foreach (var path in possiblePaths)
        {
            var configFile = Path.Combine(path, "dependencies", "texts", "Configurations", "configuration.csv");
            if (File.Exists(configFile))
            {
                _configPath = configFile;
                break;
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
            // Validate inputs
            if (!int.TryParse(WorkDurationInput.Text, out var workDuration) || workDuration <= 0)
            {
                System.Windows.MessageBox.Show("Invalid work duration. Please enter a positive number.", "Validation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ShortBreakInput.Text, out var shortBreak) || shortBreak <= 0)
            {
                System.Windows.MessageBox.Show("Invalid short break duration. Please enter a positive number.", "Validation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(LongBreakInput.Text, out var longBreak) || longBreak <= 0)
            {
                System.Windows.MessageBox.Show("Invalid long break duration. Please enter a positive number.", "Validation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Write to CSV with KEGOMODORO expected format
            // Headers: WORK_MIN,SHORT_BREAK_MIN,LONG_BREAK_MIN,NOTEPAD_MODE
            var content = $"WORK_MIN,SHORT_BREAK_MIN,LONG_BREAK_MIN,NOTEPAD_MODE\n{workDuration},{shortBreak},{longBreak},False";
            File.WriteAllText(_configPath, content);
            
            SettingsChanged = true;
            _logger.Information("Settings saved: Work={Work}, ShortBreak={Short}, LongBreak={Long}",
                workDuration, shortBreak, longBreak);
            
            System.Windows.MessageBox.Show("Settings saved successfully!\n\nRestart KEGOMODORO for changes to take effect.", "Success",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            
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
}
