using System.Collections.ObjectModel;

namespace DevToolsUno.Diagnostics.ViewModels;

internal sealed class AssetFolderNode : ViewModelBase
{
    private bool _isExpanded = true;
    private string _summary = string.Empty;

    public required string Name { get; init; }

    public required string RelativePath { get; init; }

    public AssetFolderNode? Parent { get; private set; }

    public ObservableCollection<AssetFolderNode> Children { get; } = [];

    public List<AssetEntryViewModel> DirectAssets { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public int TotalAssetCount { get; private set; }

    public string Summary
    {
        get => _summary;
        private set => RaiseAndSetIfChanged(ref _summary, value);
    }

    public void AddChild(AssetFolderNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void UpdateCountsRecursive()
    {
        var total = DirectAssets.Count;
        foreach (var child in Children)
        {
            child.UpdateCountsRecursive();
            total += child.TotalAssetCount;
        }

        TotalAssetCount = total;
        Summary = DirectAssets.Count == total
            ? $"{total} assets"
            : $"{DirectAssets.Count} direct, {total} total";
    }
}
