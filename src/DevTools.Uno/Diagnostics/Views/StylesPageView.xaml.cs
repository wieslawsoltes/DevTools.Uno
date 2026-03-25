using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml.Controls;

namespace DevTools.Uno.Diagnostics.Views;

public sealed partial class StylesPageView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;
    private readonly PaneSplitterController _paneSplitter;

    public StylesPageView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, ScopeTree, EntriesGrid, DetailsView);
        _paneSplitter = new PaneSplitterController(LayoutRoot, PaneSplitter, ScopeColumn, ContentColumn, () => _layoutRefresh.Request());
    }

    internal void RequestLayoutRecovery()
    {
        _paneSplitter.RefreshLayout();
        _layoutRefresh.Request();
        DetailsView.RequestLayoutRecovery();
    }
}
