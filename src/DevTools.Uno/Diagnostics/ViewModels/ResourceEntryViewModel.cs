using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class ResourceEntryViewModel : ViewModelBase
{
    private readonly Action<ResourceEntryViewModel>? _valueReplaced;
    private object? _value;
    private Type _valueType;
    private string _valueText;
    private string _typeText;
    private string? _validationError;

    public ResourceEntryViewModel(
        object resourceKey,
        object? value,
        ResourceDictionary ownerDictionary,
        string providerName,
        string providerKind,
        string providerPath,
        string dictionarySource,
        string resourceId,
        Action<ResourceEntryViewModel>? valueReplaced)
    {
        ResourceKey = resourceKey;
        KeyText = ResourceInspector.FormatKey(resourceKey);
        OwnerDictionary = ownerDictionary;
        ProviderName = providerName;
        ProviderKind = providerKind;
        ProviderPath = providerPath;
        DictionarySource = dictionarySource;
        ResourceId = resourceId;
        _valueReplaced = valueReplaced;

        _valueType = typeof(string);
        _valueText = string.Empty;
        _typeText = string.Empty;
        UpdateValue(value);
    }

    public object ResourceKey { get; }

    public string KeyText { get; }

    public string ProviderName { get; }

    public string ProviderKind { get; }

    public string ProviderPath { get; }

    public string DictionarySource { get; }

    public string ResourceId { get; }

    public ResourceDictionary OwnerDictionary { get; }

    public Type ValueType
    {
        get => _valueType;
        private set
        {
            if (RaiseAndSetIfChanged(ref _valueType, value))
            {
                RaisePropertyChanged(nameof(CanEditInline));
            }
        }
    }

    public string ValueText
    {
        get => _valueText;
        private set => RaiseAndSetIfChanged(ref _valueText, value);
    }

    public string TypeText
    {
        get => _typeText;
        private set => RaiseAndSetIfChanged(ref _typeText, value);
    }

    public PropertyEditorMetadata Editor => PropertyInspector.GetEditorMetadata(ValueType, _value, isEditable: true);

    public PropertyEditorKind EditorKind => Editor.Kind;

    public string PlaceholderText => Editor.PlaceholderText;

    public bool SupportsNullValue => Editor.SupportsNullValue;

    public bool SupportsThreeState => Editor.SupportsThreeState;

    public IReadOnlyList<PropertyEditorOption> EditorOptions => Editor.Options;

    public bool CanEditInline => EditorKind != PropertyEditorKind.ReadOnly;

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

    public object? RawValue => _value;

    public object? GetValue() => _value;

    public void RefreshFromOwner()
    {
        if (!OwnerDictionary.TryGetValue(ResourceKey, out var value, shouldCheckSystem: false))
        {
            value = null;
        }

        UpdateValue(value);
    }

    public bool ApplyValue(string? value)
        => ApplyValue((object?)value).Success;

    public PropertyEditorCommitResult ApplyValue(object? value)
    {
        if (!CanEditInline)
        {
            var readOnlyResult = PropertyEditorCommitResult.Failed("This resource value does not support inline editing.");
            ValidationError = readOnlyResult.ErrorMessage;
            return readOnlyResult;
        }

        if (!PropertyInspector.TryConvertValue(ValueType, value, out var converted, out var error))
        {
            var failedResult = PropertyEditorCommitResult.Failed(error);
            ValidationError = failedResult.ErrorMessage;
            return failedResult;
        }

        try
        {
            OwnerDictionary[ResourceKey] = converted;
            UpdateValue(converted);
            _valueReplaced?.Invoke(this);
            ValidationError = null;
            return PropertyEditorCommitResult.Applied();
        }
        catch (Exception ex)
        {
            var failedResult = PropertyEditorCommitResult.Failed(ex.GetBaseException().Message);
            ValidationError = failedResult.ErrorMessage;
            return failedResult;
        }
    }

    private void UpdateValue(object? value)
    {
        _value = value;
        ValueType = value?.GetType() ?? ValueType;
        ValueText = PropertyInspector.FormatShallowValue(value);
        TypeText = value?.GetType().Name ?? "(null)";
        RaisePropertyChanged(nameof(Editor));
        RaisePropertyChanged(nameof(EditorKind));
        RaisePropertyChanged(nameof(PlaceholderText));
        RaisePropertyChanged(nameof(SupportsNullValue));
        RaisePropertyChanged(nameof(SupportsThreeState));
        RaisePropertyChanged(nameof(EditorOptions));
        RaisePropertyChanged(nameof(CanEditInline));
        RaisePropertyChanged(nameof(RawValue));
    }
}
