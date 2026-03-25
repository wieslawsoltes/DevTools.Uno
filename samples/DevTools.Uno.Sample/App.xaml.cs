using DevTools.Uno;
using DevTools.Uno.Diagnostics;
using Microsoft.UI.Xaml;

namespace DevTools.Uno.Sample;

public sealed partial class App : Application
{
    private readonly bool _exitImmediately;
    private IDisposable? _devToolsAttachment;
    private Window? _window;

    public App()
        : this(false)
    {
    }

    public App(bool exitImmediately)
    {
        _exitImmediately = exitImmediately;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window();
        _window.Content = new MainPage();
        _devToolsAttachment = _window.AttachDevTools(new DevToolsOptions
        {
            LaunchView = DevToolsViewKind.VisualTree,
            ShowAsChildWindow = false,
        });
        _window.Closed += OnWindowClosed;
        _window.Activate();

        if (_exitImmediately)
        {
            _window.DispatcherQueue.TryEnqueue(() => _window?.Close());
        }
    }

    internal static void InitializeLogging()
    {
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
        }

        _devToolsAttachment?.Dispose();
        _devToolsAttachment = null;
        _window = null;
    }
}
