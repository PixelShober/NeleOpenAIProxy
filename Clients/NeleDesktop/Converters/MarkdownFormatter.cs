using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfBrushes = System.Windows.Media.Brushes;

namespace NeleDesktop.Converters;

internal static class MarkdownFormatter
{
    public static FlowDocument BuildDocument(string text)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Background = WpfBrushes.Transparent
        };

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0)
        };

        foreach (var inline in ParseInlines(text))
        {
            paragraph.Inlines.Add(inline);
        }

        document.Blocks.Add(paragraph);
        return document;
    }

    private static Inline[] ParseInlines(string text)
    {
        var inlines = new System.Collections.Generic.List<Inline>();
        var buffer = new StringBuilder();
        var bold = false;
        var italic = false;

        void Flush()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            var run = new Run(buffer.ToString())
            {
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = italic ? FontStyles.Italic : FontStyles.Normal
            };
            inlines.Add(run);
            buffer.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];
            if (current == '\r')
            {
                continue;
            }

            if (current == '\n')
            {
                Flush();
                inlines.Add(new LineBreak());
                continue;
            }

            if (current is '*' or '_')
            {
                var marker = current;
                var count = 1;
                while (i + count < text.Length && text[i + count] == marker && count < 3)
                {
                    count++;
                }

                Flush();

                switch (count)
                {
                    case 3:
                        bold = !bold;
                        italic = !italic;
                        break;
                    case 2:
                        bold = !bold;
                        break;
                    default:
                        italic = !italic;
                        break;
                }

                i += count - 1;
                continue;
            }

            buffer.Append(current);
        }

        Flush();
        return inlines.ToArray();
    }
}
