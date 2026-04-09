using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;

namespace DevToolsUno.Diagnostics.ViewModels;

internal sealed class ResourceProviderNode : ViewModelBase
{
    private bool _isExpanded = true;

    public required string Name { get; init; }

    public required string Kind { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public ResourceDictionary? Dictionary { get; init; }

    public object? Provider { get; init; }

    public ResourceProviderNode? Parent { get; private set; }

    public ObservableCollection<ResourceProviderNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public void AddChild(ResourceProviderNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}
