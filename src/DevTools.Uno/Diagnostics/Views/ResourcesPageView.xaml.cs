using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml.Controls;

namespace DevTools.Uno.Diagnostics.Views;

public sealed partial class ResourcesPageView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;
    private readonly PaneSplitterController _paneSplitter;

    public ResourcesPageView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, ProviderTree, ResourcesGrid, DetailsView);
        _paneSplitter = new PaneSplitterController(LayoutRoot, PaneSplitter, ProviderColumn, ContentColumn, () => _layoutRefresh.Request());
    }

    internal void RequestLayoutRecovery()
    {
        _paneSplitter.RefreshLayout();
        _layoutRefresh.Request();
        DetailsView.RequestLayoutRecovery();
    }
}
