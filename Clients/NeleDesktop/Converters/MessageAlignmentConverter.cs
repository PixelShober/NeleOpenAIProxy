using System;
using System.Globalization;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using System.Windows.Data;

namespace NeleDesktop.Converters;

public sealed class MessageAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return WpfHorizontalAlignment.Right;
        }

        return WpfHorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
