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

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(darkMode ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };

        WpfApplication.Current.Resources.MergedDictionaries.Clear();
        WpfApplication.Current.Resources.MergedDictionaries.Add(dictionary);
    }
}
