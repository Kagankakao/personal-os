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
        TitleText.Text = title;
        IconText.Text = icon;
        IconText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        XpText.Text = $"+{xp} XP";

        var slideIn = (Storyboard)FindResource("SlideIn");
        var fadeOut = (Storyboard)FindResource("FadeOut");
        
        slideIn.Completed += (s, e) => fadeOut.Begin(this);
        fadeOut.Completed += (s, e) => ((System.Windows.Controls.Panel)Parent).Children.Remove(this);
        
        BeginStoryboard(slideIn);
    }

    private void Toast_Click(object sender, MouseButtonEventArgs e)
    {
        OnToastClicked?.Invoke(this, EventArgs.Empty);
    }
}
