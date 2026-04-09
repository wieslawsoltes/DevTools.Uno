using DevToolsUno.Diagnostics.Internal;

namespace DevToolsUno.Diagnostics.ViewModels;

internal sealed class MemorySampleViewModel
{
    public required string SampleId { get; init; }

    public required string TimestampText { get; init; }

    public required string ManagedHeapText { get; init; }

    public required string HeapDeltaText { get; init; }

    public required string TotalAllocatedText { get; init; }

    public required string WorkingSetText { get; init; }

    public required string PrivateMemoryText { get; init; }

    public required string GcCollectionsText { get; init; }

    public required string Summary { get; init; }

    public required MemorySnapshot Snapshot { get; init; }
}
