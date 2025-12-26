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
        // Bind Level info
        LevelBadgeText.Text = _user.Level.ToString();
        LevelTitleText.Text = $"Level {_user.Level}";
        XpProgressBar.Value = _user.LevelProgress * 100;
        XpText.Text = $"{_user.XpInCurrentLevel} / {_user.XpRequiredForLevel} XP";

        // Bind Achievements - sorted ascending by XP reward
        var achievements = _achievementService.GetAchievements(_user)
            .OrderBy(a => a.XpReward)
            .ToList();
        AchievementsList.ItemsSource = achievements;
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
