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
    /// Personal symbol/identifier (e.g., "🦭" or custom emoji)
    /// </summary>
    public string PersonalSymbol { get; set; } = "";
    
    /// <summary>
    /// Computed display symbol - returns 🦭 if PersonalSymbol is empty
    /// </summary>
    public string DisplaySymbol => string.IsNullOrEmpty(PersonalSymbol) ? "🦭" : PersonalSymbol;
    
    /// <summary>
    /// Path to avatar image (optional)
    /// </summary>
    public string AvatarPath { get; set; } = "";
    
    /// <summary>
    /// Check if avatar image exists
    /// </summary>
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarPath) && System.IO.File.Exists(AvatarPath);
    
    /// <summary>
    /// Check if user has NO avatar image
    /// </summary>
    public bool HasNoAvatar => !HasAvatar;

    
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
    
    /// <summary>
    /// Comma-separated hex values for saved/recent colors in the palette
    /// </summary>
    public string? SavedColors { get; set; }
    
    // Stats
    /// <summary>
    /// Total focus hours logged
    /// </summary>
    public double TotalHours { get; set; } = 0;
    
    /// <summary>
    /// Best streak ever achieved (persisted)
    /// </summary>
    public int BestStreak { get; set; } = 0;

    /// <summary>
    /// Experience Points
    /// </summary>
    public long XP { get; set; } = 0;
    
    /// <summary>
    /// List of unlocked achievement IDs (serialized as JSON or comma-separated)
    /// </summary>
    public List<string> UnlockedAchievementIds { get; set; } = new List<string>();

    /// <summary>
    /// Property for EF Core / Dapper mapping to store/retrieve JSON string from DB
    /// </summary>
    public string UnlockedAchievementsJson
    {
        get => string.Join(",", UnlockedAchievementIds);
        set
        {
            if (string.IsNullOrEmpty(value))
                UnlockedAchievementIds = new List<string>();
            else
                UnlockedAchievementIds = value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }

    /// <summary>
    /// User level calculated from XP (Quadratic curve: Level = 1 + floor(sqrt(XP / 100)))
    /// This makes leveling "harder" as you progress.
    /// </summary>
    public int Level => 1 + (int)Math.Floor(Math.Sqrt(XP / 100.0));

    /// <summary>
    /// The total XP required to reach the START of the current level
    /// </summary>
    public long CurrentLevelXpStart => (long)(Math.Pow(Level - 1, 2) * 100);

    /// <summary>
    /// The total XP required to reach the NEXT level
    /// </summary>
    public long NextLevelXpStart => (long)(Math.Pow(Level, 2) * 100);

    /// <summary>
    /// XP earned within the current level
    /// </summary>
    public long XpInCurrentLevel => XP - CurrentLevelXpStart;

    /// <summary>
    /// Total XP needed between current level and next level
    /// </summary>
    public long XpRequiredForLevel => NextLevelXpStart - CurrentLevelXpStart;
    
    /// <summary>
    /// Display string for hours (e.g., "42.5h")
    /// </summary>
    public string DisplayHours => $"{TotalHours:F1}h";
    
    /// <summary>
    /// Display string for level (e.g., "Lv.5")
    /// </summary>
    public string DisplayLevel => $"Lv.{Level}";
    
    /// <summary>
    /// Dynamic tier color for the current level
    /// </summary>
    public string DisplayLevelColor
    {
        get
        {
            if (Level >= 1000) return "#010b19"; // Deep Space
            if (Level >= 500) return "#8B0000";  // Bloody Red
            if (Level >= 250) return "#E67E22";  // Vivid Orange
            if (Level >= 100) return "#F1C40F";  // Goldenrod
            if (Level >= 50) return "#9B59B6";   // Amethyst Purple
            if (Level >= 10) return "#5DADE2";   // Steel Blue
            return "#88CC88";                    // Fresh Green
        }
    }
    
    /// <summary>
    /// Progress to next level (0 to 1)
    /// </summary>
    public double LevelProgress => XpRequiredForLevel == 0 ? 0 : (double)XpInCurrentLevel / XpRequiredForLevel;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastLoginAt { get; set; } = DateTime.Now;
}
