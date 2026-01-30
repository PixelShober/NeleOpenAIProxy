using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NeleDesktop.Models;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace NeleDesktop.Converters;

public static class MarkdownRichTextBox
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(MarkdownRichTextBox),
        new PropertyMetadata(string.Empty, OnTextChanged));

    private static readonly DependencyProperty MeasuredWidthProperty = DependencyProperty.RegisterAttached(
        "MeasuredWidth",
        typeof(double),
        typeof(MarkdownRichTextBox),
        new PropertyMetadata(0d));

    private static readonly DependencyProperty IsMaxWidthHookedProperty = DependencyProperty.RegisterAttached(
        "IsMaxWidthHooked",
        typeof(bool),
        typeof(MarkdownRichTextBox),
        new PropertyMetadata(false));

    private static readonly DependencyProperty IsDataContextHookedProperty = DependencyProperty.RegisterAttached(
        "IsDataContextHooked",
        typeof(bool),
        typeof(MarkdownRichTextBox),
        new PropertyMetadata(false));

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
        var document = MarkdownFormatter.BuildDocument(text);
        var measuredWidth = MeasureTextWidth(richTextBox, text);
        richTextBox.SetValue(MeasuredWidthProperty, measuredWidth);
        HookMaxWidthChanged(richTextBox);
        HookDataContextChanged(richTextBox);
        ApplyDocumentWidth(richTextBox, document, measuredWidth);
        richTextBox.Document = document;
        richTextBox.SizeChanged -= RichTextBox_SizeChanged;
        richTextBox.SizeChanged += RichTextBox_SizeChanged;
    }

    private static void ApplyDocumentWidth(WpfRichTextBox richTextBox, System.Windows.Documents.FlowDocument document, double measuredWidth)
    {
        var isUser = richTextBox.DataContext is ChatMessage message
            && message.Role.Equals("user", StringComparison.OrdinalIgnoreCase);
        var maxWidth = richTextBox.MaxWidth;
        if (double.IsNaN(maxWidth) || double.IsInfinity(maxWidth) || maxWidth <= 0)
        {
            maxWidth = richTextBox.ActualWidth;
        }
        if (double.IsInfinity(maxWidth) || maxWidth <= 0)
        {
            maxWidth = 560;
        }
        if (!isUser)
        {
            var assistantWidth = Math.Max(120, maxWidth);
            document.PageWidth = assistantWidth;
            document.ColumnWidth = assistantWidth;
            return;
        }

        var paddedWidth = measuredWidth + 12;
        var userWidth = Math.Min(maxWidth, Math.Max(32, paddedWidth));
        document.PageWidth = userWidth;
        document.ColumnWidth = userWidth;
    }

    private static double MeasureTextWidth(WpfRichTextBox richTextBox, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var cleaned = text.Replace("\r", string.Empty)
            .Replace("*", string.Empty)
            .Replace("_", string.Empty);
        var dpi = VisualTreeHelper.GetDpi(richTextBox);
        var typeface = new Typeface(richTextBox.FontFamily, richTextBox.FontStyle, richTextBox.FontWeight, richTextBox.FontStretch);
        var maxWidth = 0d;

        foreach (var line in cleaned.Split('\n'))
        {
            var formatted = new FormattedText(
                line,
                CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
                typeface,
                richTextBox.FontSize,
                System.Windows.Media.Brushes.Black,
                dpi.PixelsPerDip);

            maxWidth = Math.Max(maxWidth, formatted.WidthIncludingTrailingWhitespace);
        }

        return maxWidth;
    }

    private static void RichTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not WpfRichTextBox richTextBox || richTextBox.Document is null)
        {
            return;
        }

        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 1)
        {
            return;
        }

        var measuredWidth = (double)richTextBox.GetValue(MeasuredWidthProperty);
        ApplyDocumentWidth(richTextBox, richTextBox.Document, measuredWidth);
    }

    private static void HookMaxWidthChanged(WpfRichTextBox richTextBox)
    {
        if ((bool)richTextBox.GetValue(IsMaxWidthHookedProperty))
        {
            return;
        }

        var descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
            FrameworkElement.MaxWidthProperty,
            typeof(WpfRichTextBox));
        descriptor?.AddValueChanged(richTextBox, RichTextBox_MaxWidthChanged);
        richTextBox.SetValue(IsMaxWidthHookedProperty, true);
    }

    private static void HookDataContextChanged(WpfRichTextBox richTextBox)
    {
        if ((bool)richTextBox.GetValue(IsDataContextHookedProperty))
        {
            return;
        }

        richTextBox.DataContextChanged += RichTextBox_DataContextChanged;
        richTextBox.SetValue(IsDataContextHookedProperty, true);
    }

    private static void RichTextBox_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not WpfRichTextBox richTextBox || richTextBox.Document is null)
        {
            return;
        }

        var measuredWidth = (double)richTextBox.GetValue(MeasuredWidthProperty);
        ApplyDocumentWidth(richTextBox, richTextBox.Document, measuredWidth);
    }

    private static void RichTextBox_MaxWidthChanged(object? sender, EventArgs e)
    {
        if (sender is not WpfRichTextBox richTextBox || richTextBox.Document is null)
        {
            return;
        }

        var measuredWidth = (double)richTextBox.GetValue(MeasuredWidthProperty);
        ApplyDocumentWidth(richTextBox, richTextBox.Document, measuredWidth);
    }
}
