using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DevToolsUno.Diagnostics.Internal;

internal sealed class RowSplitterController
{
    private readonly FrameworkElement _owner;
    private readonly FrameworkElement _splitter;
    private readonly RowDefinition _upperRow;
    private readonly RowDefinition _lowerRow;
    private readonly Action _requestLayout;
    private readonly double _upperMinHeight;
    private readonly double _lowerMinHeight;
    private readonly double _splitterHeight;
    private bool _isResizing;
    private double _lastPointerY;

    public RowSplitterController(
        FrameworkElement owner,
        FrameworkElement splitter,
        RowDefinition upperRow,
        RowDefinition lowerRow,
        Action requestLayout)
    {
        _owner = owner;
        _splitter = splitter;
        _upperRow = upperRow;
        _lowerRow = lowerRow;
        _requestLayout = requestLayout;
        _upperMinHeight = upperRow.MinHeight;
        _lowerMinHeight = lowerRow.MinHeight;
        _splitterHeight = splitter.Height > 0 ? splitter.Height : 8;

        SplitterChrome.ApplyVertical(_splitter);
        _splitter.PointerPressed += OnSplitterPointerPressed;
        _splitter.PointerMoved += OnSplitterPointerMoved;
        _splitter.PointerReleased += OnSplitterPointerReleased;
        _splitter.PointerCaptureLost += OnSplitterPointerCaptureLost;
        _owner.Loaded += OnOwnerLoaded;
        _owner.SizeChanged += OnOwnerSizeChanged;
    }

    public void RefreshLayout()
    {
        ConstrainRows();
        _requestLayout();
    }

    private void OnOwnerLoaded(object sender, RoutedEventArgs e)
    {
        ConstrainRows();
        _requestLayout();
    }

    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Height <= 0)
        {
            return;
        }

        ConstrainRows();
    }

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isResizing = true;
        _lastPointerY = e.GetCurrentPoint(_owner).Position.Y;
        _splitter.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing)
        {
            return;
        }

        var currentY = e.GetCurrentPoint(_owner).Position.Y;
        var delta = currentY - _lastPointerY;
        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        ResizeRows(delta);
        _lastPointerY = currentY;
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

    private void ResizeRows(double delta)
    {
        var availableHeight = GetAvailableHeight();
        if (availableHeight <= 0)
        {
            return;
        }

        var upperHeight = _upperRow.ActualHeight > 0 ? _upperRow.ActualHeight : availableHeight * 0.55;
        var newUpperHeight = Math.Max(_upperMinHeight, upperHeight + delta);
        var maxUpperHeight = Math.Max(_upperMinHeight, availableHeight - _lowerMinHeight);
        newUpperHeight = Math.Min(newUpperHeight, maxUpperHeight);

        var newLowerHeight = Math.Max(_lowerMinHeight, availableHeight - newUpperHeight);
        _upperRow.Height = new GridLength(newUpperHeight);
        _lowerRow.Height = new GridLength(newLowerHeight);
        _requestLayout();
    }

    private void ConstrainRows()
    {
        var availableHeight = GetAvailableHeight();
        if (availableHeight <= 0)
        {
            return;
        }

        var upperHeight = _upperRow.ActualHeight > 0 ? _upperRow.ActualHeight : availableHeight * 0.55;
        upperHeight = Math.Max(_upperMinHeight, Math.Min(upperHeight, Math.Max(_upperMinHeight, availableHeight - _lowerMinHeight)));
        var lowerHeight = Math.Max(_lowerMinHeight, availableHeight - upperHeight);

        _upperRow.Height = new GridLength(upperHeight);
        _lowerRow.Height = new GridLength(lowerHeight);
    }

    private double GetAvailableHeight()
        => Math.Max(0, _owner.ActualHeight - _splitterHeight);

    private void EndResize()
    {
        _isResizing = false;
        _splitter.ReleasePointerCaptures();
    }
}
