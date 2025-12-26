using Serilog;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace KeganOS.Views;

/// <summary>
/// KEGOMODORO timer settings window
/// </summary>
public partial class KegomoDoroSettingsWindow : System.Windows.Window
{
    private readonly ILogger _logger = Log.ForContext<KegomoDoroSettingsWindow>();
    private readonly string _configPath;
    private readonly string _imagesPath;
    private string? _newFireImagePath;
    private string? _newFloatingImagePath;
    
    public bool SettingsChanged { get; private set; }
    public bool ImageChanged { get; private set; }
    public Action? OnImageChanged { get; set; }

    public KegomoDoroSettingsWindow()
    {
        InitializeComponent();
        
        // Find KEGOMODORO paths
        var possiblePaths = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "kegomodoro")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "kegomodoro")),
            @"C:\Users\ariba\OneDrive\Documenti\Software Projects\AI Projects\personal-os\personal-os\kegomodoro"
        };

        _configPath = "";
        _imagesPath = "";
        foreach (var path in possiblePaths)
        {
            var configFile = Path.Combine(path, "dependencies", "texts", "Configurations", "configuration.csv");
            var imagesDir = Path.Combine(path, "dependencies", "images");
            if (File.Exists(configFile))
            {
                _configPath = configFile;
                _imagesPath = imagesDir;
                break;
            }
        }
        
        _logger.Information("KegomoDoroSettingsWindow initialized, config path: {Path}", _configPath);
        
        LoadSettings();
        LoadCurrentImages();
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

    private void LoadCurrentImages()
    {
        try
        {
            // Load fire image preview
            var fireImagePath = Path.Combine(_imagesPath, "main_image.png");
            if (File.Exists(fireImagePath))
            {
                FireImagePreview.Source = LoadImageWithoutCache(fireImagePath);
            }

            // Load floating image preview
            var floatingImagePath = Path.Combine(_imagesPath, "behelit.png");
            if (File.Exists(floatingImagePath))
            {
                FloatingImagePreview.Source = LoadImageWithoutCache(floatingImagePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load image previews");
        }
    }

    private static BitmapImage LoadImageWithoutCache(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        return bitmap;
    }

    private void BrowseFireImage_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Fire Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _newFireImagePath = dialog.FileName;
                FireImagePath.Text = Path.GetFileName(_newFireImagePath);
                FireImagePreview.Source = LoadImageWithoutCache(_newFireImagePath);
                _logger.Information("Selected new fire image: {Path}", _newFireImagePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load selected image");
                System.Windows.MessageBox.Show($"Failed to load image: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void BrowseFloatingImage_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Floating Widget Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _newFloatingImagePath = dialog.FileName;
                FloatingImagePath.Text = Path.GetFileName(_newFloatingImagePath);
                FloatingImagePreview.Source = LoadImageWithoutCache(_newFloatingImagePath);
                _logger.Information("Selected new floating image: {Path}", _newFloatingImagePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load selected floating image");
                System.Windows.MessageBox.Show($"Failed to load image: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
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
            var content = $"WORK_MIN,SHORT_BREAK_MIN,LONG_BREAK_MIN,NOTEPAD_MODE\n{workDuration},{shortBreak},{longBreak},False";
            File.WriteAllText(_configPath, content);
            
            SettingsChanged = true;
            _logger.Information("Settings saved: Work={Work}, ShortBreak={Short}, LongBreak={Long}",
                workDuration, shortBreak, longBreak);

            // Copy new fire image if selected
            if (!string.IsNullOrEmpty(_newFireImagePath) && File.Exists(_newFireImagePath))
            {
                var destPath = Path.Combine(_imagesPath, "main_image.png");
                File.Copy(_newFireImagePath, destPath, overwrite: true);
                ImageChanged = true;
                _logger.Information("Fire image updated: {Source} -> {Dest}", _newFireImagePath, destPath);
            }

            // Copy new floating image if selected
            if (!string.IsNullOrEmpty(_newFloatingImagePath) && File.Exists(_newFloatingImagePath))
            {
                var destPath = Path.Combine(_imagesPath, "behelit.png");
                File.Copy(_newFloatingImagePath, destPath, overwrite: true);
                ImageChanged = true;
                _logger.Information("Floating image updated: {Source} -> {Dest}", _newFloatingImagePath, destPath);
            }

            // Notify MainWindow to reload images
            if (ImageChanged)
            {
                OnImageChanged?.Invoke();
            }
            
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

    private void OpenThemeGallery_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _logger.Information("Opening Theme Gallery from KEGOMODORO settings...");
        
        // Get the theme service from App's service provider
        var app = (App)System.Windows.Application.Current;
        var themeService = app.Services.GetService(typeof(KeganOS.Core.Interfaces.IThemeService)) as KeganOS.Core.Interfaces.IThemeService;
        
        if (themeService != null)
        {
            var gallery = new ThemeGalleryWindow(themeService);
            gallery.Owner = this;
            gallery.ShowDialog();
        }
        else
        {
            System.Windows.MessageBox.Show("Theme service not available.", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }
}
