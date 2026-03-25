namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class MemoryTrackedObjectViewModel
{
    public required string TrackingId { get; init; }

    public required string Name { get; init; }

    public required string StatusText { get; init; }

    public required string AgeText { get; init; }

    public required string Summary { get; init; }

    public required string TypeText { get; init; }

    public required bool IsAlive { get; init; }

    public object? DetailsObject { get; init; }
}
