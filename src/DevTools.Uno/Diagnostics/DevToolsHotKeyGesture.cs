using Windows.System;

namespace DevTools.Uno.Diagnostics;

public readonly record struct DevToolsHotKeyGesture(VirtualKey Key, VirtualKeyModifiers Modifiers)
{
    public static DevToolsHotKeyGesture ModifiersOnly(VirtualKeyModifiers modifiers)
        => new(VirtualKey.None, modifiers);
}
