using System.Windows;

namespace NeleDesktop.Views;

public partial class PromptDialog : Window
{
    private PromptDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
    }

    public string ResponseText => InputBox.Text ?? string.Empty;

    public static string? Show(Window owner, string title, string prompt)
    {
        var dialog = new PromptDialog(title, prompt)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.ResponseText : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
