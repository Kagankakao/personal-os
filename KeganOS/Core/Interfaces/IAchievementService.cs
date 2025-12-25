using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

public interface IAchievementService
{
    /// <summary>
    /// Checks for new unlocks based on user stats and returns newly unlocked achievements.
    /// </summary>
    Task<List<Achievement>> CheckAchievementsAsync(User user);

    /// <summary>
    /// Gets all achievements with their unlocked status for the user.
    /// </summary>
    List<Achievement> GetAchievements(User user);

    /// <summary>
    /// Adds XP to user and calculates new level progress.
    /// Returns true if user leveled up.
    /// </summary>
    Task<bool> AddXpAsync(User user, int amount);

    /// <summary>
    /// Event fired when an achievement is unlocked.
    /// </summary>
    event EventHandler<Achievement> OnAchievementUnlocked;
}
