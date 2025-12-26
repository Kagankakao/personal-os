namespace KeganOS.Core.Models;

public enum AchievementRequirementType
{
    TotalHours,
    Streak,
    Level, // Level-based achievements
    Manual // Special events, etc.
}

public class Achievement
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "🏆"; // Emoji default
    public AchievementRequirementType RequirementType { get; set; }
    public double RequirementValue { get; set; } // e.g., 100 (hours) or 7 (days)
    public int XpReward { get; set; }
    
    // For UI binding
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }

    public string RequirementText => RequirementType switch
    {
        AchievementRequirementType.Level => $"Lv.{RequirementValue}",
        AchievementRequirementType.TotalHours => $"{(int)RequirementValue}h",
        AchievementRequirementType.Streak => $"{(int)RequirementValue} days",
        _ => ""
    };
}
