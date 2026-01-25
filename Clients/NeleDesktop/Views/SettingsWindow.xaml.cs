using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using NeleDesktop.ViewModels;
using WpfButton = System.Windows.Controls.Button;

namespace NeleDesktop.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private CancellationTokenSource? _apiKeyLoadCts;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void ApiKey_TextChanged(object sender, TextChangedEventArgs e)
    {
        _apiKeyLoadCts?.Cancel();

        var cts = new CancellationTokenSource();
        _apiKeyLoadCts = cts;

        try
        {
            await Task.Delay(600, cts.Token);
            await _viewModel.LoadModelsAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Hotkey_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
        {
            return;
        }

        var hotkey = BuildHotkey(key, Keyboard.Modifiers);
        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            _viewModel.Hotkey = hotkey;
        }
    }

    private void TemporaryHotkey_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
        {
            return;
        }

        var hotkey = BuildHotkey(key, Keyboard.Modifiers);
        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            _viewModel.TemporaryHotkey = hotkey;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && GetAncestor<WpfButton>(source) is not null)
        {
            return;
        }

        DragMove();
    }

    private static T? GetAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
    }

    private static string BuildHotkey(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        parts.Add(FormatKey(key));
        return string.Join("+", parts);
    }

    private static string FormatKey(Key key)
    {
        if (key == Key.Return)
        {
            return "Enter";
        }

        if (key == Key.Escape)
        {
            return "Esc";
        }

        var converter = new KeyConverter();
        return converter.ConvertToString(key) ?? key.ToString();
    }
}




