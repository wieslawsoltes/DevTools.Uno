using Windows.System;

namespace DevTools.Uno.Diagnostics;

public sealed class DevToolsHotKeyConfiguration
{
    public DevToolsHotKeyGesture InspectHoveredControl { get; init; } =
        DevToolsHotKeyGesture.ModifiersOnly(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

    public DevToolsHotKeyGesture TogglePopupFreeze { get; init; } =
        new(VirtualKey.F, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu);

    public DevToolsHotKeyGesture ScreenshotSelectedControl { get; init; } =
        new(VirtualKey.F8, VirtualKeyModifiers.None);
}
