namespace KeganOS.Core.Models;

/// <summary>
/// User profile for the application
/// </summary>
public class User
{
    public int Id { get; set; }
    
    /// <summary>
    /// Display name shown in UI
    /// </summary>
    public string DisplayName { get; set; } = "";
    
    /// <summary>
    /// Personal symbol/identifier (e.g., "KAÆ[Æß#")
    /// Displayed in the top-left of the dashboard
    /// </summary>
    public string PersonalSymbol { get; set; } = "";
    
    /// <summary>
    /// Path to avatar image (optional)
    /// </summary>
    public string AvatarPath { get; set; } = "";
    
    /// <summary>
    /// The journal text file name in KEGOMODORO (e.g., "diary.txt")
    /// </summary>
    public string JournalFileName { get; set; } = "diary.txt";
    
    // Pixe.la integration
    public string? PixelaUsername { get; set; }
    public string? PixelaToken { get; set; }
    public string? PixelaGraphId { get; set; }
    
    // AI integration
    public string? GeminiApiKey { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastLoginAt { get; set; } = DateTime.Now;
}
