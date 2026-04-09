using System.Collections.ObjectModel;

namespace DevToolsUno.Diagnostics.ViewModels;

internal enum StyleScopeCategory
{
    Overview,
    Style,
    ImplicitStyleScopes,
    Template,
    TemplateRoot,
    VisualStates,
    VisualStateGroup,
    VisualState,
    TemplatedParent,
    DefaultStyleKey,
}

internal sealed class StyleScopeNode : ViewModelBase
{
    private bool _isExpanded = true;

    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required StyleScopeCategory Category { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public object? InspectionObject { get; init; }

    public bool IncludeBasedOnSetters { get; init; }

    public StyleScopeNode? Parent { get; private set; }

    public ObservableCollection<StyleScopeNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public void AddChild(StyleScopeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}
