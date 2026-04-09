using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DevToolsUno.Diagnostics.Internal;

internal sealed class PaneSplitterController
{
    private readonly FrameworkElement _owner;
    private readonly FrameworkElement _splitter;
    private readonly ColumnDefinition _primaryColumn;
    private readonly ColumnDefinition _secondaryColumn;
    private readonly Action _requestLayout;
    private readonly double _primaryMinWidth;
    private readonly double _secondaryMinWidth;
    private readonly double _splitterWidth;
    private bool _isResizing;
    private double _lastPointerX;

    public PaneSplitterController(
        FrameworkElement owner,
        FrameworkElement splitter,
        ColumnDefinition primaryColumn,
        ColumnDefinition secondaryColumn,
        Action requestLayout)
    {
        _owner = owner;
        _splitter = splitter;
        _primaryColumn = primaryColumn;
        _secondaryColumn = secondaryColumn;
        _requestLayout = requestLayout;
        _primaryMinWidth = primaryColumn.MinWidth;
        _secondaryMinWidth = secondaryColumn.MinWidth;
        _splitterWidth = splitter.Width > 0 ? splitter.Width : 8;

        SplitterChrome.ApplyHorizontal(_splitter);
        _splitter.PointerPressed += OnSplitterPointerPressed;
        _splitter.PointerMoved += OnSplitterPointerMoved;
        _splitter.PointerReleased += OnSplitterPointerReleased;
        _splitter.PointerCaptureLost += OnSplitterPointerCaptureLost;
        _owner.Loaded += OnOwnerLoaded;
        _owner.SizeChanged += OnOwnerSizeChanged;
    }

    public void RefreshLayout()
    {
        ConstrainColumns();
        _requestLayout();
    }

    private void OnOwnerLoaded(object sender, RoutedEventArgs e)
    {
        ConstrainColumns();
        _requestLayout();
    }

    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0)
        {
            return;
        }

        ConstrainColumns();
    }

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isResizing = true;
        _lastPointerX = e.GetCurrentPoint(_owner).Position.X;
        _splitter.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing)
        {
            return;
        }

        var currentX = e.GetCurrentPoint(_owner).Position.X;
        var delta = currentX - _lastPointerX;
        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        ResizeColumns(delta);
        _lastPointerX = currentX;
        e.Handled = true;
    }

    private void OnSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndResize();
        e.Handled = true;
    }

    private void OnSplitterPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndResize();
    }

    private void ResizeColumns(double delta)
    {
        var availableWidth = GetAvailableWidth();
        if (availableWidth <= 0)
        {
            return;
        }

        var primaryWidth = _primaryColumn.ActualWidth > 0 ? _primaryColumn.ActualWidth : availableWidth * 0.4;
        var newPrimaryWidth = Math.Max(_primaryMinWidth, primaryWidth + delta);
        var maxPrimaryWidth = Math.Max(_primaryMinWidth, availableWidth - _secondaryMinWidth);
        newPrimaryWidth = Math.Min(newPrimaryWidth, maxPrimaryWidth);

        var newSecondaryWidth = Math.Max(_secondaryMinWidth, availableWidth - newPrimaryWidth);
        _primaryColumn.Width = new GridLength(newPrimaryWidth);
        _secondaryColumn.Width = new GridLength(newSecondaryWidth);
        _requestLayout();
    }

    private void ConstrainColumns()
    {
        var availableWidth = GetAvailableWidth();
        if (availableWidth <= 0)
        {
            return;
        }

        var primaryWidth = _primaryColumn.ActualWidth > 0 ? _primaryColumn.ActualWidth : availableWidth * 0.4;
        primaryWidth = Math.Max(_primaryMinWidth, Math.Min(primaryWidth, Math.Max(_primaryMinWidth, availableWidth - _secondaryMinWidth)));
        var secondaryWidth = Math.Max(_secondaryMinWidth, availableWidth - primaryWidth);

        _primaryColumn.Width = new GridLength(primaryWidth);
        _secondaryColumn.Width = new GridLength(secondaryWidth);
    }

    private double GetAvailableWidth()
        => Math.Max(0, _owner.ActualWidth - _splitterWidth);

    private void EndResize()
    {
        _isResizing = false;
        _splitter.ReleasePointerCaptures();
    }
}
