using DevToolsUno.Diagnostics.Internal;
using Microsoft.UI.Xaml.Controls;

namespace DevToolsUno.Diagnostics.Views;

public sealed partial class StylesPageView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;
    private readonly PaneSplitterController _paneSplitter;
    private readonly TreeDataGridSelectionBringIntoViewController _scopeSelectionBringIntoView;
    private readonly TreeDataGridSelectionBringIntoViewController _entrySelectionBringIntoView;

    public StylesPageView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, ScopeTree, EntriesGrid, DetailsView);
        _paneSplitter = new PaneSplitterController(LayoutRoot, PaneSplitter, ScopeColumn, ContentColumn, () => _layoutRefresh.Request());
        _scopeSelectionBringIntoView = new TreeDataGridSelectionBringIntoViewController(
            this,
            ScopeTree,
            dataContext => (dataContext as ViewModels.StylesPageViewModel)?.ScopeSelection);
        _entrySelectionBringIntoView = new TreeDataGridSelectionBringIntoViewController(
            this,
            EntriesGrid,
            dataContext => (dataContext as ViewModels.StylesPageViewModel)?.EntrySelection);
    }

    internal void RequestLayoutRecovery()
    {
        _paneSplitter.RefreshLayout();
        _layoutRefresh.Request();
        _scopeSelectionBringIntoView.RequestBringIntoView();
        _entrySelectionBringIntoView.RequestBringIntoView();
        DetailsView.RequestLayoutRecovery();
    }
}
