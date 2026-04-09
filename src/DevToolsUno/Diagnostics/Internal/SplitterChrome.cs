using System.Reflection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace DevToolsUno.Diagnostics.Internal;

internal static class SplitterChrome
{
    private static readonly InputCursor HorizontalCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    private static readonly InputCursor VerticalCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    private static readonly MethodInfo? SetProtectedCursorMethod =
        typeof(UIElement).GetMethod("SetProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void ApplyHorizontal(FrameworkElement splitter)
        => Apply(splitter, HorizontalCursor);

    public static void ApplyVertical(FrameworkElement splitter)
        => Apply(splitter, VerticalCursor);

    private static void Apply(FrameworkElement splitter, InputCursor cursor)
    {
        splitter.Loaded += OnLoaded;
        TrySetCursor(splitter, cursor);
        return;

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            TrySetCursor(splitter, cursor);
        }
    }

    private static void TrySetCursor(UIElement element, InputCursor cursor)
    {
        try
        {
            SetProtectedCursorMethod?.Invoke(element, [cursor]);
        }
        catch
        {
            // Best-effort only. Some heads do not expose the full cursor pipeline.
        }
    }
}
