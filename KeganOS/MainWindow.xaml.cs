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
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to show achievement toast");
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

    private void SettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_currentUser == null)
        {
            _logger.Warning("Cannot open settings: no user logged in");
            return;
        }

        _logger.Information("Opening settings for user {DisplayName}", _currentUser.DisplayName);
        
        try
        {
            var settingsWindow = new Views.SettingsWindow(_userService, _aiProvider)
            {
                Owner = this
            };
            settingsWindow.SetCurrentUser(_currentUser);
            
            if (settingsWindow.ShowDialog() == true)
            {
                // Refresh UI with updated settings
                PersonalSymbol.Text = string.IsNullOrEmpty(_currentUser.PersonalSymbol) ? "🦭" : _currentUser.PersonalSymbol;
                UserDisplayName.Text = _currentUser.DisplayName;
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

    private async void AskAIButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var question = AIChatInput.Text;
        if (string.IsNullOrWhiteSpace(question) || question.Contains("Ask me anything"))
        {
            return;
        }

        _logger.Information("AI question: {Question}", question);

        // Check if AI is configured
        if (!_aiProvider.IsAvailable)
        {
            System.Windows.MessageBox.Show("AI is not configured.\n\nPlease add your Gemini API key in profile settings to use AI features.",
                "AI Not Configured", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        // Show loading state
        var originalText = AIChatInput.Text;
        AIChatInput.Text = "Thinking...";
        AIChatInput.IsEnabled = false;

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
            System.Windows.MessageBox.Show(response, "AI Response", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get AI response");
            System.Windows.MessageBox.Show($"Failed to get AI response: {ex.Message}", 
                "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            AIChatInput.Text = "Ask me anything about your journey...";
            AIChatInput.IsEnabled = true;
        }
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