using System;
using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DevTools.Uno.Diagnostics.Views;

public sealed partial class EventsPageView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;
    private bool _isPaneResizing;
    private bool _isRouteResizing;
    private double _lastPaneX;
    private double _lastRouteY;

    public EventsPageView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, ListenersGrid, EventLogGrid, RoutesGrid);
        SplitterChrome.ApplyHorizontal(PaneSplitter);
        SplitterChrome.ApplyVertical(RouteSplitter);
    }

    private void OnPaneSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPaneResizing = true;
        _lastPaneX = e.GetCurrentPoint(LayoutRoot).Position.X;
        PaneSplitter.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPaneSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPaneResizing)
        {
            return;
        }

        var currentX = e.GetCurrentPoint(LayoutRoot).Position.X;
        var delta = currentX - _lastPaneX;
        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        ResizeColumns(delta);
        _lastPaneX = currentX;
        e.Handled = true;
    }

    private void OnPaneSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndPaneResize();
        e.Handled = true;
    }

    private void OnPaneSplitterPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndPaneResize();
    }

    private void OnRouteSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isRouteResizing = true;
        _lastRouteY = e.GetCurrentPoint(ResultsRoot).Position.Y;
        RouteSplitter.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnRouteSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isRouteResizing)
        {
            return;
        }

        var currentY = e.GetCurrentPoint(ResultsRoot).Position.Y;
        var delta = currentY - _lastRouteY;
        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        ResizeRows(delta);
        _lastRouteY = currentY;
        e.Handled = true;
    }

    private void OnRouteSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndRouteResize();
        e.Handled = true;
    }

    private void OnRouteSplitterPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndRouteResize();
    }

    private void ResizeColumns(double delta)
    {
        var availableWidth = LayoutRoot.ActualWidth - PaneSplitter.Width;
        if (availableWidth <= 0)
        {
            return;
        }

        var leftMinWidth = ListenerColumn.MinWidth;
        var rightMinWidth = ResultsColumn.MinWidth;
        var newLeftWidth = Math.Max(leftMinWidth, ListenerColumn.ActualWidth + delta);
        var maxLeftWidth = Math.Max(leftMinWidth, availableWidth - rightMinWidth);
        newLeftWidth = Math.Min(newLeftWidth, maxLeftWidth);
        var newRightWidth = Math.Max(rightMinWidth, availableWidth - newLeftWidth);

        ListenerColumn.Width = new GridLength(newLeftWidth);
        ResultsColumn.Width = new GridLength(newRightWidth);
        _layoutRefresh.Request();
    }

    private void ResizeRows(double delta)
    {
        var availableHeight = ResultsRoot.ActualHeight - RouteSplitter.Height;
        if (availableHeight <= 0)
        {
            return;
        }

        const double logMinHeight = 220;
        const double routeMinHeight = 180;

        var newLogHeight = Math.Max(logMinHeight, LogRow.ActualHeight + delta);
        var maxLogHeight = Math.Max(logMinHeight, availableHeight - routeMinHeight);
        newLogHeight = Math.Min(newLogHeight, maxLogHeight);
        var newRouteHeight = Math.Max(routeMinHeight, availableHeight - newLogHeight);

        LogRow.Height = new GridLength(newLogHeight);
        RouteRow.Height = new GridLength(newRouteHeight);
        _layoutRefresh.Request();
    }

    private void EndPaneResize()
    {
        _isPaneResizing = false;
        PaneSplitter.ReleasePointerCaptures();
    }

    private void EndRouteResize()
    {
        _isRouteResizing = false;
        RouteSplitter.ReleasePointerCaptures();
    }
}
