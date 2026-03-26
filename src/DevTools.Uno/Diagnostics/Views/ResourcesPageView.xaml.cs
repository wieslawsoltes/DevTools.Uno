using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml.Controls;

namespace DevTools.Uno.Diagnostics.Views;

public sealed partial class ResourcesPageView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;
    private readonly PaneSplitterController _paneSplitter;
    private readonly TreeDataGridSelectionBringIntoViewController _providerSelectionBringIntoView;
    private readonly TreeDataGridSelectionBringIntoViewController _resourceSelectionBringIntoView;

    public ResourcesPageView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, ProviderTree, ResourcesGrid, DetailsView);
        _paneSplitter = new PaneSplitterController(LayoutRoot, PaneSplitter, ProviderColumn, ContentColumn, () => _layoutRefresh.Request());
        _providerSelectionBringIntoView = new TreeDataGridSelectionBringIntoViewController(
            this,
            ProviderTree,
            dataContext => (dataContext as ViewModels.ResourcesPageViewModel)?.ProviderSelection);
        _resourceSelectionBringIntoView = new TreeDataGridSelectionBringIntoViewController(
            this,
            ResourcesGrid,
            dataContext => (dataContext as ViewModels.ResourcesPageViewModel)?.ResourceSelection);
    }

    internal void RequestLayoutRecovery()
    {
        _paneSplitter.RefreshLayout();
        _layoutRefresh.Request();
        _providerSelectionBringIntoView.RequestBringIntoView();
        _resourceSelectionBringIntoView.RequestBringIntoView();
        DetailsView.RequestLayoutRecovery();
    }
}
