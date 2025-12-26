using System.Windows;
using System.Windows.Controls;

namespace KeganOS.Views;

/// <summary>
/// A confirmation dialog for profile deletion
/// </summary>
public partial class DeleteConfirmationWindow : Window
{
    private string _profileName;

    public DeleteConfirmationWindow(string profileName)
    {
        InitializeComponent();
        _profileName = profileName;
        ProfileNameText.Text = profileName;
        RequiredNameText.Text = profileName;
        
        Loaded += (s, e) => ConfirmationInput.Focus();
    }

    private void ConfirmationInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        DeleteButton.IsEnabled = ConfirmationInput.Text == _profileName;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
