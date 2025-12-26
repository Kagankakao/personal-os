using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using TabControl = System.Windows.Controls.TabControl;
using Clipboard = System.Windows.Clipboard;

namespace KeganOS.Views;

public partial class WeeklyReportWindow : Window
{
    private readonly ILogger _logger = Log.ForContext<WeeklyReportWindow>();
    private readonly IAnalyticsService _analyticsService;
    private readonly User _currentUser;
    private DateTime _currentWeekStart;

    public WeeklyReportWindow(User user, IAnalyticsService analyticsService)
    {
        InitializeComponent();
        _currentUser = user;
        _analyticsService = analyticsService;
        
        // Start from current week's Monday
        _currentWeekStart = DateTime.Today;
        while (_currentWeekStart.DayOfWeek != DayOfWeek.Monday)
            _currentWeekStart = _currentWeekStart.AddDays(-1);

        LoadData();
    }

    private async void LoadData()
    {
        try
        {
            // Update Date Range
            var end = _currentWeekStart.AddDays(6);
            DateRangeText.Text = $"Week of {_currentWeekStart:MMM d} - {end:MMM d, yyyy}";

            // Get Data
            var data = await _analyticsService.GetWeeklyDataAsync(_currentUser, _currentWeekStart);
            
            // Draw Chart
            DrawChart(data);

            // Update Stats
            double total = data.Values.Sum();
            double avg = total / 7.0;
            TotalHoursText.Text = $"Total: {total:F1} hrs";
            AvgHoursText.Text = $"Avg: {avg:F1}/day";

            // Get Real Streak
            var streak = await _analyticsService.CalculateCurrentStreakAsync(_currentUser);
            StreakText.Text = $"Streak: {streak} days";
            
            // Reset Insights
            NotesInsightText.Text = "Loading insights...";
            FocusInsightText.Text = "Select tab to generate...";
            LifeInsightText.Text = "Select tab to generate...";
            
            // Generate initial insight (Notes)
            GenerateInsight("Notes");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load weekly report data");
        }
    }

    private void DrawChart(Dictionary<DayOfWeek, double> data)
    {
        ChartGrid.Children.Clear();
        
        double maxVal = data.Values.DefaultIfEmpty(0).Max();
        if (maxVal == 0) maxVal = 1; // Avoid division by zero

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        
        for (int i = 0; i < 7; i++)
        {
            double val = data[days[i]];
            double heightPercentage = val / maxVal;
            
            var bar = new Border
            {
                Background = new SolidColorBrush(val > 0 ? (val >= 4 ? Color.FromRgb(255, 255, 255) : Color.FromRgb(100, 100, 100)) : Color.FromRgb(30, 30, 30)),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Margin = new Thickness(8, 0, 8, 0),
                Height = Math.Max(2, 150 * heightPercentage), // Min height 2 for visibility
                VerticalAlignment = VerticalAlignment.Bottom,
                ToolTip = $"{val:F1} hrs"
            };

            Grid.SetColumn(bar, i);
            ChartGrid.Children.Add(bar);
        }
    }

    private async void GenerateInsight(string type)
    {
        TextBlock targetBlock = type switch
        {
            "Notes" => NotesInsightText,
            "Focus" => FocusInsightText,
            "Life" => LifeInsightText,
            _ => NotesInsightText
        };

        if (targetBlock.Text != "Loading insights..." && targetBlock.Text != "Select tab to generate...") 
            return; // Already loaded

        targetBlock.Text = "Thinking...";

        var insight = await _analyticsService.GenerateInsightAsync(_currentUser, _currentWeekStart, type);
        targetBlock.Text = insight;
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is TabControl tc && tc.SelectedItem is TabItem item)
        {
            if (item.Header == null) return;
            string header = item.Header.ToString()!.Trim();
            if (header.Contains("Notes")) GenerateInsight("Notes");
            else if (header.Contains("Focus")) GenerateInsight("Focus");
            else if (header.Contains("Life")) GenerateInsight("Life");
        }
    }

    private void PrevWeek_Click(object sender, RoutedEventArgs e)
    {
        _currentWeekStart = _currentWeekStart.AddDays(-7);
        LoadData();
    }

    private void NextWeek_Click(object sender, RoutedEventArgs e)
    {
        _currentWeekStart = _currentWeekStart.AddDays(7);
        LoadData();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"WeeklyReport_{_currentUser.DisplayName}_{_currentWeekStart:yyyy-MM-dd}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("========================================");
                sb.AppendLine($"   WEEKLY REPORT: {_currentUser.DisplayName}");
                sb.AppendLine($"   {DateRangeText.Text}");
                sb.AppendLine("========================================");
                sb.AppendLine();
                sb.AppendLine($"Total Hours: {TotalHoursText.Text.Replace("Total: ", "")}");
                sb.AppendLine($"Average: {AvgHoursText.Text.Replace("Avg: ", "")}");
                sb.AppendLine($"Current Streak: {StreakText.Text.Replace("Streak: ", "")}");
                sb.AppendLine();
                sb.AppendLine("--- AI INSIGHTS ---");
                sb.AppendLine("NOTES:");
                sb.AppendLine(NotesInsightText.Text);
                sb.AppendLine();
                sb.AppendLine("FOCUS:");
                sb.AppendLine(FocusInsightText.Text);
                sb.AppendLine();
                sb.AppendLine("LIFE:");
                sb.AppendLine(LifeInsightText.Text);
                sb.AppendLine();
                sb.AppendLine("Report generated by KeganOS");

                System.IO.File.WriteAllText(saveFileDialog.FileName, sb.ToString());
                MessageBox.Show("Report exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to export report");
            MessageBox.Show("Failed to export report: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Share_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📊 My Weekly Progress on KeganOS ({_currentWeekStart:MMM d} - {_currentWeekStart.AddDays(6):MMM d})");
            sb.AppendLine($"🕒 {TotalHoursText.Text} | {AvgHoursText.Text}");
            sb.AppendLine($"🔥 {StreakText.Text}");
            sb.AppendLine();
            
            // Add a snippet of the insight if available
            var insight = NotesInsightText.Text;
            if (insight != "Loading insights..." && insight != "Thinking...")
            {
                var lines = insight.Split('\n').Take(3).ToList();
                sb.AppendLine("💡 Top Insight:");
                foreach(var line in lines) sb.AppendLine(line);
            }

            Clipboard.SetText(sb.ToString());
            
            // Visual feedback on button? No, MessageBox is easier for now.
            MessageBox.Show("Weekly summary copied to clipboard!", "Shared", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to share report");
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
