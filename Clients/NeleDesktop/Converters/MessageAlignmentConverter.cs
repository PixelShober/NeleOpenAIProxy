using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NeleDesktop.Converters;

public sealed class MessageAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return HorizontalAlignment.Right;
        }

        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
