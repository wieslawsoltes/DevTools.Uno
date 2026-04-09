namespace DevToolsUno.Diagnostics.ViewModels;

internal sealed class PropertyEditorOption
{
    public required string DisplayText { get; init; }

    public object? Value { get; init; }

    public bool IsNullOption { get; init; }
}
