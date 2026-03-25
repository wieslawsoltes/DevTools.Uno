namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class MemoryInspectionItemViewModel
{
    public required string Name { get; init; }

    public required string Value { get; init; }

    public string Detail { get; init; } = string.Empty;

    public object? InspectionObject { get; init; }
}
