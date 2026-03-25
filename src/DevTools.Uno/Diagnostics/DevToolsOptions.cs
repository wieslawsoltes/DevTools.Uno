using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;

namespace DevTools.Uno.Diagnostics;

public sealed class DevToolsOptions
{
    public VirtualKey Gesture { get; set; } = VirtualKey.F12;

    public VirtualKeyModifiers GestureModifiers { get; set; } = VirtualKeyModifiers.None;

    public DevToolsViewKind LaunchView { get; set; } = DevToolsViewKind.LogicalTree;

    public Size Size { get; set; } = new(1400, 900);

    public bool ShowAsChildWindow { get; set; } = true;

    public Brush? FocusHighlighterBrush { get; set; } = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38));

    public Brush? SelectionHighlighterBrush { get; set; } = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 14, 116, 144));

    public Brush? InspectionHighlighterBrush { get; set; } = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));

    public StorageFolder? ScreenshotFolder { get; set; }

    public bool EnablePointerInspection { get; set; } = true;

    public bool EnableFocusTracking { get; set; } = true;
}
