using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class ControlLayoutViewModel : ViewModelBase
{
    private string _actualSize = string.Empty;
    private string _layoutSlot = string.Empty;
    private string _margin = string.Empty;
    private string _padding = string.Empty;
    private string _alignment = string.Empty;
    private string _visibility = string.Empty;
    private string _opacity = string.Empty;

    public string ActualSize
    {
        get => _actualSize;
        private set => RaiseAndSetIfChanged(ref _actualSize, value);
    }

    public string LayoutSlot
    {
        get => _layoutSlot;
        private set => RaiseAndSetIfChanged(ref _layoutSlot, value);
    }

    public string Margin
    {
        get => _margin;
        private set => RaiseAndSetIfChanged(ref _margin, value);
    }

    public string Padding
    {
        get => _padding;
        private set => RaiseAndSetIfChanged(ref _padding, value);
    }

    public string Alignment
    {
        get => _alignment;
        private set => RaiseAndSetIfChanged(ref _alignment, value);
    }

    public string Visibility
    {
        get => _visibility;
        private set => RaiseAndSetIfChanged(ref _visibility, value);
    }

    public string Opacity
    {
        get => _opacity;
        private set => RaiseAndSetIfChanged(ref _opacity, value);
    }

    public void Update(DependencyObject? element)
    {
        if (element is not FrameworkElement fe)
        {
            ActualSize = string.Empty;
            LayoutSlot = string.Empty;
            Margin = string.Empty;
            Padding = string.Empty;
            Alignment = string.Empty;
            Visibility = string.Empty;
            Opacity = string.Empty;
            return;
        }

        var slot = LayoutInformation.GetLayoutSlot(fe);
        ActualSize = $"{fe.ActualWidth:0.#} x {fe.ActualHeight:0.#}";
        LayoutSlot = $"{slot.X:0.#}, {slot.Y:0.#}, {slot.Width:0.#}, {slot.Height:0.#}";
        Margin = fe.Margin.ToString();
        Padding = fe is Control control ? control.Padding.ToString() : string.Empty;
        Alignment = $"{fe.HorizontalAlignment} / {fe.VerticalAlignment}";
        Visibility = fe.Visibility.ToString();
        Opacity = fe.Opacity.ToString("0.###");
    }
}
