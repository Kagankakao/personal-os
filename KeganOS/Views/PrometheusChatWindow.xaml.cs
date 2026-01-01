using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using Color = System.Windows.Media.Color;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using MouseButton = System.Windows.Input.MouseButton;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace KeganOS.Views;

/// <summary>
/// CLI-style Prometheus chat window with 4 UI states
/// </summary>
public partial class PrometheusChatWindow : Window
{
    private enum UIState { Loading, History, Chat }
    
    private readonly ILogger _logger = Log.ForContext<PrometheusChatWindow>();
    private readonly IPrometheusService _prometheusService;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly int? _userId;
    
    private UIState _currentState = UIState.Loading;
    private bool _isProcessing;
    private CancellationTokenSource? _spinnerCts;
    
    // History navigation
    private List<Conversation> _conversations = new();
    private int _selectedHistoryIndex = 0;
    private int? _currentConversationId;
    
    // In-memory conversation context (last 5 exchanges)
    private readonly List<(string Role, string Message)> _conversationHistory = new();
    private const int MaxHistorySize = 5;
    
    // Terminal colors
    private static readonly SolidColorBrush TerminalGreen = new(Color.FromRgb(0, 255, 65));
    private static readonly SolidColorBrush TerminalCyan = new(Color.FromRgb(0, 217, 255));
    private static readonly SolidColorBrush TerminalYellow = new(Color.FromRgb(255, 215, 0));
    private static readonly SolidColorBrush TerminalWhite = new(Color.FromRgb(204, 204, 204));
    private static readonly SolidColorBrush TerminalDim = new(Color.FromRgb(102, 102, 102));
    private static readonly SolidColorBrush TerminalSelected = new(Color.FromRgb(42, 42, 64));

    public event Action<string>? NotesSearchRequested;

    public PrometheusChatWindow(IPrometheusService prometheusService, IChatHistoryService chatHistoryService, int? userId = null)
    {
        InitializeComponent();
        _prometheusService = prometheusService;
        _chatHistoryService = chatHistoryService;
        _userId = userId;
        
        // Start directly in Loading state (unified splash/loading)
        SetState(UIState.Loading);
        _ = AutoLoadOnStartupAsync();
        
        _spinnerCts = new CancellationTokenSource();
        _ = StartSpinnerLoopAsync(_spinnerCts.Token);
    }
    
    private async Task StartSpinnerLoopAsync(CancellationToken token)
    {
        string[] frames = { "|", "/", "-", "\\" };
        int i = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                Dispatcher.Invoke(() => {
                    if (_currentState == UIState.Loading)
                    {
                        LoadSpinner.Text = frames[i % frames.Length];
                    }
                });

                i++;
                await Task.Delay(80, token);
            }
        }
        catch (OperationCanceledException) { }
    }
    
    private async Task AutoLoadOnStartupAsync()
    {
        // Initial "Consulting" phase (replaces the old splash delay)
        await Task.Delay(1800);
        LoadingText.Text = "Loading history...";
        await Task.Delay(400);
        await LoadHistoryAsync();
    }

    #region State Management
    
    private void SetState(UIState state)
    {
        _currentState = state;
        
        // Hide all panels
        LoadingPanel.Visibility = Visibility.Collapsed;
        HistoryScroller.Visibility = Visibility.Collapsed;
        ChatScrollViewer.Visibility = Visibility.Collapsed;
        InputRow.Visibility = Visibility.Collapsed;
        
        switch (state)
        {
            case UIState.Loading:
                LoadingPanel.Visibility = Visibility.Visible;
                ModeIndicator.Text = "Loading...";
                HotkeyHints.Text = "";
                break;
                
            case UIState.History:
                HistoryScroller.Visibility = Visibility.Visible;
                ModeIndicator.Text = "History";
                HotkeyHints.Text = "j/k: navigate, Enter: open, /new: new chat, Esc: quit";
                ConversationTitle.Text = "";
                break;
                
            case UIState.Chat:
                ChatScrollViewer.Visibility = Visibility.Visible;
                InputRow.Visibility = Visibility.Visible;
                ModeIndicator.Text = "Chat";
                HotkeyHints.Text = "Tab: history, /new: new chat, /clear: clear, Esc: quit";
                InputBox.Focus();
                break;
        }
    }
    
    #endregion

    #region Window Events
    
    private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Global escape to quit
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }
        
        switch (_currentState)
        {
            case UIState.Loading:
                // Auto-loads, no key handling needed
                break;
                
            case UIState.History:
                HandleHistoryNavigation(e);
                break;
                
            case UIState.Chat:
                if (e.Key == Key.Tab && !_isProcessing)
                {
                    await LoadHistoryAsync();
                    e.Handled = true;
                }
                else if (e.Key == Key.J && Keyboard.Modifiers == ModifierKeys.None && !InputBox.IsFocused)
                {
                    ChatScrollViewer.ScrollToVerticalOffset(ChatScrollViewer.VerticalOffset + 50);
                }
                else if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.None && !InputBox.IsFocused)
                {
                    ChatScrollViewer.ScrollToVerticalOffset(ChatScrollViewer.VerticalOffset - 50);
                }
                break;
        }
    }
    
    private void HandleHistoryNavigation(System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.J:
            case Key.Down:
                if (_selectedHistoryIndex < _conversations.Count - 1)
                {
                    _selectedHistoryIndex++;
                    RenderHistoryList();
                }
                e.Handled = true;
                break;
                
            case Key.K:
            case Key.Up:
                if (_selectedHistoryIndex > 0)
                {
                    _selectedHistoryIndex--;
                    RenderHistoryList();
                }
                e.Handled = true;
                break;
                
            case Key.Enter:
                if (_conversations.Count > 0)
                {
                    _ = OpenConversationAsync(_conversations[_selectedHistoryIndex]);
                }
                e.Handled = true;
                break;
                
            case Key.Delete:
                if (_conversations.Count > 0)
                {
                    _ = DeleteConversationAsync(_conversations[_selectedHistoryIndex]);
                }
                e.Handled = true;
                break;
                
            case Key.N:
                _ = CreateNewConversationAsync();
                e.Handled = true;
                break;
        }
    }
    
    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
    
    private void CloseBtn_Click(object sender, MouseButtonEventArgs e) => Close();
    private void CloseBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => CloseBtn.Fill = new SolidColorBrush(Color.FromRgb(255, 120, 120));
    private void CloseBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => CloseBtn.Fill = new SolidColorBrush(Color.FromRgb(255, 95, 86));
    
    #endregion

    #region History Management
    
    private async Task LoadHistoryAsync()
    {
        SetState(UIState.Loading);
        LoadingText.Text = "Loading history...";
        
        try
        {
            _conversations = await _chatHistoryService.GetConversationsAsync(_userId);
            _selectedHistoryIndex = 0;
            
            SetState(UIState.History);
            RenderHistoryList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load conversation history");
            LoadingText.Text = "Failed to load history :(";
        }
    }
    
    private void RenderHistoryList()
    {
        HistoryPanel.Children.Clear();
        
        if (_conversations.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "No conversations yet. Press 'n' to start a new chat.",
                Foreground = TerminalDim,
                FontSize = 12,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            HistoryPanel.Children.Add(emptyText);
            return;
        }
        
        for (int i = 0; i < _conversations.Count; i++)
        {
            var conv = _conversations[i];
            var isSelected = i == _selectedHistoryIndex;
            
            var item = CreateHistoryItem(conv, isSelected);
            HistoryPanel.Children.Add(item);
        }
    }
    
    private Border CreateHistoryItem(Conversation conv, bool isSelected)
    {
        var border = new Border
        {
            Background = isSelected ? TerminalSelected : Brushes.Transparent,
            BorderBrush = isSelected ? TerminalCyan : Brushes.Transparent,
            BorderThickness = isSelected ? new Thickness(1) : new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 4, 0, 0),
            CornerRadius = new CornerRadius(4)
        };
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        
        // Title
        var title = new TextBlock
        {
            Text = conv.Title,
            Foreground = isSelected ? TerminalWhite : TerminalDim,
            FontSize = 13,
            FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(title, 0);
        Grid.SetColumn(title, 0);
        grid.Children.Add(title);
        
        // Time
        var time = new TextBlock
        {
            Text = FormatRelativeTime(conv.LastMessageAt),
            Foreground = isSelected ? TerminalGreen : TerminalDim,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(time, 0);
        Grid.SetColumn(time, 1);
        grid.Children.Add(time);
        
        // Preview
        if (!string.IsNullOrEmpty(conv.Preview))
        {
            var preview = new TextBlock
            {
                Text = conv.Preview.Length > 60 ? conv.Preview.Substring(0, 60) + "..." : conv.Preview,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(preview, 1);
            Grid.SetColumnSpan(preview, 2);
            grid.Children.Add(preview);
        }
        
        border.Child = grid;
        return border;
    }
    
    private string FormatRelativeTime(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff.TotalMinutes < 1) return "now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}min";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d";
        return time.ToString("MMM d");
    }
    
    private async Task OpenConversationAsync(Conversation conv)
    {
        SetState(UIState.Loading);
        LoadingText.Text = "Consulting journey...";
        
        try
        {
            _currentConversationId = conv.Id;
            var messages = await _chatHistoryService.GetMessagesAsync(conv.Id);
            
            // Load messages into UI
            ChatPanel.Children.Clear();
            _conversationHistory.Clear();
            
            foreach (var msg in messages)
            {
                if (msg.Role == "user")
                    AddUserMessage(msg.Content);
                else
                    AddAIMessage(msg.Content);
                
                _conversationHistory.Add((msg.Role, msg.Content));
            }
            
            // Trim history to max size
            while (_conversationHistory.Count > MaxHistorySize * 2)
                _conversationHistory.RemoveAt(0);
            
            ConversationTitle.Text = $" / {conv.Title}";
            SetState(UIState.Chat);
            ChatScrollViewer.ScrollToEnd();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load conversation");
            LoadingText.Text = $"Error: {ex.Message}";
        }
    }
    
    private async Task CreateNewConversationAsync()
    {
        try
        {
            var conv = await _chatHistoryService.CreateConversationAsync(_userId);
            _currentConversationId = conv.Id;
            
            ChatPanel.Children.Clear();
            _conversationHistory.Clear();
            
            AddAIMessage("Hey! :) I'm Prometheus. I remember everything about our conversations and your journey. What's on your mind?");
            
            ConversationTitle.Text = " / New Chat";
            SetState(UIState.Chat);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create new conversation");
        }
    }
    
    private async Task DeleteConversationAsync(Conversation conv)
    {
        try
        {
            await _chatHistoryService.DeleteConversationAsync(conv.Id);
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete conversation");
        }
    }
    
    #endregion

    #region Chat Input
    
    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(InputBox.Text) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    private async void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_isProcessing)
        {
            await SendMessageAsync();
            e.Handled = true;
        }
    }

    private async Task SendMessageAsync()
    {
        var message = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(message) || _isProcessing)
            return;

        InputBox.Text = "";
        
        // Handle terminal commands
        if (message.StartsWith("/"))
        {
            await HandleCommandAsync(message.ToLower());
            return;
        }

        // Ensure we have a conversation
        if (_currentConversationId == null)
        {
            var conv = await _chatHistoryService.CreateConversationAsync(_userId);
            _currentConversationId = conv.Id;
        }

        _isProcessing = true;
        UpdateStatus("typing...", false);

        // Save user message
        await _chatHistoryService.AddMessageAsync(_currentConversationId.Value, "user", message);
        _conversationHistory.Add(("user", message));
        AddUserMessage(message);

        // Update conversation title from first message
        if (_conversationHistory.Count(h => h.Role == "user") == 1)
        {
            var title = message.Length > 40 ? message.Substring(0, 40) + "..." : message;
            await _chatHistoryService.UpdateConversationAsync(_currentConversationId.Value, title: title);
            ConversationTitle.Text = $" / {title}";
        }

        // Create AI message block for streaming
        var (aiPanel, aiContent, thinkingIndicator) = CreateStreamingAIMessage();
        var fullResponse = new StringBuilder();
        bool firstChunk = true;
        
        var animationCts = new CancellationTokenSource();
        _ = AnimateThinkingDotsAsync(thinkingIndicator, animationCts.Token);

        try
        {
            await foreach (var chunk in _prometheusService.ConsultStreamingAsync(message, _userId, _conversationHistory))
            {
                if (firstChunk)
                {
                    animationCts.Cancel();
                    thinkingIndicator.Text = "";
                    firstChunk = false;
                }
                
                fullResponse.Append(chunk);
                
                foreach (char c in chunk)
                {
                    aiContent.Text += c;
                    if (c == ' ' || c == '.' || c == '!' || c == '?' || c == '\n')
                    {
                        await Task.Delay(10);
                        ChatScrollViewer.ScrollToEnd();
                    }
                    else if (aiContent.Text.Length % 3 == 0)
                    {
                        await Task.Delay(4);
                    }
                }
            }
            
            // Save AI response
            var response = fullResponse.ToString();
            await _chatHistoryService.AddMessageAsync(_currentConversationId.Value, "assistant", response);
            _conversationHistory.Add(("assistant", response));
            
            // Update preview
            var preview = response.Split('\n').FirstOrDefault() ?? "";
            await _chatHistoryService.UpdateConversationAsync(_currentConversationId.Value, preview: preview);
            
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
            while (_conversationHistory.Count > MaxHistorySize * 2)
                _conversationHistory.RemoveAt(0);
            InputBox.Focus();
        }
    }
    
    private async Task HandleCommandAsync(string command)
    {
        switch (command)
        {
            case "/clear":
                ChatPanel.Children.Clear();
                _conversationHistory.Clear();
                AddSystemMessage("Chat cleared.");
                break;
                
            case "/new":
                await CreateNewConversationAsync();
                break;
                
            case "/help":
                AddSystemMessage("─────────────────────────────────────────");
                AddSystemMessage("Commands:");
                AddSystemMessage("  /clear  - Clear current chat");
                AddSystemMessage("  /new    - Start new conversation");
                AddSystemMessage("  /notes  - Search NeuralNotes");
                AddSystemMessage("  Tab     - Switch to history view");
                AddSystemMessage("─────────────────────────────────────────");
                break;
                
            case string s when s.StartsWith("/notes"):
                var query = s.Replace("/notes", "").Trim();
                if (!string.IsNullOrEmpty(query))
                {
                    NotesSearchRequested?.Invoke(query);
                    AddAIMessage($"I've updated your NeuralNotes panel to show matches for '{query}'.");
                }
                break;
        }
    }
    
    #endregion

    #region UI Helpers
    
    private void UpdateStatus(string text, bool isOk)
    {
        StatusText.Text = text;
        StatusLabel.Text = isOk ? "Live" : "Busy";
        StatusLabel.Foreground = isOk ? TerminalGreen : TerminalYellow;
        StatusDot.Fill = isOk ? TerminalGreen : TerminalYellow;
    }

    private void AddSystemMessage(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = TerminalDim,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 4)
        };
        ChatPanel.Children.Add(tb);
    }

    private void AddUserMessage(string text)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(50, 10, 0, 10) };
        
        var header = new TextBlock
        {
            Text = $"You   {DateTime.Now:HH:mm}",
            Foreground = TerminalDim,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 4)
        };
        
        var bubble = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 50)),
            CornerRadius = new CornerRadius(12, 12, 4, 12),
            Padding = new Thickness(14, 10, 14, 10)
        };
        
        bubble.Child = new TextBlock
        {
            Text = text,
            Foreground = TerminalWhite,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        };
        
        panel.Children.Add(header);
        panel.Children.Add(bubble);
        ChatPanel.Children.Add(panel);
        ChatScrollViewer.ScrollToEnd();
    }

    private void AddAIMessage(string text)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 50, 10) };
        
        var header = new TextBlock
        {
            Text = $"Prometheus   {DateTime.Now:HH:mm}",
            Foreground = TerminalCyan,
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 4)
        };
        
        var content = new TextBlock
        {
            Text = text,
            Foreground = TerminalWhite,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        
        panel.Children.Add(header);
        panel.Children.Add(content);
        ChatPanel.Children.Add(panel);
        ChatScrollViewer.ScrollToEnd();
    }

    private (StackPanel, TextBlock, Run) CreateStreamingAIMessage()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 50, 10) };
        
        var header = new TextBlock { Foreground = TerminalCyan, FontSize = 10, Margin = new Thickness(0, 0, 0, 4) };
        header.Inlines.Add(new Run($"Prometheus   {DateTime.Now:HH:mm}"));
        var thinkingIndicator = new Run(" thinking...") { Foreground = TerminalDim };
        header.Inlines.Add(thinkingIndicator);
        
        var content = new TextBlock
        {
            Foreground = TerminalWhite,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        
        panel.Children.Add(header);
        panel.Children.Add(content);
        ChatPanel.Children.Add(panel);
        ChatScrollViewer.ScrollToEnd();
        
        return (panel, content, thinkingIndicator);
    }

    private async Task AnimateThinkingDotsAsync(Run indicator, CancellationToken ct)
    {
        var frames = new[] { " thinking.", " thinking..", " thinking..." };
        int i = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() => indicator.Text = frames[i++ % frames.Length]);
                await Task.Delay(300, ct);
            }
        }
        catch (OperationCanceledException) { }
    }
    
    #endregion
}
