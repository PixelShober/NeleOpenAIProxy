using System.Windows;
using System.Windows.Controls;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace NeleDesktop.Converters;

public static class MarkdownRichTextBox
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(MarkdownRichTextBox),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static void SetText(DependencyObject element, string value)
    {
        element.SetValue(TextProperty, value);
    }

    public static string GetText(DependencyObject element)
    {
        return (string)element.GetValue(TextProperty);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WpfRichTextBox richTextBox)
        {
            return;
        }

        var text = e.NewValue as string ?? string.Empty;
        richTextBox.Document = MarkdownFormatter.BuildDocument(text);
    }
}
