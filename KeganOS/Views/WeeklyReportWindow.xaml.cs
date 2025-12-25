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
            
            // Logarithmic-ish scaling for better visibility of small values? No, linear is fine for hours.
            
            var bar = new Border
            {
                Background = new SolidColorBrush(val > 0 ? (val >= 4 ? Color.FromRgb(255, 255, 255) : Color.FromRgb(100, 100, 100)) : Color.FromRgb(30, 30, 30)),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Margin = new Thickness(8, 0, 8, 0),
                Height = Math.Max(2, 150 * heightPercentage), // Min height 2 for visibility
                VerticalAlignment = VerticalAlignment.Bottom,
                ToolTip = $"{val:F1} hrs"
            };

            // Animate height? Maybe later.

            Grid.SetColumn(bar, i);
            ChartGrid.Children.Add(bar);
            
            // Add text label on top if space permits
            if (val > 0)
            {
                var label = new TextBlock
                {
                    Text = $"{val:0.#}h",
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, bar.Height + 2)
                };
                // Can't easily stack outside border in simple grid, skip for now to keep simple
            }
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
        MessageBox.Show("PDF Export feature coming soon!", "Export");
    }

    private void Share_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Share feature coming soon!", "Share");
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
