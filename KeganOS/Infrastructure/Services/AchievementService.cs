using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;

namespace KeganOS.Infrastructure.Services;

public class AchievementService : IAchievementService
{
    private readonly ILogger _logger = Log.ForContext<AchievementService>();
    private readonly IUserService _userService;
    
    // Event
    public event EventHandler<Achievement>? OnAchievementUnlocked;

    // Hardcoded catalog of available achievements (Level-based progression)
    private readonly List<Achievement> _catalog = new()
    {
        // Level milestones - progressively unlock as user levels up (Harder growth)
        new() { Id = "lvl1", Name = "Starter", Description = "Begin your journey.", Icon = "🌱", RequirementType = AchievementRequirementType.Level, RequirementValue = 1, XpReward = 10 },
        new() { Id = "lvl5", Name = "High Five", Description = "Reach Level 5.", Icon = "✋", RequirementType = AchievementRequirementType.Level, RequirementValue = 5, XpReward = 100 },
        new() { Id = "lvl10", Name = "Double Digits", Description = "Reach Level 10.", Icon = "🔟", RequirementType = AchievementRequirementType.Level, RequirementValue = 10, XpReward = 500 },
        new() { Id = "lvl20", Name = "Committed", Description = "Reach Level 20.", Icon = "🎯", RequirementType = AchievementRequirementType.Level, RequirementValue = 20, XpReward = 1000 },
        new() { Id = "lvl50", Name = "Master", Description = "Reach Level 50.", Icon = "👑", RequirementType = AchievementRequirementType.Level, RequirementValue = 50, XpReward = 5000 },
        new() { Id = "lvl100", Name = "Legend", Description = "Reach Level 100.", Icon = "🌟", RequirementType = AchievementRequirementType.Level, RequirementValue = 100, XpReward = 10000 },
        
        // Hours milestones
        new() { Id = "hours10", Name = "Dedicated", Description = "Log 10 total hours.", Icon = "⏰", RequirementType = AchievementRequirementType.TotalHours, RequirementValue = 10, XpReward = 200 },
        new() { Id = "hours100", Name = "Expert", Description = "Log 100 total hours.", Icon = "🎓", RequirementType = AchievementRequirementType.TotalHours, RequirementValue = 100, XpReward = 2000 },
        new() { Id = "hours500", Name = "Tireless", Description = "Log 500 total hours.", Icon = "💪", RequirementType = AchievementRequirementType.TotalHours, RequirementValue = 500, XpReward = 5000 },
        new() { Id = "hours1000", Name = "Immortal", Description = "Log 1000 total hours.", Icon = "♾️", RequirementType = AchievementRequirementType.TotalHours, RequirementValue = 1000, XpReward = 20000 },
    };

    public AchievementService(IUserService userService)
    {
        _userService = userService;
    }

    public List<Achievement> GetAchievements(User user)
    {
        // Return catalog with updated status
        return _catalog.Select(a => 
        {
            var copy = new Achievement
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                Icon = a.Icon,
                RequirementType = a.RequirementType,
                RequirementValue = a.RequirementValue,
                XpReward = a.XpReward,
                IsUnlocked = user.UnlockedAchievementIds.Contains(a.Id)
            };
            return copy;
        }).ToList();
    }

    public async Task<bool> AddXpAsync(User user, int amount)
    {
        int oldLevel = user.Level;
        user.XP += amount;
        
        int newLevel = user.Level;
        bool leveledUp = newLevel > oldLevel;

        if (leveledUp)
        {
            _logger.Information("User {User} leveled up to {Level}!", user.DisplayName, newLevel);
        }

        // Persist XP to database
        await _userService.UpdateUserAsync(user);
        
        return leveledUp;
    }

    public async Task<List<Achievement>> CheckAchievementsAsync(User user)
    {
        var newlyUnlocked = new List<Achievement>();
        bool changed = false;

        foreach (var achievement in _catalog)
        {
            if (user.UnlockedAchievementIds.Contains(achievement.Id)) continue;

            bool unlock = false;

            switch (achievement.RequirementType)
            {
                case AchievementRequirementType.TotalHours:
                    if (user.TotalHours >= achievement.RequirementValue) unlock = true;
                    break;
                case AchievementRequirementType.Level:
                    if (user.Level >= achievement.RequirementValue) unlock = true;
                    break;
                case AchievementRequirementType.Streak:
                    // Streak logic deferred - not yet implemented
                    break;
                case AchievementRequirementType.Manual:
                    // Manual achievements are unlocked through specific actions
                    break;
            }

            if (unlock)
            {
                user.UnlockedAchievementIds.Add(achievement.Id);
                user.XP += achievement.XpReward; // Bonus XP for achievement
                newlyUnlocked.Add(achievement);
                changed = true;
                _logger.Information("Unlocked achievement: {Name} for user {User}", achievement.Name, user.DisplayName);
                
                // Fire event
                OnAchievementUnlocked?.Invoke(this, achievement);
            }
        }

        if (changed)
        {
            // Sync changes to DB
            await _userService.UpdateUserAsync(user);
        }

        return newlyUnlocked;
    }
}
