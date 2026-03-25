using Microsoft.UI.Xaml;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class EventListenerDefinition
{
    public required string Id { get; init; }

    public required string GroupName { get; init; }

    public required string DisplayName { get; init; }

    public required string Strategy { get; init; }

    public required string Description { get; init; }

    public required string SummaryText { get; init; }

    public required bool IsDefaultEnabled { get; init; }

    public required Func<UIElement, Action<UIElement, RoutedEventArgs>, IDisposable?> TryAttach { get; init; }

    public required Func<RoutedEventArgs, string?> FormatDetail { get; init; }

    public bool IsEnabled { get; set; }
}
