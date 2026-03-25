using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml.Controls;

namespace DevTools.Uno.Diagnostics.Views;

public sealed partial class BindingsPageView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;

    public BindingsPageView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, BindingsGrid, DetailList, DetailsView);
    }

    internal void RequestLayoutRecovery()
    {
        _layoutRefresh.Request();
        DetailsView.RequestLayoutRecovery();
    }
}
