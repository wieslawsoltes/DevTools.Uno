using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DevTools.Uno.Diagnostics.Internal;

internal sealed class DetailsPaneController
{
    private readonly FrameworkElement _owner;
    private readonly FrameworkElement _splitter;
    private readonly ColumnDefinition _splitterColumn;
    private readonly ColumnDefinition _primaryColumn;
    private readonly ColumnDefinition _detailsColumn;
    private readonly FrameworkElement _detailsPane;
    private readonly Button _toggleButton;
    private readonly Action _requestLayout;
    private readonly double _primaryMinWidth;
    private readonly double _detailsMinWidth;
    private readonly double _splitterWidth;
    private bool _isCollapsed;
    private bool _isResizing;
    private double _lastPointerX;
    private double _expandedDetailsWidth;

    public DetailsPaneController(
        FrameworkElement owner,
        FrameworkElement splitter,
        ColumnDefinition splitterColumn,
        ColumnDefinition primaryColumn,
        ColumnDefinition detailsColumn,
        FrameworkElement detailsPane,
        Button toggleButton,
        Action requestLayout)
    {
        _owner = owner;
        _splitter = splitter;
        _splitterColumn = splitterColumn;
        _primaryColumn = primaryColumn;
        _detailsColumn = detailsColumn;
        _detailsPane = detailsPane;
        _toggleButton = toggleButton;
        _requestLayout = requestLayout;
        _primaryMinWidth = primaryColumn.MinWidth;
        _detailsMinWidth = detailsColumn.MinWidth;
        _splitterWidth = splitter.Width > 0 ? splitter.Width : 8;

        _toggleButton.Click += OnToggleButtonClick;
        SplitterChrome.ApplyHorizontal(_splitter);
        _splitter.PointerPressed += OnSplitterPointerPressed;
        _splitter.PointerMoved += OnSplitterPointerMoved;
        _splitter.PointerReleased += OnSplitterPointerReleased;
        _splitter.PointerCaptureLost += OnSplitterPointerCaptureLost;
        _owner.Loaded += OnOwnerLoaded;
        _owner.SizeChanged += OnOwnerSizeChanged;

        UpdateToggleButton();
    }

    public void RefreshLayout()
    {
        if (!_isCollapsed && _detailsColumn.ActualWidth > 0)
        {
            _expandedDetailsWidth = _detailsColumn.ActualWidth;
            ConstrainExpandedColumns();
        }

        _requestLayout();
    }

    private void OnOwnerLoaded(object sender, RoutedEventArgs e)
    {
        if (_detailsColumn.ActualWidth > 0)
        {
            _expandedDetailsWidth = _detailsColumn.ActualWidth;
        }

        UpdateToggleButton();
    }

    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isCollapsed)
        {
            return;
        }

        ConstrainExpandedColumns();
    }

    private void OnToggleButtonClick(object sender, RoutedEventArgs e)
    {
        if (_isCollapsed)
        {
            ExpandDetails();
        }
        else
        {
            CollapseDetails();
        }
    }

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isCollapsed)
        {
            return;
        }

        _isResizing = true;
        _lastPointerX = e.GetCurrentPoint(_owner).Position.X;
        _splitter.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing || _isCollapsed)
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
        var availableWidth = GetAvailableWidth(includeSplitter: true);
        if (availableWidth <= 0)
        {
            return;
        }

        var primaryWidth = _primaryColumn.ActualWidth > 0 ? _primaryColumn.ActualWidth : availableWidth * 0.65;
        var newPrimaryWidth = Math.Max(_primaryMinWidth, primaryWidth + delta);
        var maxPrimaryWidth = Math.Max(_primaryMinWidth, availableWidth - _detailsMinWidth);
        newPrimaryWidth = Math.Min(newPrimaryWidth, maxPrimaryWidth);

        var newDetailsWidth = Math.Max(_detailsMinWidth, availableWidth - newPrimaryWidth);
        _primaryColumn.Width = new GridLength(newPrimaryWidth);
        _detailsColumn.Width = new GridLength(newDetailsWidth);
        _expandedDetailsWidth = newDetailsWidth;
        _requestLayout();
    }

    private void CollapseDetails()
    {
        if (_detailsColumn.ActualWidth > 0)
        {
            _expandedDetailsWidth = Math.Max(_detailsMinWidth, _detailsColumn.ActualWidth);
        }

        _primaryColumn.Width = new GridLength(1, GridUnitType.Star);
        _splitterColumn.Width = new GridLength(0);
        _splitter.Visibility = Visibility.Collapsed;
        _detailsColumn.MinWidth = 0;
        _detailsColumn.Width = new GridLength(0);
        _detailsPane.Visibility = Visibility.Collapsed;
        _isCollapsed = true;
        UpdateToggleButton();
        _requestLayout();
    }

    private void ExpandDetails()
    {
        _splitterColumn.Width = new GridLength(_splitterWidth);
        _splitter.Visibility = Visibility.Visible;
        _detailsPane.Visibility = Visibility.Visible;
        _detailsColumn.MinWidth = _detailsMinWidth;

        var availableWidth = GetAvailableWidth(includeSplitter: true);
        var detailsWidth = _expandedDetailsWidth > 0 ? _expandedDetailsWidth : Math.Max(_detailsMinWidth, availableWidth * 0.32);
        detailsWidth = Math.Min(detailsWidth, Math.Max(_detailsMinWidth, availableWidth - _primaryMinWidth));
        var primaryWidth = Math.Max(_primaryMinWidth, availableWidth - detailsWidth);

        _primaryColumn.Width = new GridLength(primaryWidth);
        _detailsColumn.Width = new GridLength(detailsWidth);
        _expandedDetailsWidth = detailsWidth;
        _isCollapsed = false;
        UpdateToggleButton();
        _requestLayout();
    }

    private void ConstrainExpandedColumns()
    {
        var availableWidth = GetAvailableWidth(includeSplitter: true);
        if (availableWidth <= 0)
        {
            return;
        }

        var detailsWidth = _detailsColumn.ActualWidth > 0 ? _detailsColumn.ActualWidth : _expandedDetailsWidth;
        if (detailsWidth <= 0)
        {
            return;
        }

        detailsWidth = Math.Max(_detailsMinWidth, Math.Min(detailsWidth, Math.Max(_detailsMinWidth, availableWidth - _primaryMinWidth)));
        var primaryWidth = Math.Max(_primaryMinWidth, availableWidth - detailsWidth);

        _primaryColumn.Width = new GridLength(primaryWidth);
        _detailsColumn.Width = new GridLength(detailsWidth);
        _expandedDetailsWidth = detailsWidth;
    }

    private void EndResize()
    {
        _isResizing = false;
        _splitter.ReleasePointerCaptures();
    }

    private double GetAvailableWidth(bool includeSplitter)
        => Math.Max(0, _owner.ActualWidth - (includeSplitter ? _splitterWidth : 0));

    private void UpdateToggleButton()
    {
        _toggleButton.Content = _isCollapsed ? "Show Details" : "Hide Details";
    }
}
