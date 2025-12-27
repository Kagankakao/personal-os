using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace KeganOS.Views;

public partial class AchievementsWindow : Window
{
    private readonly User _user;
    private readonly IAchievementService _achievementService;

    public AchievementsWindow(User user, IAchievementService achievementService)
    {
        InitializeComponent();
        _user = user;
        _achievementService = achievementService;
        
        LoadData();
    }

    private void LoadData()
    {
        // Bind Level info dynamically
        LevelText.Text = $"Level {_user.Level}";
        XpText.Text = $"{_user.XpInCurrentLevel} / {_user.XpRequiredForLevel} XP";
        
        long remaining = _user.XpRequiredForLevel - _user.XpInCurrentLevel;
        XpRemainingText.Text = $"You are ascending. Only {remaining} XP remaining for the next level.";

        // Dynamic ASCII Progress Bar
        int totalDots = 25;
        int filledDots = (int)(_user.LevelProgress * totalDots);
        AsciiProgressBar.Text = new string('░', filledDots) + new string(' ', totalDots - filledDots);
        
        PopulateAchievements();
    }

    private void PopulateAchievements()
    {
        var allAchievements = _achievementService.GetAchievements(_user).ToList();
        
        UnlockedAchievementsList.ItemsSource = allAchievements
            .Where(a => a.IsUnlocked)
            .OrderByDescending(a => a.XpReward)
            .ToList();
            
        LockedAchievementsList.ItemsSource = allAchievements
            .Where(a => !a.IsUnlocked)
            .OrderBy(a => a.XpReward)
            .ToList();
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
