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

    public void Show(string title, string icon, int xp)
    {
        TitleText.Text = title;
        IconText.Text = icon;
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
