using System.Collections.ObjectModel;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class EventListenerNode : ViewModelBase
{
    private readonly Action _selectionChanged;
    private bool _isExpanded = true;
    private bool? _isChecked;

    public EventListenerNode(string name, EventListenerDefinition? definition, EventListenerNode? parent, Action selectionChanged)
    {
        Name = name;
        Definition = definition;
        Parent = parent;
        _selectionChanged = selectionChanged;
        _isChecked = definition?.IsEnabled ?? false;
    }

    public string Name { get; }

    public EventListenerDefinition? Definition { get; }

    public EventListenerNode? Parent { get; }

    public ObservableCollection<EventListenerNode> Children { get; } = [];

    public bool IsLeaf => Definition is not null;

    public string Summary => IsLeaf
        ? Definition!.SummaryText
        : $"{EnabledLeafCount} of {LeafCount} enabled";

    public string Description => IsLeaf
        ? Definition!.Description
        : $"{Children.Count} listeners";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetCheckedCore(value == true, notifySelectionChanged: true);
    }

    private int LeafCount => IsLeaf ? 1 : Children.Sum(x => x.LeafCount);

    private int EnabledLeafCount => IsLeaf ? (Definition!.IsEnabled ? 1 : 0) : Children.Sum(x => x.EnabledLeafCount);

    internal void AddChild(EventListenerNode child)
    {
        Children.Add(child);
        RefreshFromChildren();
    }

    internal void SetCheckedFromOwner(bool isEnabled)
    {
        SetCheckedCore(isEnabled, notifySelectionChanged: false);
    }

    internal void RefreshFromChildrenRecursive()
    {
        foreach (var child in Children)
        {
            child.RefreshFromChildrenRecursive();
        }

        RefreshFromChildren();
    }

    private void SetCheckedCore(bool isEnabled, bool notifySelectionChanged)
    {
        if (IsLeaf)
        {
            Definition!.IsEnabled = isEnabled;
            RaiseAndSetIfChanged(ref _isChecked, isEnabled);
            RaisePropertyChanged(nameof(Summary));
            Parent?.RefreshFromChildren();
        }
        else
        {
            foreach (var child in Children)
            {
                child.SetCheckedCore(isEnabled, notifySelectionChanged: false);
            }

            RefreshFromChildren();
        }

        if (notifySelectionChanged)
        {
            _selectionChanged();
        }
    }

    private void RefreshFromChildren()
    {
        if (IsLeaf)
        {
            var isEnabled = Definition!.IsEnabled;
            RaiseAndSetIfChanged(ref _isChecked, isEnabled);
            RaisePropertyChanged(nameof(Summary));
            return;
        }

        var anyEnabled = false;
        var anyDisabled = false;

        foreach (var child in Children)
        {
            if (child.IsChecked == true)
            {
                anyEnabled = true;
            }
            else if (child.IsChecked == false)
            {
                anyDisabled = true;
            }
            else
            {
                anyEnabled = true;
                anyDisabled = true;
            }
        }

        bool? groupValue = anyEnabled && anyDisabled ? null : anyEnabled;
        RaiseAndSetIfChanged(ref _isChecked, groupValue);
        RaisePropertyChanged(nameof(Summary));
        Parent?.RefreshFromChildren();
    }
}
