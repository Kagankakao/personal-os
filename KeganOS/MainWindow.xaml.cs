using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Linq;

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
                
                _logger.Information("Stats loaded: Total={Total}h, Week={Week}h, Today={Today}h", 
                    (int)totalTime.TotalHours, (int)weekTime.TotalHours, (int)todayTime.TotalHours);
            }
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to load user data");
        }
    }

    private async void StartFocusButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _logger.Information("Starting KEGOMODORO...");
        
        // Show launching feedback
        StartFocusButton.IsEnabled = false;
        
        _kegomoDoroService.Launch();
        
        // Wait a moment for process to start
        await System.Threading.Tasks.Task.Delay(500);
        
        StartFocusButton.IsEnabled = true;
        
        if (_kegomoDoroService.IsRunning)
        {
            // Success - don't show message, just let it run
            _logger.Information("KEGOMODORO is running");
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
}