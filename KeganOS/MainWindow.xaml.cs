using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using KeganOS.Infrastructure.Services;
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
    private readonly IAIProvider _aiProvider;
    private readonly IMotivationalMessageService _motivationalService;

    private readonly IUserService _userService;
    private readonly IBackupService _backupService;
    private readonly IAchievementService _achievementService;
    private readonly IAnalyticsService _analyticsService;
    private User? _currentUser;
    private List<ChatMessage> _chatHistory = [];
    
    // Ticker animation
    private List<string> _tickerQuotes = [];
    private int _tickerIndex = 0;
    private System.Windows.Media.Animation.Storyboard? _tickerStoryboard;

    public MainWindow(
        IKegomoDoroService kegomoDoroService, 
        IJournalService journalService, 
        IPixelaService pixelaService,

        IAIProvider aiProvider,
        IMotivationalMessageService motivationalService,
        IUserService userService,
        IBackupService backupService,
        IAchievementService achievementService,
        IAnalyticsService analyticsService)
    {
        InitializeComponent();
        _kegomoDoroService = kegomoDoroService;
        _journalService = journalService;
        _pixelaService = pixelaService;
        _aiProvider = aiProvider;
        _motivationalService = motivationalService;
        _userService = userService;
        _backupService = backupService;
        _achievementService = achievementService;
        _analyticsService = analyticsService;
        
        // Subscribe to achievements
        _achievementService.OnAchievementUnlocked += OnAchievementUnlocked;
        
        _logger.Information("MainWindow initialized with all services including AI");
        
        // Load KEGOMODORO images
        LoadKegomoDoroImages();
    }

    private void OnAchievementUnlocked(object? sender, Achievement achievement)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                // List of achievement IDs that deserve a "Big Win" celebration
                string[] bigWins = { "lvl10", "lvl20", "lvl50", "lvl100", "hours100", "hours500", "hours1000" };

                if (bigWins.Contains(achievement.Id))
                {
                    MajorCelebrationOverlay.Show(achievement.Name, achievement.Icon, achievement.XpReward);
                    _logger.Information("Displayed big celebration for achievement: {Name}", achievement.Name);
                }
                else
                {
                    var toast = new Views.Components.ToastNotification();
                    toast.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    toast.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                    toast.Margin = new System.Windows.Thickness(0, 0, 20, 50);
                    System.Windows.Controls.Grid.SetRow(toast, 1);
                    
                    // Wire up click to open achievements
                    toast.OnToastClicked += (s, e) =>
                    {
                        var achievementsWindow = new Views.AchievementsWindow(_currentUser, _achievementService);
                        achievementsWindow.Owner = this;
                        achievementsWindow.ShowDialog();
                    };
                    
                    RootGrid.Children.Add(toast);
                    toast.Show(achievement.Name, achievement.Icon, achievement.XpReward);
                    
                    _logger.Information("Displayed toast for achievement: {Name}", achievement.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to show achievement notification");
            }
        });
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

    private void RootGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Clear focus from journal when clicking empty dashboard space
        // This makes Ask Prometheus button visible again
        RootGrid.Focus();
    }

    private async void SettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_currentUser == null)
        {
            _logger.Warning("Cannot open settings: no user logged in");
            return;
        }

        _logger.Information("Opening settings for user {DisplayName}", _currentUser.DisplayName);
        
        try
        {
            var settingsWindow = new Views.SettingsWindow(_userService, _aiProvider, _pixelaService, _backupService)
            {
                Owner = this
            };
            settingsWindow.SetCurrentUser(_currentUser);
            
            if (settingsWindow.ShowDialog() == true)
            {
                // IMPORTANT: Reload user from DB to get updated settings (especially Pixe.la credentials)
                var refreshedUser = await _userService.GetUserByIdAsync(_currentUser.Id);
                if (refreshedUser != null)
                {
                    _currentUser = refreshedUser;
                    _logger.Information("User data refreshed from database after settings save");
                }
                
                // Refresh UI with updated settings
                PersonalSymbol.Text = string.IsNullOrEmpty(_currentUser.PersonalSymbol) ? "🦭" : _currentUser.PersonalSymbol;
                UserDisplayName.Text = _currentUser.DisplayName;
                
                // Reload heatmap with new credentials
                _ = LoadPixelaHeatmapAsync();
                
                _logger.Information("Settings saved, UI refreshed");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open settings window");
            System.Windows.MessageBox.Show($"Failed to open settings: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void AddTimeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_currentUser == null)
        {
            _logger.Warning("Cannot add time: no user logged in");
            return;
        }

        _logger.Information("Opening Add Manual Time dialog");
        
        try
        {
            var addTimeWindow = new Views.AddManualTimeWindow(_pixelaService, _achievementService, _currentUser)
            {
                Owner = this
            };
            
            if (addTimeWindow.ShowDialog() == true)
            {
                // Refresh user data after adding time
                LoadUserDataAsync();
                _logger.Information("Manual time added, refreshing data");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open Add Time dialog");
            System.Windows.MessageBox.Show($"Failed to open Add Time: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void Stats_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_currentUser == null) return;
        
        var reportWindow = new Views.WeeklyReportWindow(_currentUser, _analyticsService);
        reportWindow.Owner = this;
        reportWindow.ShowDialog();
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
        PersonalSymbol.Text = string.IsNullOrEmpty(user.PersonalSymbol) ? "🦭" : user.PersonalSymbol;
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
            
            // Load ticker quotes
            await LoadTickerQuotesAsync();
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to load user data");
        }
    }

    private async System.Threading.Tasks.Task LoadTickerQuotesAsync()
    {
        if (_currentUser == null) return;

        try
        {
            _logger.Debug("Loading ticker quotes from journal...");
            
            // Configure AI provider with user's API key if available
            if (!string.IsNullOrEmpty(_currentUser.GeminiApiKey) && _aiProvider is GeminiProvider gemini)
            {
                gemini.Configure(_currentUser.GeminiApiKey);
            }

            // Load quotes from journal service
            _tickerQuotes = await ExtractJournalQuotesAsync();
            
            if (_tickerQuotes.Count == 0)
            {
                // Fallback to default quotes
                _tickerQuotes =
                [
                    "💪 Discipline is freedom in its purest form.",
                    "🎯 Focus on progress, not perfection.",
                    "⏰ Every minute counts. Make them matter.",
                    "🌟 Small steps lead to big achievements.",
                    "🔥 Consistency beats intensity."
                ];
            }
            
            _tickerIndex = 0;
            _logger.Information("Loaded {Count} ticker quotes", _tickerQuotes.Count);
            
            StartTickerAnimation();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load ticker quotes");
            TickerText.Text = "📜 \"Every expert was once a beginner.\"";
        }
    }

    private async System.Threading.Tasks.Task<List<string>> ExtractJournalQuotesAsync()
    {
        var quotes = new List<string>();
        
        try
        {
            var entries = await _journalService.ReadEntriesAsync(_currentUser!);
            var recentEntries = entries.OrderByDescending(e => e.Date).Take(30);
            
            foreach (var entry in recentEntries)
            {
                if (string.IsNullOrEmpty(entry.NoteText)) continue;
                
                // Split into sentences/lines
                var lines = entry.NoteText.Split(['\n', '.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    
                    // Filter: 20-100 chars, starts with letter, no URLs/dates
                    if (trimmed.Length >= 20 && trimmed.Length <= 100 &&
                        char.IsLetter(trimmed[0]) &&
                        !trimmed.Contains("http") &&
                        !Regex.IsMatch(trimmed, @"^\d{1,2}[/.-]\d{1,2}"))
                    {
                        quotes.Add($"📝 \"{trimmed}\"");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error extracting journal quotes");
        }
        
        return quotes.Distinct().Take(20).ToList();
    }

    private void StartTickerAnimation()
    {
        if (_tickerQuotes.Count == 0) return;
        
        // Set current quote
        TickerText.Text = _tickerQuotes[_tickerIndex];
        _logger.Debug("Ticker displaying quote #{Index}", _tickerIndex + 1);
        
        // Measure text width
        TickerText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var textWidth = TickerText.DesiredSize.Width;
        var canvasWidth = TickerCanvas.ActualWidth > 0 ? TickerCanvas.ActualWidth : 500;
        
        // Create animation: start from right edge, move to left beyond visible area
        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = canvasWidth,
            To = -textWidth - 50,
            Duration = System.TimeSpan.FromSeconds(10)
        };
        
        animation.Completed += (s, e) =>
        {
            // Move to next quote
            _tickerIndex = (_tickerIndex + 1) % _tickerQuotes.Count;
            StartTickerAnimation();
        };
        
        _tickerStoryboard?.Stop();
        _tickerStoryboard = new System.Windows.Media.Animation.Storyboard();
        _tickerStoryboard.Children.Add(animation);
        System.Windows.Media.Animation.Storyboard.SetTarget(animation, TickerText);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(animation, 
            new System.Windows.PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
        
        _tickerStoryboard.Begin();
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
        /* Enable hover on pixels */
        rect.day {{
            transition: all 0.2s ease-in-out;
            transform-origin: center;
        }}
        rect.day:hover {{
            stroke: #FFD700 !important;
            stroke-width: 2px !important;
            cursor: pointer;
            filter: brightness(1.5);
            transform: scale(1.3);
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
        // Use a flag to prevent click bubbling when a pixel is clicked
        let pixelClicked = false;

        document.querySelectorAll('rect.day').forEach(r => {{
            r.addEventListener('click', function(e) {{
                pixelClicked = true;
                e.preventDefault();
                e.stopPropagation();
                
                const titleElement = r.querySelector('title');
                if (titleElement) {{
                    const title = titleElement.textContent;
                    // Format: 2023-10-24 : 5.00 hours
                    const match = title.match(/(\d{{4}}-\d{{2}}-\d{{2}})/);
                    if (match) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'pixel_click', 
                            date: match[1],
                            text: title
                        }});
                    }}
                }}
            }});
        }});

        document.body.addEventListener('click', function(e) {{
            if (!pixelClicked) {{
                window.open('{graphUrl}', '_blank');
            }}
            pixelClicked = false;
        }});
    </script>
</body>
</html>";
            
            // Ensure WebView2 is initialized before navigating
            await PixelaHeatmapWebView.EnsureCoreWebView2Async();
            
            // Unsubscribe and subscribe to prevent duplicate handlers
            PixelaHeatmapWebView.WebMessageReceived -= PixelaHeatmapWebView_WebMessageReceived;
            PixelaHeatmapWebView.WebMessageReceived += PixelaHeatmapWebView_WebMessageReceived;
            
            PixelaHeatmapWebView.NavigateToString(html);
            
            PixelaStatus.Text = $"📊 {_currentUser.PixelaUsername}/{_currentUser.PixelaGraphId} • Click pixel to edit";
            
            _logger.Information("Pixe.la heatmap (anchor: {Date}) rendered with WebView2", latestDate ?? "today");
        }
        catch (System.Exception ex)
        {
            _logger.Warning(ex, "Failed to load Pixe.la heatmap");
            PixelaStatus.Text = "Could not load heatmap";
        }
    }

    private void PixelaHeatmapWebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.GetProperty("type").GetString() == "pixel_click")
            {
                var dateStr = root.GetProperty("date").GetString();
                var text = root.GetProperty("text").GetString();
                
                _logger.Information("Pixel clicked: {Date} - {Text}", dateStr, text);

                if (_currentUser != null)
                {
                    var editWindow = new Views.PixelEditWindow(_pixelaService, _userService, _currentUser)
                    {
                        Owner = this
                    };

                    if (editWindow.ShowDialog() == true)
                    {
                        // Refresh heatmap to show changes
                        _ = LoadPixelaHeatmapAsync();
                        _logger.Information("Heatmap refreshed after pixel update");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling WebView2 message");
        }
    }

    private async void PaletteColor_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && _currentUser != null)
        {
            var color = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(color)) return;

            _logger.Information("Changing Pixe.la theme to: {Color}", color);
            var (success, error) = await _pixelaService.UpdateGraphAsync(_currentUser, color: color);
            
            if (success)
            {
                _ = LoadPixelaHeatmapAsync(); // Refresh
            }
            else
            {
                System.Windows.MessageBox.Show(error ?? "Unknown error", "Failed to Update Theme");
            }
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

    private void AchievementsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_currentUser == null) return;
        
        var window = new Views.AchievementsWindow(_currentUser, _achievementService);
        window.Owner = this;
        window.ShowDialog();
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

    private async void AskPrometheusButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _logger.Information("Ask Prometheus button clicked");

        // Check if AI is configured
        if (!_aiProvider.IsAvailable)
        {
            System.Windows.MessageBox.Show("Prometheus is not configured.\n\nPlease add your Gemini API key in settings to awaken Prometheus.",
                "Prometheus Not Configured", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        // Simple input dialog for question
        var inputDialog = new Views.TextInputDialog("Ask Prometheus", "What would you like to know about your journey?");
        if (inputDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(inputDialog.ResponseText))
        {
            return;
        }

        var question = inputDialog.ResponseText;
        _logger.Information("Prometheus question: {Question}", question);

        try
        {
            // Build context from user's journey
            var context = "";
            if (_currentUser != null)
            {
                var entries = await _journalService.ReadEntriesAsync(_currentUser);
                var recentEntries = entries.OrderByDescending(e => e.Date).Take(5);
                if (recentEntries.Any())
                {
                    context = $"Recent journal entries from {_currentUser.DisplayName}'s journey:\n" +
                              string.Join("\n", recentEntries.Select(e => $"- {e.Date:MMM d}: {(e.NoteText.Length > 40 ? e.NoteText[..40] + "..." : e.NoteText)}"));
                }
            }

            var prompt = string.IsNullOrEmpty(context) 
                ? question 
                : $"Context about the user's personal journey:\n{context}\n\nUser's question: {question}";

            // Add to chat history
            _chatHistory.Add(new ChatMessage("user", question));

            // Get AI response
            var response = await _aiProvider.GenerateResponseAsync(prompt, _chatHistory.TakeLast(6));
            
            // Add response to history
            _chatHistory.Add(new ChatMessage("assistant", response));

            // Show response
            System.Windows.MessageBox.Show(response, "🔥 Prometheus", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get Prometheus response");
            System.Windows.MessageBox.Show($"Failed to consult Prometheus: {ex.Message}", 
                "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private void JournalInput_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        // Fade out Ask Prometheus button when typing in journal
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.2, TimeSpan.FromMilliseconds(200));
        AskPrometheusButton.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void JournalInput_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        // Fade in Ask Prometheus button when not typing
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(200));
        AskPrometheusButton.BeginAnimation(OpacityProperty, fadeIn);
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