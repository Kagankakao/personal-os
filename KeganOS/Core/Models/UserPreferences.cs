namespace KeganOS.Core.Models;

/// <summary>
/// User preferences for customization
/// </summary>
public class UserPreferences
{
    public int UserId { get; set; }
    public string ThemeName { get; set; } = "Office2019Black";
    public string AccentColor { get; set; } = "#8B0000";
    public int KegomoDoroWorkMin { get; set; } = 25;
    public int KegomoDoroShortBreak { get; set; } = 5;
    public int KegomoDoroLongBreak { get; set; } = 20;
    public string KegomoDoroBackgroundColor { get; set; } = "#8B0000";
    public string? KegomoDoroMainImagePath { get; set; }
}
