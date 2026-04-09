using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;

namespace DevToolsUno.Diagnostics.ViewModels;

internal sealed class PropertyGridNode : ViewModelBase
{
    private string _valueText = string.Empty;
    private string _priorityText = string.Empty;
    private string _sourceText = string.Empty;
    private bool _isExpanded;
    private string? _validationError;

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
    public PropertyEditorMetadata Editor { get; init; } = PropertyEditorMetadata.ReadOnly;
    public ObservableCollection<PropertyGridNode> Children { get; } = [];

    public PropertyEditorKind EditorKind => Editor.Kind;

    public string PlaceholderText => Editor.PlaceholderText;

    public bool SupportsNullValue => Editor.SupportsNullValue;

    public bool SupportsThreeState => Editor.SupportsThreeState;

    public IReadOnlyList<PropertyEditorOption> EditorOptions => Editor.Options;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public string? ValidationError
    {
        get => _validationError;
        private set
        {
            if (RaiseAndSetIfChanged(ref _validationError, value))
            {
                RaisePropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    public Func<object?, PropertyEditorCommitResult>? TrySetValue { get; init; }
    public Func<IReadOnlyList<PropertyValueSourceViewModel>>? GetSources { get; init; }
    public Func<object?>? GetRawValue { get; init; }
    public Action? NotifyValueChanged { get; set; }

    public object? RawValue => GetRawValue?.Invoke();

    public bool ApplyValue(string? value)
        => CommitValue(value).Success;

    public bool ApplyTypedValue(object? value)
        => CommitValue(value).Success;

    public PropertyEditorCommitResult CommitValue(object? value)
    {
        if (TrySetValue is null)
        {
            var readOnlyResult = PropertyEditorCommitResult.Failed("This property is read-only.");
            ValidationError = readOnlyResult.ErrorMessage;
            return readOnlyResult;
        }

        var result = TrySetValue(value);
        ValidationError = result.Success ? null : result.ErrorMessage;

        if (result.Success)
        {
            RaisePropertyChanged(nameof(RawValue));
            NotifyValueChanged?.Invoke();
        }

        return result;
    }

    public void ClearValidation()
        => ValidationError = null;

    public IReadOnlyList<PropertyValueSourceViewModel> LoadSources()
        => GetSources?.Invoke() ?? [];

    public object? GetValue()
        => GetRawValue?.Invoke();
}
