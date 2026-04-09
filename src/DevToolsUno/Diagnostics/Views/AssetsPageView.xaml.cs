using DevToolsUno.Diagnostics.Internal;
using Microsoft.UI.Xaml.Controls;

namespace DevToolsUno.Diagnostics.Views;

public sealed partial class AssetsPageView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;
    private readonly PaneSplitterController _paneSplitter;
    private readonly RowSplitterController _previewSplitter;

    public AssetsPageView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, FolderTree, AssetsGrid, PreviewView);
        _paneSplitter = new PaneSplitterController(LayoutRoot, PaneSplitter, FolderColumn, ContentColumn, () => _layoutRefresh.Request());
        _previewSplitter = new RowSplitterController(this, PreviewSplitter, AssetsRow, PreviewRow, () => _layoutRefresh.Request());
    }

    internal void RequestLayoutRecovery()
    {
        _paneSplitter.RefreshLayout();
        _previewSplitter.RefreshLayout();
        _layoutRefresh.Request();
        PreviewView.RequestLayoutRecovery();
    }
}
