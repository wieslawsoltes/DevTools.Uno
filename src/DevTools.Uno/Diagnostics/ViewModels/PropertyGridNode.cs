using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class PropertyGridNode : ViewModelBase
{
    private string _valueText = string.Empty;
    private string _priorityText = string.Empty;
    private string _sourceText = string.Empty;
    private bool _isExpanded;

    public required string Name { get; init; }
    public string ValueText
    {
        get => _valueText;
        set => RaiseAndSetIfChanged(ref _valueText, value);
    }

    public required string TypeText { get; init; }
    public string PriorityText
    {
        get => _priorityText;
        set => RaiseAndSetIfChanged(ref _priorityText, value);
    }

    public string SourceText
    {
        get => _sourceText;
        set => RaiseAndSetIfChanged(ref _sourceText, value);
    }

    public bool IsGroup { get; init; }
    public bool IsAttachedProperty { get; init; }
    public bool IsPinned { get; set; }
    public bool IsEditable { get; init; }
    public string FullName { get; init; } = string.Empty;
    public ObservableCollection<PropertyGridNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public Func<string?, bool>? TrySetValue { get; init; }
    public Func<IReadOnlyList<PropertyValueSourceViewModel>>? GetSources { get; init; }
    public Func<object?>? GetRawValue { get; init; }
    public Action? NotifyValueChanged { get; set; }

    public bool ApplyValue(string? value)
    {
        if (TrySetValue is null)
        {
            return false;
        }

        var applied = TrySetValue(value);
        if (applied)
        {
            NotifyValueChanged?.Invoke();
        }

        return applied;
    }

    public IReadOnlyList<PropertyValueSourceViewModel> LoadSources()
        => GetSources?.Invoke() ?? [];

    public object? GetValue()
        => GetRawValue?.Invoke();
}
