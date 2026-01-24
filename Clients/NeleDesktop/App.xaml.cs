using System.Windows;

namespace NeleDesktop;

public partial class App : Application
{
    public static bool IsShuttingDown { get; private set; }

    public static void RequestShutdown()
    {
        IsShuttingDown = true;
        Current.Shutdown();
    }
}
