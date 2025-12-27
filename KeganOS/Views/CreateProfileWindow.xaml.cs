using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.IO;
using System.Text.RegularExpressions;

namespace KeganOS.Views;

/// <summary>
/// Create new user profile dialog
/// </summary>
public partial class CreateProfileWindow : System.Windows.Window
{
    private readonly ILogger _logger = Log.ForContext<CreateProfileWindow>();
    private readonly IUserService _userService;
    private readonly IPixelaService _pixelaService;
    private string? _selectedJournalPath;
    private int _validEntriesCount;
    private bool _pixelaUsernameAvailable = false;
    private bool _isNewPixelaUser = false;
    private User? _editingUser = null;
    private string? _selectedAvatarPath;
    
    private readonly IBackupService _backupService;

    public User? CreatedUser { get; private set; }

    public CreateProfileWindow(IUserService userService, IPixelaService pixelaService, IBackupService backupService)
        : this(userService, pixelaService, backupService, null) { }

    public CreateProfileWindow(IUserService userService, IPixelaService pixelaService, IBackupService backupService, User? editingUser)
    {
        InitializeComponent();
        _userService = userService;
        _pixelaService = pixelaService;
        _backupService = backupService;
        _editingUser = editingUser;
        
        if (_editingUser != null)
        {
            _logger.Information("CreateProfileWindow initialized in EDIT mode for {DisplayName}", _editingUser.DisplayName);
            Title = "Edit Profile";
            LoadEditingUser();
        }
        else
        {
            _logger.Information("CreateProfileWindow initialized in CREATE mode");
        }
        
        // Focus on first input
        Loaded += (s, e) => DisplayNameInput.Focus();
    }
    
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }
    
    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }

    private void LoadEditingUser()
    {
        if (_editingUser == null) return;
        
        DisplayNameInput.Text = _editingUser.DisplayName;
        SymbolInput.Text = _editingUser.PersonalSymbol;
        JournalFileInput.Text = _editingUser.JournalFileName;
        _selectedJournalPath = null; // Keep existing unless changed
        
        // Load existing avatar
        if (_editingUser.HasAvatar)
        {
            _selectedAvatarPath = _editingUser.AvatarPath;
            LoadAvatarPreview(_selectedAvatarPath);
        }
        
        if (!string.IsNullOrEmpty(_editingUser.PixelaUsername))
        {
            PixelaUsernameInput.Text = _editingUser.PixelaUsername;
            PixelaTokenInput.Password = _editingUser.PixelaToken ?? "";
            PixelaGraphIdInput.Text = _editingUser.PixelaGraphId ?? "";
            _pixelaUsernameAvailable = true;
            _isNewPixelaUser = false;
        }
    }

    private void AvatarBrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Avatar Image",
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif"
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedAvatarPath = dialog.FileName;
            _logger.Information("Avatar selected: {Path}", _selectedAvatarPath);
            LoadAvatarPreview(_selectedAvatarPath);
        }
    }

    private void LoadAvatarPreview(string imagePath)
    {
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath);
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            
            AvatarImageBrush.ImageSource = bitmap;
            AvatarPreview.Visibility = System.Windows.Visibility.Visible;
            AvatarPlaceholder.Visibility = System.Windows.Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load avatar preview");
        }
    }

    private string SaveAvatarToAppData(string sourcePath, string userName)
    {
        try
        {
            var avatarsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KeganOS", "avatars");
            
            Directory.CreateDirectory(avatarsDir);
            
            var extension = Path.GetExtension(sourcePath);
            var safeUserName = string.Join("_", userName.Split(Path.GetInvalidFileNameChars()));
            var destFileName = $"{safeUserName}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
            var destPath = Path.Combine(avatarsDir, destFileName);
            
            File.Copy(sourcePath, destPath, overwrite: true);
            _logger.Information("Avatar saved: {Path}", destPath);
            
            return destPath;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save avatar");
            return "";
        }
    }

    private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select KEGOMODORO Journal File",
            Filter = "Text Files (*.txt)|*.txt",
            InitialDirectory = GetKegomoDoroPath()
        };

        if (dialog.ShowDialog() == true)
        {
            // Only accept .txt files
            if (!dialog.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning("Invalid journal file format selected: {Path}", dialog.FileName);
                System.Windows.MessageBox.Show("Journal file must be a .txt file.", "Invalid Format",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            
            _selectedJournalPath = dialog.FileName;
            JournalFileInput.Text = Path.GetFileName(_selectedJournalPath);
            _logger.Information("Journal file selected: {Path}", _selectedJournalPath);
            ValidateJournalFile(_selectedJournalPath);
        }
    }

    private string GetKegomoDoroPath()
    {
        // Try to find KEGOMODORO texts folder
        var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro", "dependencies", "texts");
        if (Directory.Exists(basePath))
            return Path.GetFullPath(basePath);
        
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void ValidateJournalFile(string filePath)
    {
        _logger.Information("Validating journal file: {Path}", filePath);
        
        try
        {
            var content = File.ReadAllText(filePath);
            var lines = content.Split('\n');
            
            // Patterns for KEGOMODORO journal format
            var datePattern = new Regex(@"^\d{1,2}[/.]?\d{1,2}[/.]?\d{4}");
            var timePattern = new Regex(@"^\d{1,2}:\d{2}");
            var separatorPattern = new Regex(@"^[-*=_]{3,}");
            var headerPattern = new Regex(@"^##?\s");
            
            var uniqueDates = new HashSet<string>();
            int timeEntries = 0;
            int noteLines = 0;
            int emptyLines = 0;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (string.IsNullOrEmpty(trimmed))
                {
                    emptyLines++;
                    continue;
                }
                
                if (datePattern.IsMatch(trimmed))
                    uniqueDates.Add(trimmed); // Use HashSet for unique dates
                else if (timePattern.IsMatch(trimmed))
                    timeEntries++;
                else if (separatorPattern.IsMatch(trimmed) || headerPattern.IsMatch(trimmed))
                    continue; // Separators and headers are formatting
                else
                    noteLines++; // Notes, reflections, comments - all valid!
            }
            
            int dateEntries = uniqueDates.Count;
            _validEntriesCount = dateEntries;
            
            // Show status - always green if we found dates!
            JournalStatusPanel.Visibility = System.Windows.Visibility.Visible;
            
            if (dateEntries > 0)
            {
                JournalStatusPanel.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1A, 0x2A, 0x1A));
                JournalStatusPanel.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x2A, 0x4A, 0x2A));
                JournalStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x88, 0xCC, 0x88));
                
                // Friendly message showing all content is valid
                JournalStatusText.Text = $"✓ {dateEntries} days tracked, {timeEntries} time logs";
                if (noteLines > 0)
                {
                    JournalStatusText.Text += $"\n✓ {noteLines} notes & reflections";
                }
            }
            else
            {
                JournalStatusPanel.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x1A));
                JournalStatusPanel.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4A, 0x4A, 0x2A));
                JournalStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0x88));
                JournalStatusText.Text = noteLines > 0 
                    ? $"📝 Text file with {noteLines} lines (no dates found)"
                    : "📄 Empty or new file - ready to start!";
            }
            
            _logger.Information("Journal validation: {Dates} days, {Times} times, {Notes} notes",
                dateEntries, timeEntries, noteLines);
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to validate journal file");
            JournalStatusPanel.Visibility = System.Windows.Visibility.Visible;
            JournalStatusPanel.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x2A, 0x1A, 0x1A));
            JournalStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xCC, 0x88, 0x88));
            JournalStatusText.Text = $"✗ Error reading file: {ex.Message}";
        }
    }

    private void ShowPixelaStatus(string message, bool isSuccess)
    {
        PixelaStatusPanel.Visibility = System.Windows.Visibility.Visible;
        PixelaStatusPanel.Background = new System.Windows.Media.SolidColorBrush(
            isSuccess ? System.Windows.Media.Color.FromRgb(0x1A, 0x2A, 0x1A) : System.Windows.Media.Color.FromRgb(0x2A, 0x1A, 0x1A));
        PixelaStatusPanel.BorderBrush = new System.Windows.Media.SolidColorBrush(
            isSuccess ? System.Windows.Media.Color.FromRgb(0x2A, 0x4A, 0x2A) : System.Windows.Media.Color.FromRgb(0x4A, 0x2A, 0x2A));
        PixelaStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            isSuccess ? System.Windows.Media.Color.FromRgb(0x88, 0xCC, 0x88) : System.Windows.Media.Color.FromRgb(0xCC, 0x88, 0x88));
        PixelaStatusText.Text = message;
    }

    private async void CheckUsernameButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var username = PixelaUsernameInput.Text.Trim().ToLower();
        
        if (string.IsNullOrEmpty(username))
        {
            ShowPixelaStatus("Enter a username first", false);
            return;
        }

        // Validate format first
        var (isValid, formatError) = await _pixelaService.CheckUsernameAvailabilityAsync(username);
        if (!isValid)
        {
            ShowPixelaStatus($"✗ {formatError}", false);
            TokenInputPanel.Visibility = System.Windows.Visibility.Collapsed;
            _pixelaUsernameAvailable = false;
            return;
        }

        CheckUsernameButton.IsEnabled = false;
        CheckUsernameButton.Content = "[ ... ]";
        
        try
        {
            // Check if user exists on Pixe.la
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = System.TimeSpan.FromSeconds(10);
            
            var response = await httpClient.GetAsync($"https://pixe.la/@{username}");
            var userExists = response.IsSuccessStatusCode;
            
            if (userExists)
            {
                // Existing user - need their token
                _pixelaUsernameAvailable = true;
                _isNewPixelaUser = false;
                TokenInputPanel.Visibility = System.Windows.Visibility.Visible;
                ShowPixelaStatus($"✓ Username '{username}' exists. Enter your token below.", true);
                _logger.Information("Pixe.la user '{Username}' exists, requesting token", username);
            }
            else
            {
                // New user - will auto-register
                _pixelaUsernameAvailable = true;
                _isNewPixelaUser = true;
                TokenInputPanel.Visibility = System.Windows.Visibility.Collapsed;
                ShowPixelaStatus($"✓ Username '{username}' is available! Will auto-register.", true);
                _logger.Information("Pixe.la username '{Username}' is available", username);
            }
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to check Pixe.la username");
            ShowPixelaStatus($"✗ Could not check username: {ex.Message}", false);
            TokenInputPanel.Visibility = System.Windows.Visibility.Collapsed;
            _pixelaUsernameAvailable = false;
        }
        finally
        {
            CheckUsernameButton.IsEnabled = true;
            CheckUsernameButton.Content = "[ Check ]";
        }
    }

    private void CreatePixelaAccount_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://pixe.la/",
                UseShellExecute = true
            });
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to open Pixe.la website");
        }
    }

    private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void CreateButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var displayName = DisplayNameInput.Text.Trim();
        var symbol = SymbolInput.Text.Trim();
        var pixelaUsername = PixelaUsernameInput.Text.Trim().ToLower();

        // Validation
        if (string.IsNullOrEmpty(displayName))
        {
            System.Windows.MessageBox.Show("Please enter a display name.", "Validation", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            DisplayNameInput.Focus();
            return;
        }

        // Validate Pixe.la username if provided
        if (!string.IsNullOrEmpty(pixelaUsername))
        {
            // Must click Check first
            if (!_pixelaUsernameAvailable)
            {
                ShowPixelaStatus("✗ Please click [Check] to verify your username first", false);
                return;
            }
            
            // Existing user must provide token
            if (!_isNewPixelaUser && string.IsNullOrEmpty(PixelaTokenInput.Password.Trim()))
            {
                ShowPixelaStatus("✗ Please enter your Pixe.la token", false);
                PixelaTokenInput.Focus();
                return;
            }
        }

        // If no symbol provided, use 🦭 (seal emoji) as default
        if (string.IsNullOrEmpty(symbol))
        {
            symbol = "🦭";
        }

        // Determine journal file name
        string journalFile;
        if (!string.IsNullOrEmpty(_selectedJournalPath))
        {
            journalFile = Path.GetFileName(_selectedJournalPath);
        }
        else
        {
            // Auto-generate from display name
            journalFile = displayName.Replace(" ", "_").ToLower() + ".txt";
        }

        // Token: use provided token for existing users, or auto-generate for new users
        string? pixelaToken = null;
        string? graphId = PixelaGraphIdInput.Text.Trim();
        
        if (!string.IsNullOrEmpty(pixelaUsername))
        {
            if (_isNewPixelaUser)
            {
                // New user - auto-generate token
                pixelaToken = _pixelaService.GenerateToken(pixelaUsername);
                if (string.IsNullOrEmpty(graphId)) graphId = "focus";
            }
            else
            {
                // Existing user - use their provided token
                pixelaToken = PixelaTokenInput.Password.Trim();
                if (string.IsNullOrEmpty(graphId)) graphId = "graph1";
            }
        }

        // Create or update user object
        User user;
        if (_editingUser != null)
        {
            // Edit mode - update existing user
            user = _editingUser;
            user.DisplayName = displayName;
            user.PersonalSymbol = symbol;
            if (!string.IsNullOrEmpty(_selectedJournalPath))
            {
                user.JournalFileName = journalFile;
            }
            if (!string.IsNullOrEmpty(pixelaUsername))
            {
                user.PixelaUsername = pixelaUsername;
                user.PixelaToken = pixelaToken;
                user.PixelaGraphId = graphId;
            }
            // Handle avatar update
            if (!string.IsNullOrEmpty(_selectedAvatarPath) && _selectedAvatarPath != user.AvatarPath)
            {
                // Backup existing avatar if present and different
                if (!string.IsNullOrEmpty(_editingUser.AvatarPath) && File.Exists(_editingUser.AvatarPath) && _editingUser.AvatarPath != _selectedAvatarPath)
                {
                    await _backupService.BackupImageAsync(_editingUser, _editingUser.AvatarPath);
                    _logger.Information("Previous avatar backed up before replacement");
                }
                
                var avatarPath = SaveAvatarToAppData(_selectedAvatarPath, displayName);
                _editingUser.AvatarPath = avatarPath;
            }
        }
        else
        {
            // Create mode - new user
            user = new User
            {
                DisplayName = displayName,
                PersonalSymbol = symbol,
                JournalFileName = journalFile,
                PixelaUsername = string.IsNullOrEmpty(pixelaUsername) ? null : pixelaUsername,
                PixelaToken = pixelaToken,
                PixelaGraphId = graphId,
                AvatarPath = !string.IsNullOrEmpty(_selectedAvatarPath) 
                    ? SaveAvatarToAppData(_selectedAvatarPath, displayName) 
                    : ""
            };
        }

        try
        {
            if (_editingUser != null)
            {
                _logger.Information("Updating user: {Name} with symbol: {Symbol}", displayName, symbol);
                await _userService.UpdateUserAsync(user);
                CreatedUser = user;
            }
            else
            {
                _logger.Information("Creating user: {Name} with symbol: {Symbol}, journal: {Journal}", 
                    displayName, symbol, journalFile);

                // Register on Pixe.la if username provided (with retry loop for free version)
                if (!string.IsNullOrEmpty(pixelaUsername) && !string.IsNullOrEmpty(pixelaToken))
                {
                    _logger.Information("Registering Pixe.la user: {Username}", pixelaUsername);
                    ShowPixelaStatus("Connecting to Pixe.la (this may take a few seconds)...", true);
                    
                    var (success, error) = await _pixelaService.RegisterUserAsync(pixelaUsername, pixelaToken);
                    if (!success)
                    {
                        ShowPixelaStatus($"✗ Pixe.la: {error}", false);
                        return;
                    }

                    // Create default graph
                    ShowPixelaStatus($"Creating {graphId} graph...", true);
                    var graphCreated = await _pixelaService.CreateGraphAsync(user, graphId, "Focus Hours");
                    if (!graphCreated)
                    {
                        _logger.Warning("Graph creation failed, but continuing");
                    }
                    
                    ShowPixelaStatus("✓ Pixe.la connected!", true);
                }

                CreatedUser = await _userService.CreateUserAsync(user);
            }
            
            DialogResult = true;
            Close();
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, _editingUser != null ? "Failed to update user" : "Failed to create user");
            System.Windows.MessageBox.Show($"Failed to {(_editingUser != null ? "update" : "create")} profile: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
