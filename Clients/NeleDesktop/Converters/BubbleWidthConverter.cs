using System;
using System.Globalization;
using System.Windows.Data;

namespace NeleDesktop.Converters;

public sealed class BubbleWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not double width || width <= 0)
        {
            return 560d;
        }

        var role = values.Length > 1 ? values[1] as string ?? string.Empty : string.Empty;
        var isUser = role.Equals("user", StringComparison.OrdinalIgnoreCase);
        var mode = parameter as string ?? string.Empty;
        if (string.Equals(mode, "min", StringComparison.OrdinalIgnoreCase))
        {
            return isUser ? 0d : Math.Max(0, width - 16);
        }
        var available = Math.Max(0, width - 32);
        return available;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
