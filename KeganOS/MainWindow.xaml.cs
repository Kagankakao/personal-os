using KeganOS.Core.Models;
using Serilog;

namespace KeganOS;

/// <summary>
/// Main dashboard window
/// </summary>
public partial class MainWindow : System.Windows.Window
{
    private readonly ILogger _logger = Log.ForContext<MainWindow>();
    private User? _currentUser;

    public MainWindow()
    {
        InitializeComponent();
        _logger.Information("MainWindow initialized");
    }

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
        LoadUserData();
    }

    private void LoadUserData()
    {
        if (_currentUser == null) return;
        
        _logger.Debug("Loading user data for {User}", _currentUser.DisplayName);
        // TODO: Load journal entries, Pixe.la stats, etc.
    }

    private void StartFocusButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _logger.Information("Starting KEGOMODORO...");
        // TODO: Launch KEGOMODORO via IKegomoDoroService
        System.Windows.MessageBox.Show("KEGOMODORO will launch here!", "KEGOMODORO");
    }

    private void OpenNotepadButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _logger.Information("Opening journal in Notepad...");
        // TODO: Open via IJournalService
    }

    private void SaveJournalButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var entry = JournalInput.Text;
        if (string.IsNullOrWhiteSpace(entry))
        {
            return;
        }

        _logger.Information("Saving journal entry: {Preview}...", 
            entry.Length > 30 ? entry[..30] : entry);
        
        // TODO: Save via IJournalService
        JournalInput.Text = "";
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
        AIChatInput.Text = "Ask me anything about your journey...";
    }
}