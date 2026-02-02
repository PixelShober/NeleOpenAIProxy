using System;
using System.Globalization;
using WpfApplication = System.Windows.Application;
using System.Windows.Data;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace NeleDesktop.Converters;

public sealed class MessageBubbleBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (WpfApplication.Current is null)
        {
            return WpfBrushes.Transparent;
        }

        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return WpfApplication.Current.Resources["UserBubbleBrush"] as WpfBrush ?? WpfBrushes.Transparent;
        }

        return WpfApplication.Current.Resources["AssistantBubbleBrush"] as WpfBrush ?? WpfBrushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
