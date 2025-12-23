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
    
    public User? CreatedUser { get; private set; }

    public CreateProfileWindow(IUserService userService, IPixelaService pixelaService)
    {
        InitializeComponent();
        _userService = userService;
        _pixelaService = pixelaService;
        _logger.Information("CreateProfileWindow initialized");
        
        // Focus on first input
        Loaded += (s, e) => DisplayNameInput.Focus();
    }

    private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select KEGOMODORO Journal File",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            InitialDirectory = GetKegomoDoroPath()
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedJournalPath = dialog.FileName;
            JournalFileInput.Text = Path.GetFileName(_selectedJournalPath);
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
            
            int dateEntries = 0;
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
                    dateEntries++;
                else if (timePattern.IsMatch(trimmed))
                    timeEntries++;
                else if (separatorPattern.IsMatch(trimmed) || headerPattern.IsMatch(trimmed))
                    continue; // Separators and headers are formatting
                else
                    noteLines++; // Notes, reflections, comments - all valid!
            }
            
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

        // If no symbol provided, use display name
        if (string.IsNullOrEmpty(symbol))
        {
            symbol = displayName;
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
        string? graphId = null;
        if (!string.IsNullOrEmpty(pixelaUsername))
        {
            if (_isNewPixelaUser)
            {
                // New user - auto-generate token
                pixelaToken = _pixelaService.GenerateToken(pixelaUsername);
                graphId = "focus";
            }
            else
            {
                // Existing user - use their provided token
                pixelaToken = PixelaTokenInput.Password.Trim();
                graphId = "graph1"; // Default graph ID used by KEGOMODORO
            }
        }

        // Create user object
        var user = new User
        {
            DisplayName = displayName,
            PersonalSymbol = symbol,
            JournalFileName = journalFile,
            PixelaUsername = string.IsNullOrEmpty(pixelaUsername) ? null : pixelaUsername,
            PixelaToken = pixelaToken,
            PixelaGraphId = graphId
        };

        try
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
                ShowPixelaStatus("Creating focus graph...", true);
                var graphCreated = await _pixelaService.CreateGraphAsync(user, "focus", "Focus Hours");
                if (!graphCreated)
                {
                    _logger.Warning("Graph creation failed, but continuing");
                }
                
                ShowPixelaStatus("✓ Pixe.la connected!", true);
            }

            CreatedUser = await _userService.CreateUserAsync(user);
            DialogResult = true;
            Close();
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to create user");
            System.Windows.MessageBox.Show($"Failed to create profile: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
