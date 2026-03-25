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

    public bool CanEditInline => PropertyInspector.CanConvertFromString(ValueType);

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
    {
        if (!CanEditInline || !PropertyInspector.TryConvertFromString(ValueType, value, out var converted))
        {
            return false;
        }

        try
        {
            OwnerDictionary[ResourceKey] = converted;
            UpdateValue(converted);
            _valueReplaced?.Invoke(this);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateValue(object? value)
    {
        _value = value;
        ValueType = value?.GetType() ?? ValueType;
        ValueText = PropertyInspector.FormatShallowValue(value);
        TypeText = value?.GetType().Name ?? "(null)";
    }
}
