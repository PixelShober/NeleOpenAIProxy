using System;
using System.Windows;

namespace NeleDesktop.Services;

public sealed class ThemeService
{
    public void ApplyTheme(bool darkMode)
    {
        if (Application.Current is null)
        {
            return;
        }

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(darkMode ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };

        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dictionary);
    }
}
