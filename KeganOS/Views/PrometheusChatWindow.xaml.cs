using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KeganOS.Core.Interfaces;
using Serilog;

namespace KeganOS.Views;

public partial class PrometheusChatWindow : Window
{
    private readonly ILogger _logger = Log.ForContext<PrometheusChatWindow>();
    private readonly IPrometheusService _prometheusService;
    private readonly int? _userId;
    private bool _isProcessing;

    public event Action<string> NotesSearchRequested;
    
    // Conversation history for context (last 5 exchanges)
    private readonly List<(string Role, string Message)> _conversationHistory = new();
    private const int MaxHistorySize = 5;

    // Terminal colors
    private static readonly SolidColorBrush TerminalGreen = new(System.Windows.Media.Color.FromRgb(0, 255, 65));
    private static readonly SolidColorBrush TerminalCyan = new(System.Windows.Media.Color.FromRgb(0, 217, 255));
    private static readonly SolidColorBrush TerminalYellow = new(System.Windows.Media.Color.FromRgb(255, 215, 0));
    private static readonly SolidColorBrush TerminalWhite = new(System.Windows.Media.Color.FromRgb(204, 204, 204));
    private static readonly SolidColorBrush TerminalDim = new(System.Windows.Media.Color.FromRgb(102, 102, 102));

    public PrometheusChatWindow(IPrometheusService prometheusService, int? userId = null)
    {
        InitializeComponent();
        _prometheusService = prometheusService;
        _userId = userId;
        
        // Welcome message
        AddSystemMessage("Prometheus Terminal v1.0");
        AddSystemMessage("Type your message and press Enter to chat.");
        AddSystemMessage("─────────────────────────────────────────");
        AddAIMessage("Hey! I'm Prometheus. I remember everything about our conversations and your journey. What's on your mind?");
        
        InputBox.Focus();
    }

    // Close button event handlers
    private void CloseBtn_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Close();
    }

    private void CloseBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseBtn.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 120, 120));
    }

    private void CloseBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseBtn.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 95, 86));
    }

    // Header drag handler
    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    // Placeholder visibility
    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(InputBox.Text) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    private async void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && !_isProcessing)
        {
            await SendMessage();
            e.Handled = true;
        }
    }

    private async Task SendMessage()
    {
        var message = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(message) || _isProcessing)
            return;

        InputBox.Text = "";
        
        // Handle terminal commands
        if (message.StartsWith("/"))
        {
            HandleCommand(message.ToLower());
            return;
        }
        
        _isProcessing = true;
        UpdateStatus("typing...", false);

        // Add user message to history
        AddToHistory("user", message);
        
        // Add user message to UI
        AddUserMessage(message);

        // Create AI message block for streaming
        var (aiPanel, aiContent, thinkingIndicator) = CreateStreamingAIMessage();
        var fullResponse = new System.Text.StringBuilder();
        bool firstChunk = true;
        
        // Start animated thinking dots
        var animationCts = new System.Threading.CancellationTokenSource();
        _ = AnimateThinkingDotsAsync(thinkingIndicator, animationCts.Token);

        try
        {
            // Stream response from Prometheus with typewriter effect
            await foreach (var chunk in _prometheusService.ConsultStreamingAsync(message, _userId, _conversationHistory))
            {
                // Stop animation and hide thinking indicator on first chunk
                if (firstChunk)
                {
                    animationCts.Cancel();
                    thinkingIndicator.Text = "";
                    firstChunk = false;
                }
                
                fullResponse.Append(chunk);
                
                // Typewriter effect: add characters one by one with small delay
                foreach (char c in chunk)
                {
                    aiContent.Text += c;
                    
                    // Small delay for smooth typing effect
                    if (c == ' ' || c == '.' || c == '!' || c == '?' || c == '\n')
                    {
                        await Task.Delay(20); // Pause at word boundaries
                        ChatScrollViewer.ScrollToEnd();
                    }
                    else if (aiContent.Text.Length % 3 == 0)
                    {
                        await Task.Delay(8); // Tiny delay every 3 chars
                    }
                }
            }
            
            // Add AI response to history
            AddToHistory("assistant", fullResponse.ToString());
            UpdateStatus("connected", true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get Prometheus response");
            aiContent.Text += $"\n\n:( Error: {ex.Message}";
            UpdateStatus("error", false);
        }
        finally
        {
            _isProcessing = false;
            InputBox.Focus();
        }
    }
    
    private async Task AnimateThinkingDotsAsync(Run thinkingIndicator, System.Threading.CancellationToken ct)
    {
        var frames = new[] { " thinking.", " thinking..", " thinking..." };
        int i = 0;
        
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    thinkingIndicator.Text = frames[i % frames.Length];
                });
                i++;
                await Task.Delay(300, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
    }
    
    private void AddToHistory(string role, string message)
    {
        _conversationHistory.Add((role, message));
        
        // Keep only last MaxHistorySize exchanges (user + AI = 2 entries per exchange)
        while (_conversationHistory.Count > MaxHistorySize * 2)
        {
            _conversationHistory.RemoveAt(0);
        }
    }

    private void UpdateStatus(string text, bool isOk)
    {
        StatusText.Text = text;
        StatusDot.Fill = isOk 
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 201, 63)) 
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 189, 46));
    }

    private void HandleCommand(string command)
    {
        switch (command)
        {
            case "/clear":
                ChatPanel.Children.Clear();
                _conversationHistory.Clear();
                AddSystemMessage("Chat cleared.");
                break;
                
            case "/help":
                AddSystemMessage("─────────────────────────────────────────");
                AddSystemMessage("Available commands:");
                AddSystemMessage("  /clear   - Clear chat history");
                AddSystemMessage("  /new     - Start new conversation");
                AddSystemMessage("  /notes   - Find/Bridge to NeuralNotes (e.g., /notes space game)");
                AddSystemMessage("  /history - Show conversation summary");
                AddSystemMessage("  /help    - Show this help");
                AddSystemMessage("─────────────────────────────────────────");
                break;
                
            case string s when s.StartsWith("/notes"):
                var query = s.Replace("/notes", "").Trim();
                if (string.IsNullOrEmpty(query))
                {
                    AddSystemMessage("Usage: /notes [your search or finding request]");
                }
                else
                {
                    AddSystemMessage($"AI Searching for: {query}...");
                    NotesSearchRequested?.Invoke(query);
                    AddAIMessage($"I've updated your NeuralNotes panel to show matches for '{query}'. Take a look!");
                }
                break;
                
            case "/new":
                ChatPanel.Children.Clear();
                _conversationHistory.Clear();
                AddSystemMessage("Prometheus Terminal v1.0");
                AddSystemMessage("New conversation started.");
                AddSystemMessage("─────────────────────────────────────────");
                AddAIMessage("Ready for a fresh start. What's on your mind?");
                break;
                
            case "/history":
                if (_conversationHistory.Count == 0)
                {
                    AddSystemMessage("No conversation history yet.");
                }
                else
                {
                    AddSystemMessage("─────────────────────────────────────────");
                    AddSystemMessage($"Conversation history ({_conversationHistory.Count} messages):");
                    foreach (var (role, msg) in _conversationHistory.TakeLast(6))
                    {
                        var preview = msg.Length > 50 ? msg.Substring(0, 50) + "..." : msg;
                        AddSystemMessage($"  [{role}] {preview}");
                    }
                    AddSystemMessage("─────────────────────────────────────────");
                }
                break;
                
            default:
                AddSystemMessage($"Unknown command: {command}");
                AddSystemMessage("Type /help for available commands.");
                break;
        }
    }

    private void AddSystemMessage(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = TerminalDim,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 2),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace")
        };
        ChatPanel.Children.Add(block);
        ChatScrollViewer.ScrollToEnd();
    }

    private void AddUserMessage(string text)
    {
        var timestamp = DateTime.Now.ToString("HH:mm");
        
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        
        // Timestamp + prompt
        var header = new TextBlock
        {
            FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            Margin = new Thickness(0, 0, 0, 2)
        };
        header.Inlines.Add(new Run($"[{timestamp}] ") { Foreground = TerminalDim });
        header.Inlines.Add(new Run("you") { Foreground = TerminalCyan, FontWeight = FontWeights.Bold });
        header.Inlines.Add(new Run(" ❯ ") { Foreground = TerminalGreen });
        
        panel.Children.Add(header);
        
        // Message content (selectable)
        var content = new System.Windows.Controls.TextBox
        {
            Text = text,
            Foreground = TerminalWhite,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(20, 0, 0, 0),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            Cursor = System.Windows.Input.Cursors.IBeam
        };
        panel.Children.Add(content);
        
        ChatPanel.Children.Add(panel);
        ChatScrollViewer.ScrollToEnd();
    }

    private void AddAIMessage(string text)
    {
        var timestamp = DateTime.Now.ToString("HH:mm");
        
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        
        // Timestamp + prompt
        var header = new TextBlock
        {
            FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            Margin = new Thickness(0, 0, 0, 2)
        };
        header.Inlines.Add(new Run($"[{timestamp}] ") { Foreground = TerminalDim });
        header.Inlines.Add(new Run("prometheus") { Foreground = TerminalYellow, FontWeight = FontWeights.Bold });
        
        panel.Children.Add(header);
        
        // Message content (selectable)
        var content = new System.Windows.Controls.TextBox
        {
            Text = text,
            Foreground = TerminalWhite,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(20, 0, 0, 0),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            Cursor = System.Windows.Input.Cursors.IBeam
        };
        panel.Children.Add(content);
        
        ChatPanel.Children.Add(panel);
        ChatScrollViewer.ScrollToEnd();
    }

    private (StackPanel Panel, System.Windows.Controls.TextBox Content, Run ThinkingIndicator) CreateStreamingAIMessage()
    {
        var timestamp = DateTime.Now.ToString("HH:mm");
        
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        
        // Timestamp + prompt with thinking indicator
        var header = new TextBlock
        {
            FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            Margin = new Thickness(0, 0, 0, 2)
        };
        header.Inlines.Add(new Run($"[{timestamp}] ") { Foreground = TerminalDim });
        header.Inlines.Add(new Run("prometheus") { Foreground = TerminalYellow, FontWeight = FontWeights.Bold });
        
        // Thinking indicator (shown while waiting, hidden when content arrives)
        var thinkingIndicator = new Run(" thinking...") { Foreground = TerminalDim, FontStyle = FontStyles.Italic };
        header.Inlines.Add(thinkingIndicator);
        
        panel.Children.Add(header);
        
        // Empty message content for streaming (selectable)
        var content = new System.Windows.Controls.TextBox
        {
            Text = "",
            Foreground = TerminalWhite,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(20, 0, 0, 0),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            Cursor = System.Windows.Input.Cursors.IBeam
        };
        panel.Children.Add(content);
        
        ChatPanel.Children.Add(panel);
        ChatScrollViewer.ScrollToEnd();
        
        return (panel, content, thinkingIndicator);
    }

    private void AddErrorMessage(string text)
    {
        var block = new TextBlock
        {
            Text = $"Error: {text}",
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68)),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace")
        };
        ChatPanel.Children.Add(block);
        ChatScrollViewer.ScrollToEnd();
    }

    private StackPanel AddTypingIndicator()
    {
        var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        
        var text = new TextBlock
        {
            Text = "prometheus is typing",
            Foreground = TerminalDim,
            FontStyle = FontStyles.Italic,
            FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace")
        };
        panel.Children.Add(text);
        
        var dots = new TextBlock
        {
            Text = "...",
            Foreground = TerminalYellow,
            FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace")
        };
        panel.Children.Add(dots);
        
        ChatPanel.Children.Add(panel);
        ChatScrollViewer.ScrollToEnd();
        
        return panel;
    }
}
