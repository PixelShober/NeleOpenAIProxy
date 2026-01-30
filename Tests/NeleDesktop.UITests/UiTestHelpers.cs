using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace NeleDesktop.UITests;

internal static class UiTestHelpers
{
    private static readonly object AppLock = new();

    public static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplication();
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    public static void EnsureApplication()
    {
        if (Application.Current is not null)
        {
            return;
        }

        lock (AppLock)
        {
            if (Application.Current is null)
            {
                _ = new Application();
            }
        }
    }

    public static ResourceDictionary LoadThemeDictionary(string themeFile)
    {
        return new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/NeleDesktop;component/Themes/{themeFile}", UriKind.Absolute)
        };
    }

    public static void ApplyTheme(ResourceDictionary dictionary)
    {
        EnsureApplication();
        var resources = Application.Current!.Resources;
        resources.MergedDictionaries.Clear();
        resources.MergedDictionaries.Add(dictionary);
        resources["BooleanToVisibilityConverter"] = new System.Windows.Controls.BooleanToVisibilityConverter();
        resources["MessageAlignmentConverter"] = new NeleDesktop.Converters.MessageAlignmentConverter();
        resources["MessageBubbleBrushConverter"] = new NeleDesktop.Converters.MessageBubbleBrushConverter();
        resources["MarkdownToFlowDocumentConverter"] = new NeleDesktop.Converters.MarkdownToFlowDocumentConverter();
        resources["BubbleWidthConverter"] = new NeleDesktop.Converters.BubbleWidthConverter();
        resources["CountToVisibilityConverter"] = new NeleDesktop.Converters.CountToVisibilityConverter();
    }

    public static T? FindVisualChild<T>(DependencyObject root, Func<T, bool>? predicate = null)
        where T : DependencyObject
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < children; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is T match && (predicate is null || predicate(match)))
                {
                    return match;
                }

                queue.Enqueue(child);
            }
        }

        return null;
    }

    public static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    public static T? FindLogicalChild<T>(DependencyObject root, Func<T, bool>? predicate = null)
        where T : DependencyObject
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in LogicalTreeHelper.GetChildren(current))
            {
                if (child is not DependencyObject depChild)
                {
                    continue;
                }

                if (depChild is T match && (predicate is null || predicate(match)))
                {
                    return match;
                }

                queue.Enqueue(depChild);
            }
        }

        return null;
    }

    public static DataTemplate? FindTemplate(ResourceDictionary resources, Type dataType)
    {
        foreach (var resource in resources.Values)
        {
            if (resource is DataTemplate template && template.DataType is Type type && type == dataType)
            {
                return template;
            }
        }

        return null;
    }

    public static Color GetResourceColor(ResourceDictionary dictionary, string key)
    {
        if (dictionary[key] is Color color)
        {
            return color;
        }

        throw new InvalidOperationException($"Color resource '{key}' not found.");
    }

    public static double ContrastRatio(Color a, Color b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(double channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var r = Channel(color.R);
        var g = Channel(color.G);
        var b = Channel(color.B);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    public static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ =>
        {
            frame.Continue = false;
            return null!;
        }), null);
        Dispatcher.PushFrame(frame);
    }
}
