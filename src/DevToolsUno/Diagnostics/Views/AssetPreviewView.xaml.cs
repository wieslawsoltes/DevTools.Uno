using Microsoft.UI.Xaml;
using DevToolsUno.Diagnostics.Internal;
using Microsoft.UI.Xaml.Controls;

namespace DevToolsUno.Diagnostics.Views;

public sealed partial class AssetPreviewView : UserControl
{
    private readonly DeferredLayoutRefresh _layoutRefresh;

    public AssetPreviewView()
    {
        InitializeComponent();
        _layoutRefresh = new DeferredLayoutRefresh(this, 6, PreviewScroll, FontPreviewScroll, FallbackPreviewScroll, ImagePreviewHost);
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    internal void RequestLayoutRecovery()
    {
        UpdateImageViewport();
        _layoutRefresh.Request();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateImageViewport();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateImageViewport();
    }

    private void UpdateImageViewport()
    {
        var availableWidth = Math.Max(0, ImagePreviewHost.ActualWidth - ImagePreviewHost.Padding.Left - ImagePreviewHost.Padding.Right);
        var availableHeight = Math.Max(0, ImagePreviewHost.ActualHeight - ImagePreviewHost.Padding.Top - ImagePreviewHost.Padding.Bottom);

        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return;
        }

        PreviewImage.MaxWidth = availableWidth;
        PreviewImage.MaxHeight = availableHeight;
    }
}
