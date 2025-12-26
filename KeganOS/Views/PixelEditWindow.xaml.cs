using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System;
using System.Windows;
using System.Windows.Input;

namespace KeganOS.Views;

/// <summary>
/// Simple theme color picker for Pixe.la graphs
/// Only supports the 6 Pixe.la preset colors: shibafu, momiji, sora, ichou, ajisai, kuro
/// </summary>
public partial class PixelEditWindow : Window
{
    private readonly ILogger _logger = Log.ForContext<PixelEditWindow>();
    private readonly IPixelaService _pixelaService;
    private readonly User _user;

    public PixelEditWindow(IPixelaService pixelaService, IUserService userService, User user)
    {
        InitializeComponent();
        _pixelaService = pixelaService;
        _user = user;
        
        _logger.Debug("PixelEditWindow opened for theme selection");
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void PresetColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
        {
            var colorName = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(colorName)) return;

            _logger.Information("Applying Pixe.la theme: {Color}", colorName);
            
            var (success, error) = await _pixelaService.UpdateGraphAsync(_user, color: colorName);
            
            if (success)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show(error ?? "Failed to update theme", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
