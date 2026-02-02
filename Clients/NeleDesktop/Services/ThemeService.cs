using System;
using WpfApplication = System.Windows.Application;
using System.Windows;

namespace NeleDesktop.Services;

public sealed class ThemeService
{
    public void ApplyTheme(bool darkMode)
    {
        if (WpfApplication.Current is null)
        {
            return;
        }

        var themeName = darkMode ? "Dark" : "Light";
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/NeleDesktop;component/Themes/{themeName}.xaml", UriKind.Absolute)
        };

        WpfApplication.Current.Resources.MergedDictionaries.Clear();
        WpfApplication.Current.Resources.MergedDictionaries.Add(dictionary);
    }
}
