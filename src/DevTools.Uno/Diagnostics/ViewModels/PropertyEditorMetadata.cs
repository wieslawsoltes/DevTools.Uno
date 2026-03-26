namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class PropertyEditorMetadata
{
    public static PropertyEditorMetadata ReadOnly { get; } = new()
    {
        Kind = PropertyEditorKind.ReadOnly,
    };

    public required PropertyEditorKind Kind { get; init; }

    public string PlaceholderText { get; init; } = string.Empty;

    public bool SupportsNullValue { get; init; }

    public bool SupportsThreeState { get; init; }

    public IReadOnlyList<PropertyEditorOption> Options { get; init; } = [];
}
