using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevTools.Uno.Diagnostics.Views;

public sealed partial class MemoryObjectDetailsView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;
    private readonly DetailsPaneController _detailsPane;

    public MemoryObjectDetailsView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, PropertyGrid, DetailsPanel);
        _detailsPane = new DetailsPaneController(this, DetailsSplitter, DetailsSplitterColumn, PropertyColumn, DetailsColumn, DetailsPanel, ToggleDetailsButton, () => _layoutRefresh.Request());
        DataContextChanged += OnDataContextChanged;
    }

    internal void RequestLayoutRecovery()
    {
        _layoutRefresh.Request();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        _detailsPane.RefreshLayout();
    }
}
