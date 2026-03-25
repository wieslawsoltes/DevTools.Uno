using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml.Controls;

namespace DevTools.Uno.Diagnostics.Views;

public sealed partial class MemoryPageView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;

    public MemoryPageView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, SamplesGrid, DetailList, TrackedGrid, DetailsView);
    }

    internal void RequestLayoutRecovery()
    {
        _layoutRefresh.Request();
        DetailsView.RequestLayoutRecovery();
    }
}
