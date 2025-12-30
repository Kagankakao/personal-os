using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace KeganOS.Views.Components;

public partial class ToastNotification : System.Windows.Controls.UserControl
{
    public event EventHandler? OnToastClicked;

    public ToastNotification()
    {
        InitializeComponent();
    }

    public void Show(string title, string icon, int xp, string color = "#FFCC00")
    {
        HeaderText.Text = "[!] ACHIEVEMENT UNLOCKED";
        HeaderText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        TitleText.Text = title;
        IconText.Text = icon;
        IconText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        XpText.Text = $"+{xp} XP";
        XpText.Visibility = Visibility.Visible;

        var slideIn = (Storyboard)FindResource("SlideIn");
        var fadeOut = (Storyboard)FindResource("FadeOut");
        
        slideIn.Completed += (s, e) => fadeOut.Begin(this);
        fadeOut.Completed += (s, e) => 
        {
            if (Parent is System.Windows.Controls.Panel panel)
                panel.Children.Remove(this);
        };
        
        BeginStoryboard(slideIn);
    }

    private void Toast_Click(object sender, MouseButtonEventArgs e)
    {
        OnToastClicked?.Invoke(this, EventArgs.Empty);
        // Immediately hide on click
        var fadeOut = (Storyboard)FindResource("FadeOut");
        fadeOut.Begin(this);
    }

    /// <summary>
    /// Show a simple message toast (success, info, warning)
    /// </summary>
    public void ShowMessage(string message, string icon = "✓", string color = "#44CC44")
    {
        HeaderText.Text = "[!] SUCCESS";
        HeaderText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        TitleText.Text = message;
        IconText.Text = icon;
        IconText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        XpText.Visibility = Visibility.Collapsed;  // Hide XP for simple messages

        var slideIn = (Storyboard)FindResource("SlideIn");
        var fadeOut = (Storyboard)FindResource("FadeOut");
        
        slideIn.Completed += (s, e) => fadeOut.Begin(this);
        fadeOut.Completed += (s, e) => 
        {
            if (Parent is System.Windows.Controls.Panel panel)
                panel.Children.Remove(this);
        };
        
        BeginStoryboard(slideIn);
    }
}
