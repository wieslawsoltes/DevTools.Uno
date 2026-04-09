using Microsoft.UI.Xaml;

namespace DevToolsUno.Diagnostics.Internal;

internal sealed class DeferredLayoutRefresh
{
    private readonly FrameworkElement _owner;
    private readonly FrameworkElement[] _targets;
    private readonly int _maxAttempts;
    private bool _pending;
    private int _remainingAttempts;

    public DeferredLayoutRefresh(FrameworkElement owner, int maxAttempts, params FrameworkElement[] targets)
    {
        _owner = owner;
        _targets = targets;
        _maxAttempts = Math.Max(1, maxAttempts);

        owner.Loaded += OnLoaded;
        owner.SizeChanged += OnSizeChanged;
        owner.LayoutUpdated += OnLayoutUpdated;
        owner.Unloaded += OnUnloaded;
    }

    public void Request()
    {
        _pending = true;
        _remainingAttempts = _maxAttempts;
        RefreshTargets();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Request();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            Request();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _pending = false;

    private void OnLayoutUpdated(object? sender, object e)
    {
        if (!_pending || !HasVisibleLayout(_owner))
        {
            return;
        }

        if (TargetsHaveVisibleLayout())
        {
            _pending = false;
            return;
        }

        if (_remainingAttempts-- <= 0)
        {
            _pending = false;
            return;
        }

        RefreshTargets();
    }

    private bool TargetsHaveVisibleLayout()
    {
        foreach (var target in _targets)
        {
            if (!HasVisibleLayout(target))
            {
                return false;
            }
        }

        return true;
    }

    private void RefreshTargets()
    {
        _owner.InvalidateMeasure();
        _owner.InvalidateArrange();

        foreach (var target in _targets)
        {
            target.InvalidateMeasure();
            target.InvalidateArrange();
            target.UpdateLayout();
        }

        _owner.UpdateLayout();
    }

    private static bool HasVisibleLayout(FrameworkElement element)
        => element.Visibility == Visibility.Visible && element.ActualWidth > 0 && element.ActualHeight > 0;
}
