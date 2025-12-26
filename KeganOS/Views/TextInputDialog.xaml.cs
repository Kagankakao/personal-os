using System.Windows;
using System.Windows.Input;

namespace KeganOS.Views;

/// <summary>
/// A simple text input dialog for Ask Prometheus
/// </summary>
public partial class TextInputDialog : Window
{
    public string ResponseText => InputTextBox.Text;

    public TextInputDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        
        Loaded += (s, e) => 
        {
            InputTextBox.Focus();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
