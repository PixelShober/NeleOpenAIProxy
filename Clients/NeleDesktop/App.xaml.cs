using System;
using System.Drawing;
using System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfExitEventArgs = System.Windows.ExitEventArgs;
using WpfStartupEventArgs = System.Windows.StartupEventArgs;

namespace NeleDesktop;

public partial class App : WpfApplication
{
    public static bool IsShuttingDown { get; private set; }
    private NotifyIcon? _notifyIcon;

    protected override void OnStartup(WpfStartupEventArgs e)
    {
        base.OnStartup(e);
        InitializeTrayIcon();
    }

    protected override void OnExit(WpfExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        base.OnExit(e);
    }

    public static void RequestShutdown()
    {
        IsShuttingDown = true;
        WpfApplication.Current.Shutdown();
    }

    private void InitializeTrayIcon()
    {
        var icon = LoadTrayIcon() ?? SystemIcons.Application;
        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Nele AI",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => RequestShutdown());
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (WpfApplication.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ShowFromTray();
        }
    }

    private void OpenSettings()
    {
        if (WpfApplication.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.OpenSettingsFromTray();
        }
    }

    private Icon? LoadTrayIcon()
    {
        var resource = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/NeleAi.ico"));
        if (resource is null)
        {
            return null;
        }

        return new Icon(resource.Stream);
    }
}
