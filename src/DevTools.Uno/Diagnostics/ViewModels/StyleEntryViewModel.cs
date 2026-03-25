namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class StyleEntryViewModel
{
    public required string EntryId { get; init; }

    public required string Name { get; init; }

    public required string ValueText { get; init; }

    public required string TypeText { get; init; }

    public required string OriginText { get; init; }

    public required string Kind { get; init; }

    public string Summary { get; init; } = string.Empty;

    public object? InspectionObject { get; init; }

    public object? RawValue { get; init; }

    public object? GetInspectionTarget() => InspectionObject ?? RawValue;
}
