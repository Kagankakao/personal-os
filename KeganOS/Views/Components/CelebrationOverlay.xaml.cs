using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace KeganOS.Views.Components;

public partial class CelebrationOverlay : System.Windows.Controls.UserControl
{
    public CelebrationOverlay()
    {
        InitializeComponent();
    }

    public void Show(string achievementName, string icon, int xpReward, string color = "#FFCC00")
    {
        AchievementName.Text = achievementName;
        BadgeIcon.Text = icon;
        BadgeIcon.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        RewardText.Text = $"+{xpReward} XP";
        
        this.Visibility = Visibility.Visible;
        
        // Simple scale-in animation
        var anim = new DoubleAnimation(0.5, 1.0, TimeSpan.FromSeconds(0.5))
        {
            EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
        };
        
        var scale = new System.Windows.Media.ScaleTransform(0.5, 0.5);
        this.RenderTransform = scale;
        this.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, anim);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, anim);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Visibility = Visibility.Collapsed;
    }
}
