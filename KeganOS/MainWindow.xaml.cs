using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace KeganOS;

/// <summary>
/// Main dashboard window
/// </summary>
public partial class MainWindow : System.Windows.Window
{
    private readonly ILogger _logger = Log.ForContext<MainWindow>();
    private readonly IKegomoDoroService _kegomoDoroService;
    private readonly IJournalService _journalService;
    private readonly IPixelaService _pixelaService;
    private User? _currentUser;

    public MainWindow(IKegomoDoroService kegomoDoroService, IJournalService journalService, IPixelaService pixelaService)
    {
        InitializeComponent();
        _kegomoDoroService = kegomoDoroService;
        _journalService = journalService;
        _pixelaService = pixelaService;
        
        _logger.Information("MainWindow initialized with services");
        
        // Load KEGOMODORO images
        LoadKegomoDoroImages();
    }

    /// <summary>
    /// Load images from KEGOMODORO dependencies folder
    /// </summary>
    private void LoadKegomoDoroImages()
    {
        try
        {
            var possiblePaths = new[]
            {
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "kegomodoro", "dependencies", "images")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro", "dependencies", "images")),
                @"C:\Users\ariba\OneDrive\Documenti\Software Projects\AI Projects\personal-os\personal-os\kegomodoro\dependencies\images"
            };

            string? imagesPath = null;
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    imagesPath = path;
                    break;
                }
            }

            if (imagesPath != null)
            {
                // Load fire image with cache disabled for live updates
                var fireImagePath = Path.Combine(imagesPath, "main_image.png");
                if (File.Exists(fireImagePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.UriSource = new Uri(fireImagePath);
                    bitmap.EndInit();
                    FireImage.Source = bitmap;
                    _logger.Debug("Loaded fire image from {Path}", fireImagePath);
                }
            }
            else
            {
                _logger.Warning("KEGOMODORO images folder not found");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load KEGOMODORO images");
        }
    }

    #region Title Bar Controls
    
    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                // Double-click to maximize/restore
                MaximizeButton_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }
    }

    private void MinimizeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        WindowState = System.Windows.WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        WindowState = WindowState == System.Windows.WindowState.Maximized 
            ? System.Windows.WindowState.Normal 
            : System.Windows.WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
    
    #endregion

    /// <summary>
    /// Set the current logged-in user and update UI
    /// </summary>
    public void SetCurrentUser(User user)
    {
        _currentUser = user;
        _logger.Information("User set: {Name}", user.DisplayName);
        
        // Update UI with user info
        PersonalSymbol.Text = string.IsNullOrEmpty(user.PersonalSymbol) ? user.DisplayName : user.PersonalSymbol;
        UserDisplayName.Text = user.DisplayName;
        
        // Load user-specific data
        LoadUserDataAsync();
    }

    private async void LoadUserDataAsync()
    {
        if (_currentUser == null) return;
        
        _logger.Debug("Loading user data for {User}", _currentUser.DisplayName);
        
        try
        {
            // Load journal entries and calculate stats
            var entries = await _journalService.ReadEntriesAsync(_currentUser);
            var entryList = entries.ToList();
            
            if (entryList.Count > 0)
            {
                // Calculate total hours from TimeSpan
                TimeSpan totalTime = TimeSpan.Zero;
                foreach (var entry in entryList)
                {
                    if (entry.TimeWorked.HasValue)
                    {
                        totalTime += entry.TimeWorked.Value;
                    }
                }
                TotalHours.Text = $"{(int)totalTime.TotalHours} hrs";
                
                // Calculate week hours (last 7 days)
                var weekAgo = DateTime.Now.AddDays(-7);
                var weekEntries = entryList.Where(e => e.Date >= weekAgo).ToList();
                TimeSpan weekTime = TimeSpan.Zero;
                foreach (var entry in weekEntries)
                {
                    if (entry.TimeWorked.HasValue)
                    {
                        weekTime += entry.TimeWorked.Value;
                    }
                }
                WeekHours.Text = $"{(int)weekTime.TotalHours} hrs";
                
                // Today
                var todayEntries = entryList.Where(e => e.Date.Date == DateTime.Today).ToList();
                TimeSpan todayTime = TimeSpan.Zero;
                foreach (var entry in todayEntries)
                {
                    if (entry.TimeWorked.HasValue)
                    {
                        todayTime += entry.TimeWorked.Value;
                    }
                }
                TodayHours.Text = $"{(int)todayTime.TotalHours} hrs";
                
                _logger.Information("Stats loaded from journal: Total={Total}h, Week={Week}h, Today={Today}h", 
                    (int)totalTime.TotalHours, (int)weekTime.TotalHours, (int)todayTime.TotalHours);
            }
            else
            {
                // Fallback to Pixe.la stats if journal is empty
                _logger.Information("Journal is empty, attempting to load stats from Pixe.la");
                
                // 1. Get aggregate stats for the true total
                var pixelaStats = await _pixelaService.GetStatsAsync(_currentUser);
                if (pixelaStats != null)
                {
                    TotalHours.Text = $"{(int)pixelaStats.TotalQuantity} hrs";
                    _logger.Information("TotalHours from Pixe.la stats: {Total}", pixelaStats.TotalQuantity);
                }

                // 2. Get pixels for last 365 days to calculate Today and Week stats
                // Fetching a larger window to ensure the dashboard looks "alive" even for occasional users
                var toDate = DateTime.Today;
                var fromDate = toDate.AddDays(-365);
                var pixels = await _pixelaService.GetPixelsAsync(_currentUser, fromDate, toDate);
                var pixelList = pixels.ToList();

                double weekQty = 0;
                double todayQty = 0;
                
                var weekAgo = DateTime.Today.AddDays(-7);

                foreach (var p in pixelList)
                {
                    if (p.Date >= weekAgo) weekQty += p.Quantity;
                    if (p.Date.Date == DateTime.Today) todayQty += p.Quantity;
                }

                WeekHours.Text = $"{(int)weekQty} hrs";
                TodayHours.Text = $"{(int)todayQty} hrs";
                
                _logger.Information("Pixe.la stats (bulk 365d): Week={Week}h, Today={Today}h", 
                    (int)weekQty, (int)todayQty);
            }
            
            // Load Pixe.la heatmap if configured
            await LoadPixelaHeatmapAsync();
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to load user data");
        }
    }

    private async System.Threading.Tasks.Task LoadPixelaHeatmapAsync()
    {
        if (_currentUser == null || !_pixelaService.IsConfigured(_currentUser))
        {
            PixelaStatus.Text = "Pixe.la not configured";
            return;
        }

        try
        {
            _logger.Information("Loading Pixe.la heatmap for {User}", _currentUser.PixelaUsername);
            PixelaStatus.Text = "Loading...";
            
            // 1. Find the latest NON-ZERO activity date to ensure we show "greens" 
            // instead of just today's (empty) dashboard view.
            var latestDate = await _pixelaService.GetLatestActiveDateAsync(_currentUser);
            
            // 2. Fetch the SVG with that date as the anchor and DARK appearance
            var svg = await _pixelaService.GetSvgAsync(_currentUser, latestDate, "dark");
            
            if (string.IsNullOrEmpty(svg))
            {
                PixelaStatus.Text = "Could not fetch heatmap";
                return;
            }

            // Cleanup SVG string: remove XML declaration and DOCTYPE
            svg = Regex.Replace(svg, @"<\?xml.*?\?>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            svg = Regex.Replace(svg, @"<!DOCTYPE.*?>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var graphUrl = $"https://pixe.la/v1/users/{_currentUser.PixelaUsername}/graphs/{_currentUser.PixelaGraphId}.html";
            
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ 
            margin: 0; 
            padding: 0; 
            background: #0A0A0A; 
            overflow: hidden; 
            display: flex; 
            justify-content: center; 
            align-items: center; 
            height: 100vh;
            cursor: pointer;
        }}
        svg {{ 
            width: 100% !important; 
            height: 100% !important;
            display: block;
        }}
        svg:hover {{  
            opacity: 0.85;
        }}
        /* Block all internal SVG links */
        svg a {{
            pointer-events: none;
        }}
    </style>
</head>
<body>
    {svg}
    <script>
        document.body.addEventListener('click', function(e) {{
            e.preventDefault();
            e.stopPropagation();
            window.open('{graphUrl}', '_blank');
        }});
    </script>
</body>
</html>";
            
            // Ensure WebView2 is initialized before navigating
            await PixelaHeatmapWebView.EnsureCoreWebView2Async();
            PixelaHeatmapWebView.NavigateToString(html);
            
            PixelaStatus.Text = $"📊 {_currentUser.PixelaUsername}/{_currentUser.PixelaGraphId}";
            
            _logger.Information("Pixe.la heatmap (anchor: {Date}) rendered with WebView2", latestDate ?? "today");
        }
        catch (System.Exception ex)
        {
            _logger.Warning(ex, "Failed to load Pixe.la heatmap");
            PixelaStatus.Text = "Could not load heatmap";
        }
    }

    private async void StartFocusButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _logger.Information("Starting KEGOMODORO...");
        
        // Check if already running FIRST before doing anything
        if (_kegomoDoroService.IsAnyInstanceRunning)
        {
            System.Windows.MessageBox.Show("KEGOMODORO is already running!\n\nCheck your taskbar for the timer window.", 
                "Already Running", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }
        
        // Show launching feedback
        var originalContent = StartFocusButton.Content;
        StartFocusButton.Content = "⏳ Launching...";
        StartFocusButton.IsEnabled = false;
        
        _kegomoDoroService.Launch();
        
        // Wait a moment for process to start
        await System.Threading.Tasks.Task.Delay(1000);
        
        StartFocusButton.Content = originalContent;
        StartFocusButton.IsEnabled = true;
        
        if (_kegomoDoroService.IsRunning)
        {
            // Success - don't show message, just let it run
            _logger.Information("KEGOMODORO is running");
        }
        else if (_kegomoDoroService.LastError == "KEGOMODORO is already running")
        {
            // Already running - show friendly message
            System.Windows.MessageBox.Show("KEGOMODORO is already running!\n\nCheck your taskbar for the timer window.", 
                "Already Running", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        else if (!string.IsNullOrEmpty(_kegomoDoroService.LastError))
        {
            // Show specific error
            System.Windows.MessageBox.Show($"Failed to launch KEGOMODORO:\n\n{_kegomoDoroService.LastError}", 
                "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        else
        {
            // Process started but crashed - likely Python dependency issue
            System.Windows.MessageBox.Show("KEGOMODORO started but closed immediately.\n\nCheck if KEGOMODORO runs directly from terminal:\ncd kegomodoro && python main.py", 
                "Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private void OpenNotepadButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_currentUser == null) return;
        
        _logger.Information("Opening journal in Notepad...");
        _journalService.OpenInNotepad(_currentUser);
    }

    private async void SaveJournalButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_currentUser == null) return;
        
        var entry = JournalInput.Text;
        if (string.IsNullOrWhiteSpace(entry))
        {
            return;
        }

        _logger.Information("Saving journal entry: {Preview}...", 
            entry.Length > 30 ? entry[..30] : entry);
        
        await _journalService.AppendEntryAsync(_currentUser, entry);
        JournalInput.Text = "";
        
        // Reload stats
        LoadUserDataAsync();
    }

    private void AskAIButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var question = AIChatInput.Text;
        if (string.IsNullOrWhiteSpace(question) || question.Contains("Ask me anything"))
        {
            return;
        }

        _logger.Information("AI question: {Question}", question);
        
        // TODO: Query via IAIProvider
        System.Windows.MessageBox.Show("AI features coming in Phase 5!",
            "AI Chat", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        
        AIChatInput.Text = "Ask me anything about your journey...";
    }

    private void CustomizeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _logger.Information("Opening KEGOMODORO settings...");
        
        var settingsWindow = new Views.KegomoDoroSettingsWindow();
        settingsWindow.Owner = this;
        
        // Set callback to reload fire image when changed
        settingsWindow.OnImageChanged = () =>
        {
            _logger.Information("Fire image changed - reloading...");
            LoadKegomoDoroImages();
        };
        
        settingsWindow.ShowDialog();
        
        if (settingsWindow.SettingsChanged)
        {
            _logger.Information("KEGOMODORO settings changed - user should restart timer");
        }
        
        // Also reload if image was changed (in case callback didn't fire)
        if (settingsWindow.ImageChanged)
        {
            LoadKegomoDoroImages();
        }
    }
}