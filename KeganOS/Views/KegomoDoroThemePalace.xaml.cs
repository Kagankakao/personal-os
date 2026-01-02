using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KeganOS.Views;

public partial class KegomoDoroThemePalace : Window
{
    private readonly ILogger _logger = Log.ForContext<KegomoDoroThemePalace>();
    private readonly IThemeService _themeService;
    private readonly User? _currentUser;
    private Theme _currentTheme;
    private string? _tempGuardianPath;
    private string? _tempHeartPath;

    public bool ImageChanged { get; private set; }

    public KegomoDoroThemePalace(IThemeService themeService, User? currentUser = null)
    {
        InitializeComponent();
        _themeService = themeService;
        _currentUser = currentUser;
        _currentTheme = new Theme { Name = "Custom Theme", IsCustom = true };
        
        LoadInitialTheme();
        LoadPresets();
        LoadUserImages();
    }

    private async void LoadInitialTheme()
    {
        try
        {
            var activeTheme = await _themeService.GetCurrentThemeAsync();
            ApplyThemeToPreview(activeTheme, false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load initial theme");
        }
    }

    private async void LoadPresets()
    {
        try
        {
            var themes = await _themeService.GetAvailableThemesAsync();
            foreach (var theme in themes)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = theme.BackgroundBrush,
                    ToolTip = theme.Name,
                    BorderBrush = theme.AccentBrush,
                    BorderThickness = new Thickness(2),
                    Tag = theme
                };
                btn.Click += Preset_Click;
                PresetsList.Items.Add(btn);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load presets");
        }
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is Theme theme)
        {
            ApplyThemeToPreview(theme, true);
        }
    }

    private void ApplyThemeToPreview(Theme theme, bool updateControls)
    {
        _currentTheme = theme;

        // Update Previews
        FloatingWindowBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 255)); // Always show Magenta background in preview for clarity
        FloatingTimerPreview.Foreground = theme.TextColorBrush;
        
        MainAppBackground.Background = theme.BackgroundBrush;
        MainTimerPreview.Foreground = theme.TextColorBrush;

        // Update Control Buttons if requested
        if (updateControls)
        {
            BgColorBtn.Background = theme.BackgroundBrush;
            TextColorBtn.Background = theme.TextColorBrush;
            AccentColorBtn.Background = theme.AccentBrush;
        }

        _logger.Information("Preview updated with theme: {Name}", theme.Name);
    }

    private void ColorBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
        {
            // Simple approach: Use System.Windows.Forms.ColorDialog with explicit namespace
            var dialog = new System.Windows.Forms.ColorDialog();
            
            // Try to set initial color
            if (btn.Background is SolidColorBrush currentBrush)
            {
                var c = currentBrush.Color;
                dialog.Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newColor = System.Windows.Media.Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                var newBrush = new SolidColorBrush(newColor);
                btn.Background = newBrush;

                string hex = $"#{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
                string type = btn.Tag?.ToString() ?? "";

                if (type == "BG") { _currentTheme.BackgroundColor = hex; MainAppBackground.Background = newBrush; }
                if (type == "Text") { _currentTheme.TextColor = hex; MainTimerPreview.Foreground = newBrush; FloatingTimerPreview.Foreground = newBrush; }
                if (type == "Accent") { _currentTheme.AccentColor = hex; }
            }
        }
    }

    private void ChangeImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var type = btn.Tag?.ToString();
                var image = new BitmapImage(new Uri(dialog.FileName));
                var fileName = Path.GetFileName(dialog.FileName);
                
                if (type == "Guardian")
                {
                    GuardianPreview.Source = image;
                    GuardianThumbnail.Source = image;
                    GuardianFileName.Text = fileName;
                    _tempGuardianPath = dialog.FileName;
                    _currentTheme.FloatingImagePath = fileName;
                }
                else if (type == "Heart")
                {
                    HeartPreview.Source = image;
                    HeartThumbnail.Source = image;
                    HeartFileName.Text = fileName;
                    _tempHeartPath = dialog.FileName;
                    _currentTheme.MainImagePath = fileName;
                }
            }
        }
    }

    private async void SaveTheme_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // If custom images were selected, we need to handle them
            // In a real implementation, we'd copy them to the app's assets or user folder
            // For now, let's assume ThemeService might handle this or we do it manually
            
            _logger.Information("Saving theme: {Name}", _currentTheme.Name);
            
            // 1. Apply colors and base settings
            bool success = await _themeService.ApplyThemeAsync(_currentTheme, _currentUser);
            
            if (success)
            {
                // 2. Handle Custom Images if they were picked from file system
                var activeBasePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro"));
                if (!Directory.Exists(Path.Combine(activeBasePath, "dependencies")))
                {
                    // Fallback path
                    activeBasePath = @"C:\Users\ariba\OneDrive\Documenti\Software Projects\AI Projects\personal-os\personal-os\kegomodoro";
                }

                string userImagesFolder;
                if (_currentUser != null)
                    userImagesFolder = Path.Combine(activeBasePath, "dependencies", "images", "Users", _currentUser.DisplayName);
                else
                    userImagesFolder = Path.Combine(activeBasePath, "dependencies", "images");

                Directory.CreateDirectory(userImagesFolder);

                if (!string.IsNullOrEmpty(_tempGuardianPath) && File.Exists(_tempGuardianPath))
                {
                    var dest = Path.Combine(userImagesFolder, "behelit.png");
                    File.Copy(_tempGuardianPath, dest, true);
                    _logger.Information("Custom Guardian image copied to: {Dest}", dest);
                }

                if (!string.IsNullOrEmpty(_tempHeartPath) && File.Exists(_tempHeartPath))
                {
                    var dest = Path.Combine(userImagesFolder, "main_image.png");
                    File.Copy(_tempHeartPath, dest, true);
                    _logger.Information("Custom Heart image copied to: {Dest}", dest);
                }

                if (!string.IsNullOrEmpty(_tempGuardianPath) || !string.IsNullOrEmpty(_tempHeartPath))
                {
                    ImageChanged = true;
                }

                if (Owner is ThemeGalleryWindow gallery && gallery.Owner is KegomoDoroSettingsWindow settings)
                {
                    if (ImageChanged) 
                    {
                        settings.OnImageChanged?.Invoke();
                    }
                }

                if (Owner is MainWindow main)
                    main.ShowToast("Theme Ascended! ✦", "🎨", "#00FFFF");

                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("The ritual failed. Check the logs.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save theme");
            System.Windows.MessageBox.Show($"Error: {ex.Message}");
        }
    }

    private async void SaveToGallery_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ThemeNameInput.Text))
            {
                System.Windows.MessageBox.Show("Please provide a name for your masterpiece.", "Naming Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var themeToSave = new Theme
            {
                Id = Guid.NewGuid().ToString(),
                Name = ThemeNameInput.Text,
                Description = $"Custom theme created on {DateTime.Now:yyyy-MM-dd}",
                BackgroundColor = _currentTheme.BackgroundColor,
                TextColor = _currentTheme.TextColor,
                AccentColor = _currentTheme.AccentColor,
                MainImagePath = _currentTheme.MainImagePath,
                FloatingImagePath = _currentTheme.FloatingImagePath,
                IsCustom = true,
                IsDark = _currentTheme.IsDark
            };

            _logger.Information("Saving custom theme '{Name}' to gallery", themeToSave.Name);
            bool success = await _themeService.SaveCustomThemeAsync(themeToSave);
            
            if (success)
            {
                if (Owner is MainWindow main)
                    main.ShowToast($"Theme '{themeToSave.Name}' saved to gallery!", "✨", "#88CC88");
                else
                    System.Windows.MessageBox.Show($"Theme '{themeToSave.Name}' successfully added to your collection.", "Theme Saved");
                
                // Refresh presets
                PresetsList.Items.Clear();
                LoadPresets();
            }
            else
            {
                System.Windows.MessageBox.Show("Failed to save theme to gallery.", "Gallery Error");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save to gallery");
            System.Windows.MessageBox.Show($"Error: {ex.Message}");
        }
    }

    private void BrowseMarketplace_Click(object sender, RoutedEventArgs e)
    {
        _logger.Information("User returning to marketplace...");
        DialogResult = false; // Close without applying current unsaved changes
        Close();
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void PresetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handled via button click instead
    }

    private void PreviewTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PreviewTabControl == null) return;
        
        // Show only the relevant asset panel based on selected tab
        if (PreviewTabControl.SelectedIndex == 0) // Floating tab
        {
            GuardianAssetPanel.Visibility = Visibility.Visible;
            HeartAssetPanel.Visibility = Visibility.Collapsed;
        }
        else // Main App tab
        {
            GuardianAssetPanel.Visibility = Visibility.Collapsed;
            HeartAssetPanel.Visibility = Visibility.Visible;
        }
    }

    private void LoadUserImages()
    {
        try
        {
            // Determine paths for user-specific images
            string imageFolder;
            if (_currentUser != null)
            {
                // User-specific images folder
                imageFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..",
                    "kegomodoro", "dependencies", "images", "Users", _currentUser.DisplayName);
            }
            else
            {
                // Fallback to theme assets
                imageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Themes");
            }

            // Load Guardian (Floating) image
            var guardianPath = Path.GetFullPath(Path.Combine(imageFolder, "behelit.png"));
            if (File.Exists(guardianPath))
            {
                var guardianImage = new BitmapImage(new Uri(guardianPath, UriKind.Absolute));
                GuardianPreview.Source = guardianImage;
                GuardianThumbnail.Source = guardianImage;
                _logger.Information("Loaded Guardian image from {Path}", guardianPath);
            }

            // Load Heart (Main) image
            var heartPath = Path.GetFullPath(Path.Combine(imageFolder, "main_image.png"));
            if (File.Exists(heartPath))
            {
                var heartImage = new BitmapImage(new Uri(heartPath, UriKind.Absolute));
                HeartPreview.Source = heartImage;
                HeartThumbnail.Source = heartImage;
                _logger.Information("Loaded Heart image from {Path}", heartPath);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load user images for preview");
        }
    }
}
