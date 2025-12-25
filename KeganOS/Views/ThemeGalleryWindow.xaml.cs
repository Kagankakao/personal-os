using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Windows;
using System.Windows.Input;

namespace KeganOS.Views;

public partial class ThemeGalleryWindow : Window
{
    private readonly ILogger _logger = Log.ForContext<ThemeGalleryWindow>();
    private readonly IThemeService _themeService;
    private List<Theme> _themes = new();
    private int _currentIndex = 0;

    public ThemeGalleryWindow(IThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;
        
        LoadThemes();
    }

    private async void LoadThemes()
    {
        try
        {
            var themes = await _themeService.GetAvailableThemesAsync();
            _themes = themes.ToList();
            
            if (_themes.Count > 0)
            {
                _currentIndex = 0;
                UpdateCarousel();
            }
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to load themes");
            System.Windows.MessageBox.Show("Failed to load themes.");
        }
    }

    private void UpdateCarousel()
    {
        if (_themes.Count == 0) return;

        var theme = _themes[_currentIndex];
        
        // Update Card
        PreviewBackground.Background = theme.BackgroundBrush;
        PreviewAccent.Fill = theme.AccentBrush;
        PreviewText.Foreground = theme.TextColorBrush;
        ThemeNameText.Text = theme.Name;
        ThemeDescText.Text = theme.Description;

        // Update Dots
        DotsPanel.Children.Clear();
        for (int i = 0; i < _themes.Count; i++)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(4),
                Fill = (i == _currentIndex) ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85))
            };
            DotsPanel.Children.Add(dot);
        }

        // Enable Apply only if valid
        ApplyButton.IsEnabled = true;
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_themes.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _themes.Count;
        UpdateCarousel();
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_themes.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _themes.Count) % _themes.Count;
        UpdateCarousel();
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_themes.Count == 0) return;
        var selectedTheme = _themes[_currentIndex];

        try
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            ApplyButton.IsEnabled = false;

            _logger.Information("Applying theme: {Name}", selectedTheme.Name);
            bool success = await _themeService.ApplyThemeAsync(selectedTheme);

            if (success)
            {
                System.Windows.MessageBox.Show($"Theme '{selectedTheme.Name}' applied successfully!\n\nNote: You may need to restart the Timer app to see full changes.", 
                    "Theme Applied", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Failed to apply theme. Check logs for details.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Error implementing theme application");
            System.Windows.MessageBox.Show($"Error: {ex.Message}");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            ApplyButton.IsEnabled = true;
        }
    }

    private void CreateCustom_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Custom theme creator coming in Phase 15.5!", "Coming Soon");
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
