using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Collections.Generic;

namespace KeganOS.Views;

/// <summary>
/// Profile selection window - no password, just click to login
/// </summary>
public partial class ProfileSelectionWindow : System.Windows.Window
{
    private readonly ILogger _logger = Log.ForContext<ProfileSelectionWindow>();
    private readonly IUserService _userService;
    private readonly IPixelaService _pixelaService;
    
    public User? SelectedUser { get; private set; }

    public ProfileSelectionWindow(IUserService userService, IPixelaService pixelaService)
    {
        InitializeComponent();
        _userService = userService;
        _pixelaService = pixelaService;
        _logger.Information("ProfileSelectionWindow initialized");
        
        LoadProfiles();
    }

    private async void LoadProfiles()
    {
        _logger.Debug("Loading user profiles...");
        
        try
        {
            var users = await _userService.GetAllUsersAsync();
            ProfileList.ItemsSource = users;
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to load profiles");
            System.Windows.MessageBox.Show($"Failed to load profiles: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private async void ProfileCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border border && border.Tag is int userId)
        {
            _logger.Information("User selected: {UserId}", userId);
            
            try
            {
                SelectedUser = await _userService.GetUserByIdAsync(userId);
                
                if (SelectedUser != null)
                {
                    // Update last login
                    await _userService.UpdateUserAsync(SelectedUser);
                    DialogResult = true;
                    Close();
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Failed to load user {UserId}", userId);
                System.Windows.MessageBox.Show($"Failed to load profile: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void CreateProfile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _logger.Information("Opening profile creation...");
        var createWindow = new CreateProfileWindow(_userService, _pixelaService);
        createWindow.Owner = this;
        
        if (createWindow.ShowDialog() == true && createWindow.CreatedUser != null)
        {
            SelectedUser = createWindow.CreatedUser;
            DialogResult = true;
            Close();
        }
        else
        {
            // Refresh the list in case a user was created
            LoadProfiles();
        }
    }
}
