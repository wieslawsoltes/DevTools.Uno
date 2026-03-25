using Microsoft.UI.Xaml;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class EventLogEntry
{
    private WeakReference<DependencyObject>? _sourceElementReference;

    public required long SequenceId { get; init; }

    public required DateTime Timestamp { get; init; }

    public required string EventName { get; init; }

    public required string EventText { get; init; }

    public required string Strategy { get; init; }

    public required string SourceDisplayName { get; init; }

    public required string SourceSelector { get; init; }

    public required string HandledText { get; init; }

    public required string RouteCountText { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyList<EventRouteEntryViewModel> RouteEntries { get; init; }

    public string Time => Timestamp.ToString("HH:mm:ss.fff");

    public DependencyObject? SourceElement
    {
        get
        {
            if (_sourceElementReference is null)
            {
                return null;
            }

            return _sourceElementReference.TryGetTarget(out var target) ? target : null;
        }
    }

    public static EventLogEntry Create(
        long sequenceId,
        DateTime timestamp,
        string eventName,
        string eventText,
        string strategy,
        DependencyObject? sourceElement,
        string sourceDisplayName,
        string sourceSelector,
        string handledText,
        string routeCountText,
        string summary,
        IReadOnlyList<EventRouteEntryViewModel> routeEntries)
        => new()
        {
            _sourceElementReference = sourceElement is not null ? new WeakReference<DependencyObject>(sourceElement) : null,
            SequenceId = sequenceId,
            Timestamp = timestamp,
            EventName = eventName,
            EventText = eventText,
            Strategy = strategy,
            SourceDisplayName = sourceDisplayName,
            SourceSelector = sourceSelector,
            HandledText = handledText,
            RouteCountText = routeCountText,
            Summary = summary,
            RouteEntries = routeEntries,
        };
}
