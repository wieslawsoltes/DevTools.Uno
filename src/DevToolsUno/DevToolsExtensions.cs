using DevToolsUno.Diagnostics;
using Microsoft.UI.Xaml;
using DevToolsHost = DevToolsUno.Diagnostics.DevTools;

namespace DevToolsUno;

public static class DevToolsExtensions
{
    public static IDisposable AttachDevTools(this FrameworkElement root)
        => DevToolsHost.Attach(root, new DevToolsOptions());

    public static IDisposable AttachDevTools(this FrameworkElement root, DevToolsOptions options)
        => DevToolsHost.Attach(root, options);

    public static IDisposable AttachDevTools(this Window window)
        => DevToolsHost.Attach(window, new DevToolsOptions());

    public static IDisposable AttachDevTools(this Window window, DevToolsOptions options)
        => DevToolsHost.Attach(window, options);

    public static IDisposable AttachDevTools(this Application application)
        => DevToolsHost.Attach(application, new DevToolsOptions());

    public static IDisposable AttachDevTools(this Application application, DevToolsOptions options)
        => DevToolsHost.Attach(application, options);
}
