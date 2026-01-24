using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NeleDesktop.Converters;

public sealed class MessageBubbleBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Application.Current is null)
        {
            return Brushes.Transparent;
        }

        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.Resources["UserBubbleBrush"] as Brush ?? Brushes.Transparent;
        }

        return Application.Current.Resources["AssistantBubbleBrush"] as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
