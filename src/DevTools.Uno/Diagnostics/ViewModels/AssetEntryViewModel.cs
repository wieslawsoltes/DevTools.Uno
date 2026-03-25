namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class AssetEntryViewModel
{
    public required string Name { get; init; }

    public required string RelativePath { get; init; }

    public required string FolderPath { get; init; }

    public required string Type { get; init; }

    public required string Extension { get; init; }

    public required Uri AssetUri { get; init; }

    public ulong SizeBytes { get; init; }

    public string SizeText => FormatSize(SizeBytes);

    private static string FormatSize(ulong value)
    {
        const double kilo = 1024d;
        const double mega = kilo * 1024d;
        const double giga = mega * 1024d;

        return value switch
        {
            >= (ulong)giga => $"{value / giga:0.##} GB",
            >= (ulong)mega => $"{value / mega:0.##} MB",
            >= (ulong)kilo => $"{value / kilo:0.##} KB",
            _ => $"{value} B",
        };
    }
}
