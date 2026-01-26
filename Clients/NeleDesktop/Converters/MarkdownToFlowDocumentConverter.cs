using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Documents;

namespace NeleDesktop.Converters;

public sealed class MarkdownToFlowDocumentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        return MarkdownFormatter.BuildDocument(text);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

}
