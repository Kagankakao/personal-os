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
    
    private readonly IBackupService _backupService;
    
    public User? SelectedUser { get; private set; }

    public ProfileSelectionWindow(IUserService userService, IPixelaService pixelaService, IBackupService backupService)
    {
        InitializeComponent();
        _userService = userService;
        _pixelaService = pixelaService;
        _backupService = backupService;
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

            // Fetch Pixe.la stats in background for each user to update display
            foreach (var user in users)
            {
                if (_pixelaService.IsConfigured(user))
                {
                    FetchPixelaStats(user);
                }
            }
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to load profiles");
            System.Windows.MessageBox.Show($"Failed to load profiles: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async void FetchPixelaStats(User user)
    {
        try 
        {
            _logger.Information("Fetching Pixe.la hours for {User}", user.DisplayName);
            var stats = await _pixelaService.GetStatsAsync(user);
            if (stats != null)
            {
                // Update local hours for display only
                user.TotalHours = stats.TotalQuantity;
                _logger.Information("Updated {User} hours from Pixe.la: {Hours}", user.DisplayName, user.TotalHours);
                
                // Refresh display
                Dispatcher.Invoke(() => ProfileList.Items.Refresh());
            }
        }
        catch (System.Exception ex)
        {
            _logger.Warning(ex, "Could not fetch Pixe.la stats for {User}", user.DisplayName);
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
        var createWindow = new CreateProfileWindow(_userService, _pixelaService, _backupService);
        
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

    private async void EditProfile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Stop event from bubbling to card click
        e.Handled = true;
        
        if (sender is System.Windows.Controls.Button button && button.Tag is int userId)
        {
            _logger.Information("Edit profile requested for user {UserId}", userId);
            
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null) return;

                var editWindow = new CreateProfileWindow(_userService, _pixelaService, _backupService, user);
                editWindow.Owner = this;
                
                if (editWindow.ShowDialog() == true)
                {
                    _logger.Information("Profile updated: {DisplayName}", user.DisplayName);
                    LoadProfiles();
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Failed to edit profile {UserId}", userId);
                System.Windows.MessageBox.Show($"Failed to edit profile: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private async void DeleteProfile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Stop event from bubbling to card click
        e.Handled = true;
        
        if (sender is System.Windows.Controls.Button button && button.Tag is int userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null) return;

                _logger.Information("Delete profile requested for user {DisplayName}", user.DisplayName);

                // Show confirmation dialog - user must type profile name
                var confirmName = Microsoft.VisualBasic.Interaction.InputBox(
                    $"To delete '{user.DisplayName}', type the profile name exactly to confirm:",
                    "Delete Profile",
                    "");

                if (confirmName == user.DisplayName)
                {
                    // Backup user data before deletion
                    var backupPath = BackupUserData(user);
                    if (!string.IsNullOrEmpty(backupPath))
                    {
                        _logger.Information("Pre-deletion backup created: {Path}", backupPath);
                    }
                    
                    await _userService.DeleteUserAsync(userId);
                    _logger.Information("Deleted profile {DisplayName} by user confirmation", user.DisplayName);
                    
                    var message = $"Profile '{user.DisplayName}' has been deleted.";
                    if (!string.IsNullOrEmpty(backupPath))
                    {
                        message += $"\n\nBackup saved to:\n{backupPath}";
                    }
                    
                    System.Windows.MessageBox.Show(message, "Deleted",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    
                    LoadProfiles();
                }
                else if (!string.IsNullOrEmpty(confirmName))
                {
                    _logger.Warning("Delete cancelled - name mismatch: expected {Expected}, got {Actual}", 
                        user.DisplayName, confirmName);
                    System.Windows.MessageBox.Show("Profile name did not match. Deletion cancelled.", "Cancelled",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Failed to delete profile {UserId}", userId);
                System.Windows.MessageBox.Show($"Failed to delete profile: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private string BackupUserData(Core.Models.User user)
    {
        try
        {
            var backupDir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "KeganOS", "backups", "deleted_profiles");
            
            System.IO.Directory.CreateDirectory(backupDir);
            
            var safeUserName = string.Join("_", user.DisplayName.Split(System.IO.Path.GetInvalidFileNameChars()));
            var backupFileName = $"{safeUserName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            var backupPath = System.IO.Path.Combine(backupDir, backupFileName);
            
            // Create JSON backup of user data
            var userData = new
            {
                user.Id,
                user.DisplayName,
                user.PersonalSymbol,
                user.AvatarPath,
                user.JournalFileName,
                user.PixelaUsername,
                user.TotalHours,
                user.CreatedAt,
                DeletedAt = System.DateTime.Now
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(userData, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            System.IO.File.WriteAllText(backupPath, json);
            
            _logger.Information("User data backup created: {Path}", backupPath);
            return backupPath;
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to backup user data before deletion");
            return "";
        }
    }
}
