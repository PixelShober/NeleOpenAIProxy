using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;

namespace NeleDesktop.Views;

public partial class PromptDialog : Window
{
    private PromptDialog(string title, string prompt, string? initialValue)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initialValue ?? string.Empty;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string ResponseText => InputBox.Text ?? string.Empty;

    public static string? Show(Window owner, string title, string prompt, string? initialValue = null)
    {
        var dialog = new PromptDialog(title, prompt, initialValue)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.ResponseText : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && GetAncestor<WpfButton>(source) is not null)
        {
            return;
        }

        DragMove();
    }

    private static T? GetAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}


