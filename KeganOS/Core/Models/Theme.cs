using System.Windows.Media;

namespace KeganOS.Core.Models;

/// <summary>
/// Represents a UI theme for KeganOS and KEGOMODORO
/// </summary>
public class Theme
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // UI Colors (Hex strings)
    public string BackgroundColor { get; set; } = "#0D0D0D";
    public string AccentColor { get; set; } = "#FFFFFF";
    public string TextColor { get; set; } = "#FFFFFF";
    public string SecondaryTextColor { get; set; } = "#888888";
    
    // Images (relative path or absolute path)
    public string MainImagePath { get; set; } = string.Empty;
    public string FloatingImagePath { get; set; } = string.Empty;
    
    // Metadata
    public bool IsCustom { get; set; } = false;
    public bool IsDark { get; set; } = true;
    
    // For UI Binding convenience
    public SolidColorBrush BackgroundBrush 
    {
        get 
        {
            try 
            {
                return (SolidColorBrush)new BrushConverter().ConvertFromString(BackgroundColor)!;
            }
            catch 
            {
                return System.Windows.Media.Brushes.Black;
            }
        }
    }
    
    public SolidColorBrush AccentBrush 
    {
        get 
        {
            try 
            {
                return (SolidColorBrush)new BrushConverter().ConvertFromString(AccentColor)!;
            }
            catch 
            {
                return System.Windows.Media.Brushes.White;
            }
        }
    }

    public SolidColorBrush TextColorBrush 
    {
        get 
        {
            try 
            {
                return (SolidColorBrush)new BrushConverter().ConvertFromString(TextColor)!;
            }
            catch 
            {
                return System.Windows.Media.Brushes.White;
            }
        }
    }
}
