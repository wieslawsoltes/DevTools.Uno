namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class HotKeyEntry
{
    public required string Element { get; init; }
    public required string Gesture { get; init; }
    public required string Scope { get; init; }
}
