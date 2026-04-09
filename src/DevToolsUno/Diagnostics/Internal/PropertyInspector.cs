using System.ComponentModel;
using System.Collections;
using System.Globalization;
using System.Reflection;
using DevToolsUno.Diagnostics.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace DevToolsUno.Diagnostics.Internal;

internal static class PropertyInspector
{
    private static readonly BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public;
    private static readonly BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    private static readonly PropertyInfo? DependencyPropertyNameProperty =
        typeof(DependencyProperty).GetProperty("Name", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly PropertyInfo? DependencyPropertyTypeProperty =
        typeof(DependencyProperty).GetProperty("Type", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly PropertyInfo? DependencyPropertyOwnerTypeProperty =
        typeof(DependencyProperty).GetProperty("OwnerType", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly PropertyInfo? DependencyPropertyIsAttachedProperty =
        typeof(DependencyProperty).GetProperty("IsAttached", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly Type? UnoDependencyPropertyHelperType =
        typeof(DependencyObjectStore).Assembly.GetType("Uno.UI.Xaml.Core.DependencyPropertyHelper");

    private static readonly MethodInfo? DependencyPropertyHelperTryGetPropertiesForTypeMethod =
        UnoDependencyPropertyHelperType?.GetMethod("TryGetDependencyPropertiesForType", BindingFlags.Public | BindingFlags.Static);

    private static readonly MethodInfo? StoreGetBaseValueMethod =
        typeof(DependencyObjectStore).GetMethod("GetBaseValue", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(DependencyProperty)]);

    private static readonly MethodInfo? StoreGetCurrentHighestValuePrecedenceMethod =
        typeof(DependencyObjectStore).GetMethod("GetCurrentHighestValuePrecedence", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(DependencyProperty)]);

    private static readonly MethodInfo? StoreGetResourceBindingsForPropertyMethod =
        typeof(DependencyObjectStore).GetMethod("GetResourceBindingsForProperty", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(DependencyProperty)]);

    private static readonly FieldInfo? StorePropertiesField =
        typeof(DependencyObjectStore).GetField("_properties", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo? PropertyDetailsCollectionGetAllDetailsMethod =
        StorePropertiesField?.FieldType.GetMethod("GetAllDetails", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly PropertyInfo? DependencyPropertyDetailsPropertyProperty =
        typeof(DependencyObjectStore).Assembly
            .GetType("Microsoft.UI.Xaml.DependencyPropertyDetails")
            ?.GetProperty("Property", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static IReadOnlyList<PropertyGridNode> BuildPropertyTree(
        object element,
        ISet<string> pinnedProperties,
        bool includeClrProperties,
        FilterViewModel filter)
    {
        var groups = new List<PropertyGridNode>();
        var dependencyGroup = CreateGroup("Dependency Properties");
        var attachedGroup = CreateGroup("Attached Properties");
        var clrGroup = CreateGroup("CLR Properties");
        var pinnedGroup = CreateGroup("Pinned");

        if (element is DependencyObject dependencyObject)
        {
            foreach (var node in GetDependencyProperties(dependencyObject))
            {
                if (!MatchesFilter(filter, node))
                {
                    continue;
                }

                node.IsPinned = pinnedProperties.Contains(node.FullName);
                if (node.IsPinned)
                {
                    pinnedGroup.Children.Add(node);
                }

                if (node.IsAttachedProperty)
                {
                    attachedGroup.Children.Add(node);
                }
                else
                {
                    dependencyGroup.Children.Add(node);
                }
            }
        }

        if (includeClrProperties)
        {
            foreach (var node in GetClrProperties(element))
            {
                if (!MatchesFilter(filter, node))
                {
                    continue;
                }

                node.IsPinned = pinnedProperties.Contains(node.FullName);
                if (node.IsPinned)
                {
                    pinnedGroup.Children.Add(node);
                }

                clrGroup.Children.Add(node);
            }
        }

        if (pinnedGroup.Children.Count > 0)
        {
            groups.Add(pinnedGroup);
        }

        if (dependencyGroup.Children.Count > 0)
        {
            groups.Add(dependencyGroup);
        }

        if (attachedGroup.Children.Count > 0)
        {
            groups.Add(attachedGroup);
        }

        if (clrGroup.Children.Count > 0)
        {
            groups.Add(clrGroup);
        }

        return groups;
    }

    private static bool MatchesFilter(FilterViewModel filter, PropertyGridNode node)
        => filter.Filter(node.Name) ||
           filter.Filter(node.ValueText) ||
           filter.Filter(node.TypeText) ||
           filter.Filter(node.PriorityText) ||
           filter.Filter(node.SourceText) ||
           filter.Filter(node.FullName);

    public static async Task CopyTextAsync(string text)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }
        catch
        {
        }

        await Task.CompletedTask;
    }

    private static PropertyGridNode CreateGroup(string name) => new()
    {
        Name = name,
        ValueText = string.Empty,
        TypeText = string.Empty,
        PriorityText = string.Empty,
        SourceText = string.Empty,
        IsGroup = true,
        IsEditable = false,
        IsExpanded = true,
    };

    private static IEnumerable<PropertyGridNode> GetDependencyProperties(DependencyObject element)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in EnumerateRegisteredDependencyProperties(element.GetType()))
        {
            if (!seen.Add(GetDependencyPropertyKey(property)))
            {
                continue;
            }

            yield return CreateDependencyPropertyNode(
                element,
                property,
                GetDependencyPropertyName(property) ?? property.ToString() ?? nameof(DependencyProperty));
        }

        foreach (var property in EnumerateActiveAttachedProperties(element))
        {
            if (!seen.Add(GetDependencyPropertyKey(property)))
            {
                continue;
            }

            yield return CreateDependencyPropertyNode(
                element,
                property,
                GetDependencyPropertyName(property) ?? property.ToString() ?? nameof(DependencyProperty));
        }
    }

    private static IEnumerable<PropertyGridNode> GetClrProperties(object element)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.GetType().GetProperties(AllInstance))
        {
            if (property.GetIndexParameters().Length > 0 || !seen.Add(property.Name))
            {
                continue;
            }

            PropertyGridNode? node = null;
            node = new PropertyGridNode
            {
                Name = property.Name,
                ValueText = FormatValue(SafeRead(() => property.GetValue(element))),
                TypeText = property.PropertyType.Name,
                PriorityText = "CLR",
                SourceText = property.DeclaringType?.Name ?? string.Empty,
                IsGroup = false,
                IsEditable = property.CanWrite,
                FullName = $"CLR:{property.DeclaringType?.FullName}.{property.Name}",
                Editor = GetEditorMetadata(property.PropertyType, SafeRead(() => property.GetValue(element)), property.CanWrite),
                TrySetValue = value =>
                {
                    var result = TrySetClrValue(element, property, value);
                    if (!result.Success)
                    {
                        return result;
                    }

                    node!.ValueText = FormatValue(SafeRead(() => property.GetValue(element)));
                    return result;
                },
                GetSources = () =>
                [
                    new PropertyValueSourceViewModel
                    {
                        Name = "CLR",
                        Value = FormatValue(SafeRead(() => property.GetValue(element))),
                        Detail = property.DeclaringType?.FullName ?? string.Empty,
                        IsActive = true,
                    },
                ],
                GetRawValue = () => SafeRead(() => property.GetValue(element)),
            };

            yield return node;
        }
    }

    private static PropertyGridNode CreateDependencyPropertyNode(DependencyObject element, DependencyProperty property, string name)
    {
        var propertyType = GetDependencyPropertyType(property) ?? typeof(object);
        var ownerType = GetDependencyPropertyOwnerType(property) ?? element.GetType();
        var isAttached = GetDependencyPropertyIsAttached(property);
        PropertyGridNode? node = null;

        node = new PropertyGridNode
        {
            Name = isAttached ? $"[{ownerType.Name}.{name}]" : name,
            ValueText = FormatValue(SafeRead(() => element.GetValue(property))),
            TypeText = propertyType.Name,
            PriorityText = GetCurrentPrecedence(element, property),
            SourceText = ownerType.Name,
            IsGroup = false,
            IsAttachedProperty = isAttached,
            IsEditable = true,
            FullName = $"DP:{ownerType.FullName}.{name}",
            Editor = GetEditorMetadata(propertyType, SafeRead(() => element.GetValue(property)), isEditable: true),
            TrySetValue = value =>
            {
                var result = TrySetDependencyValue(element, property, propertyType, value);
                if (!result.Success)
                {
                    return result;
                }

                node!.ValueText = FormatValue(SafeRead(() => element.GetValue(property)));
                node.PriorityText = GetCurrentPrecedence(element, property);
                return result;
            },
            GetSources = () => GetDependencySources(element, property, propertyType),
            GetRawValue = () => SafeRead(() => element.GetValue(property)),
        };

        return node;
    }

    private static IEnumerable<DependencyProperty> EnumerateRegisteredDependencyProperties(Type type)
    {
        if (DependencyPropertyHelperTryGetPropertiesForTypeMethod is not null)
        {
            var args = new object?[] { type, null };
            if (DependencyPropertyHelperTryGetPropertiesForTypeMethod.Invoke(null, args) is true &&
                args[1] is IEnumerable properties)
            {
                foreach (var property in properties.OfType<DependencyProperty>())
                {
                    yield return property;
                }

                yield break;
            }
        }

        foreach (var field in type.GetFields(AllStatic))
        {
            if (field.Name.EndsWith("Property", StringComparison.Ordinal) &&
                field.GetValue(null) is DependencyProperty dependencyProperty)
            {
                yield return dependencyProperty;
            }
        }
    }

    private static IEnumerable<DependencyProperty> EnumerateActiveAttachedProperties(DependencyObject element)
    {
        if (element is not IDependencyObjectStoreProvider { Store: { } store } ||
            StorePropertiesField?.GetValue(store) is not object propertyDetailsCollection ||
            PropertyDetailsCollectionGetAllDetailsMethod?.Invoke(propertyDetailsCollection, []) is not IEnumerable details)
        {
            yield break;
        }

        foreach (var detail in details)
        {
            if (detail is null ||
                DependencyPropertyDetailsPropertyProperty?.GetValue(detail) is not DependencyProperty property ||
                !GetDependencyPropertyIsAttached(property))
            {
                continue;
            }

            yield return property;
        }
    }

    private static IReadOnlyList<PropertyValueSourceViewModel> GetDependencySources(
        DependencyObject element,
        DependencyProperty property,
        Type propertyType)
    {
        var result = new List<PropertyValueSourceViewModel>();
        var store = (element as IDependencyObjectStoreProvider)?.Store;

        var currentValue = SafeRead(() => element.GetValue(property));
        var localValue = SafeRead(() => element.ReadLocalValue(property));
        var precedence = GetCurrentPrecedence(element, property);
        var metadata = SafeRead(() => property.GetMetadata(element.GetType()));
        var bindingExpression = SafeRead(() => store?.GetBindingExpression(property));

        result.Add(new PropertyValueSourceViewModel
        {
            Name = "Current",
            Value = FormatValue(currentValue),
            Detail = precedence,
            IsActive = true,
        });

        if (localValue != DependencyProperty.UnsetValue && localValue is not null)
        {
            result.Add(new PropertyValueSourceViewModel
            {
                Name = "Local",
                Value = FormatValue(localValue),
                Detail = "ReadLocalValue",
                IsActive = true,
            });
        }

        if (metadata is not null)
        {
            result.Add(new PropertyValueSourceViewModel
            {
                Name = "Default",
                Value = FormatValue(metadata.DefaultValue),
                Detail = DescribeDefaultMetadata(metadata),
                IsActive = IsDefaultValuePrecedence(precedence),
            });

            if (TryCreateMetadataSource(metadata, precedence, out var metadataSource))
            {
                result.Add(metadataSource);
            }

            if (TryCreateInheritanceSource(metadata, currentValue, precedence, out var inheritanceSource))
            {
                result.Add(inheritanceSource);
            }
        }

        if (store is not null && StoreGetBaseValueMethod?.Invoke(store, [property]) is ValueTuple<object?, object> tuple)
        {
            result.Add(new PropertyValueSourceViewModel
            {
                Name = "Base",
                Value = FormatValue(tuple.Item1),
                Detail = tuple.Item2?.ToString() ?? string.Empty,
                IsActive = tuple.Item1 is not null,
            });
        }

        if (bindingExpression is not null)
        {
            var binding = bindingExpression.ParentBinding;
            result.Add(new PropertyValueSourceViewModel
            {
                Name = binding.RelativeSource?.Mode == RelativeSourceMode.TemplatedParent ? "TemplateBinding" : "Binding",
                Value = DescribeBindingValue(binding),
                Detail = DescribeBindingDetail(bindingExpression),
                IsActive = true,
            });
        }

        if (store is not null && StoreGetResourceBindingsForPropertyMethod?.Invoke(store, [property]) is System.Collections.IEnumerable resourceBindings)
        {
            foreach (var binding in resourceBindings.Cast<object>())
            {
                result.Add(new PropertyValueSourceViewModel
                {
                    Name = "Resource",
                    Value = binding.ToString() ?? string.Empty,
                    Detail = propertyType.Name,
                    IsActive = true,
                });
            }
        }

        return result;
    }

    private static bool TryCreateMetadataSource(PropertyMetadata metadata, string precedence, out PropertyValueSourceViewModel source)
    {
        var flags = new List<string>();

        if (metadata is FrameworkPropertyMetadata frameworkMetadata)
        {
            if (frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.Inherits))
            {
                flags.Add("Inherits");
            }

            if (frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.AffectsMeasure))
            {
                flags.Add("AffectsMeasure");
            }

            if (frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.AffectsArrange))
            {
                flags.Add("AffectsArrange");
            }

            if (frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.AffectsRender))
            {
                flags.Add("AffectsRender");
            }

            if (frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.AutoConvert))
            {
                flags.Add("AutoConvert");
            }

            if (frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.ValueInheritsDataContext))
            {
                flags.Add("ValueInheritsDataContext");
            }

            if (frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.ValueDoesNotInheritDataContext))
            {
                flags.Add("ValueDoesNotInheritDataContext");
            }

            if (frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.WeakStorage))
            {
                flags.Add("WeakStorage");
            }
        }

        if (metadata.PropertyChangedCallback is not null)
        {
            flags.Add("ChangedCallback");
        }

        if (metadata.CreateDefaultValueCallback is not null)
        {
            flags.Add("DefaultFactory");
        }

        if (flags.Count == 0)
        {
            source = default!;
            return false;
        }

        source = new PropertyValueSourceViewModel
        {
            Name = "Metadata",
            Value = string.Join(" | ", flags),
            Detail = metadata.GetType().Name,
            IsActive = IsDefaultValuePrecedence(precedence),
        };
        return true;
    }

    private static bool TryCreateInheritanceSource(PropertyMetadata metadata, object? currentValue, string precedence, out PropertyValueSourceViewModel source)
    {
        if (metadata is not FrameworkPropertyMetadata frameworkMetadata ||
            !frameworkMetadata.Options.HasFlag(FrameworkPropertyMetadataOptions.Inherits))
        {
            source = default!;
            return false;
        }

        source = new PropertyValueSourceViewModel
        {
            Name = "Inheritance",
            Value = FormatValue(currentValue),
            Detail = "FrameworkPropertyMetadataOptions.Inherits",
            IsActive = precedence.Contains("Inheritance", StringComparison.OrdinalIgnoreCase),
        };
        return true;
    }

    private static bool IsDefaultValuePrecedence(string precedence)
        => precedence.Contains("Default", StringComparison.OrdinalIgnoreCase);

    private static string DescribeDefaultMetadata(PropertyMetadata metadata)
    {
        var details = new List<string>
        {
            metadata.GetType().Name,
        };

        if (metadata.CreateDefaultValueCallback is not null)
        {
            details.Add("Factory default");
        }

        return string.Join(" | ", details);
    }

    private static string DescribeBindingValue(Binding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.Path?.Path))
        {
            return binding.Path.Path;
        }

        if (!string.IsNullOrWhiteSpace(binding.ElementName?.ToString()))
        {
            return $"Element:{binding.ElementName}";
        }

        if (binding.Source is not null)
        {
            return binding.Source.GetType().Name;
        }

        if (binding.RelativeSource is not null)
        {
            return $"Relative:{binding.RelativeSource.Mode}";
        }

        return binding.GetType().Name;
    }

    private static string DescribeBindingDetail(BindingExpression bindingExpression)
    {
        var binding = bindingExpression.ParentBinding;
        var details = new List<string>
        {
            $"Mode:{binding.Mode}",
        };

        if (!string.IsNullOrWhiteSpace(binding.Path?.Path))
        {
            details.Add($"Path:{binding.Path.Path}");
        }

        if (!string.IsNullOrWhiteSpace(binding.ElementName?.ToString()))
        {
            details.Add($"Element:{binding.ElementName}");
        }

        if (binding.RelativeSource is not null)
        {
            details.Add($"Relative:{binding.RelativeSource.Mode}");
        }
        else if (binding.Source is not null)
        {
            details.Add($"Source:{binding.Source.GetType().Name}");
        }
        else if (bindingExpression.DataItem is not null)
        {
            details.Add($"DataItem:{bindingExpression.DataItem.GetType().Name}");
        }

        if (binding.FallbackValue is not null)
        {
            details.Add($"Fallback:{FormatValue(binding.FallbackValue)}");
        }

        if (binding.TargetNullValue is not null)
        {
            details.Add($"TargetNull:{FormatValue(binding.TargetNullValue)}");
        }

        return string.Join(" | ", details);
    }

    internal static PropertyEditorMetadata GetEditorMetadata(Type targetType, object? currentValue, bool isEditable)
    {
        if (!isEditable)
        {
            return PropertyEditorMetadata.ReadOnly;
        }

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var actualType = nullableType ?? targetType;
        var supportsNullValue = nullableType is not null || !targetType.IsValueType;

        if (actualType == typeof(bool))
        {
            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.Boolean,
                PlaceholderText = supportsNullValue ? "(null)" : string.Empty,
                SupportsNullValue = supportsNullValue,
                SupportsThreeState = supportsNullValue,
            };
        }

        if (actualType.IsEnum)
        {
            var options = Enum
                .GetValues(actualType)
                .Cast<object>()
                .Select(x => new PropertyEditorOption
                {
                    DisplayText = x.ToString() ?? actualType.Name,
                    Value = x,
                })
                .ToList();

            if (supportsNullValue)
            {
                options.Insert(0, new PropertyEditorOption
                {
                    DisplayText = "(null)",
                    Value = null,
                    IsNullOption = true,
                });
            }

            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.Enum,
                PlaceholderText = supportsNullValue ? "(null)" : actualType.Name,
                SupportsNullValue = supportsNullValue,
                Options = options,
            };
        }

        if (IsNumericType(actualType))
        {
            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.Numeric,
                PlaceholderText = supportsNullValue ? "(null)" : actualType.Name,
                SupportsNullValue = supportsNullValue,
            };
        }

        if (actualType == typeof(Thickness))
        {
            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.Thickness,
                PlaceholderText = "Left,Top,Right,Bottom",
                SupportsNullValue = supportsNullValue,
            };
        }

        if (actualType == typeof(CornerRadius))
        {
            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.CornerRadius,
                PlaceholderText = "TopLeft,TopRight,BottomRight,BottomLeft",
                SupportsNullValue = supportsNullValue,
            };
        }

        if (actualType == typeof(GridLength))
        {
            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.GridLength,
                PlaceholderText = "Auto | 120 | 1*",
                SupportsNullValue = supportsNullValue,
            };
        }

        if (actualType == typeof(Color))
        {
            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.Color,
                PlaceholderText = "#AARRGGBB",
                SupportsNullValue = supportsNullValue,
            };
        }

        if (typeof(Brush).IsAssignableFrom(actualType))
        {
            if (currentValue is not null &&
                currentValue is not SolidColorBrush &&
                actualType != typeof(SolidColorBrush))
            {
                return CanConvertFromString(targetType)
                    ? new PropertyEditorMetadata
                    {
                        Kind = PropertyEditorKind.Text,
                        PlaceholderText = currentValue.GetType().Name,
                        SupportsNullValue = supportsNullValue,
                    }
                    : PropertyEditorMetadata.ReadOnly;
            }

            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.Brush,
                PlaceholderText = currentValue is null ? "(null)" : currentValue.GetType().Name,
                SupportsNullValue = supportsNullValue,
            };
        }

        if (CanConvertFromString(targetType))
        {
            return new PropertyEditorMetadata
            {
                Kind = PropertyEditorKind.Text,
                PlaceholderText = supportsNullValue ? "(null)" : actualType.Name,
                SupportsNullValue = supportsNullValue,
            };
        }

        return PropertyEditorMetadata.ReadOnly;
    }

    private static PropertyEditorCommitResult TrySetDependencyValue(DependencyObject element, DependencyProperty property, Type propertyType, object? value)
    {
        if (!TryConvertValue(propertyType, value, out var converted, out var error))
        {
            return PropertyEditorCommitResult.Failed(error);
        }

        try
        {
            element.SetValue(property, converted);
            return PropertyEditorCommitResult.Applied();
        }
        catch (Exception ex)
        {
            return PropertyEditorCommitResult.Failed(ex.GetBaseException().Message);
        }
    }

    private static PropertyEditorCommitResult TrySetClrValue(object element, PropertyInfo property, object? value)
    {
        if (!TryConvertValue(property.PropertyType, value, out var converted, out var error))
        {
            return PropertyEditorCommitResult.Failed(error);
        }

        try
        {
            property.SetValue(element, converted);
            return PropertyEditorCommitResult.Applied();
        }
        catch (Exception ex)
        {
            return PropertyEditorCommitResult.Failed(ex.GetBaseException().Message);
        }
    }

    internal static bool CanConvertFromString(Type targetType)
    {
        if (targetType == typeof(string))
        {
            return true;
        }

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var actualType = nullableType ?? targetType;
        if (actualType.IsEnum)
        {
            return true;
        }

        var converter = TypeDescriptor.GetConverter(actualType);
        return converter.CanConvertFrom(typeof(string)) || typeof(IConvertible).IsAssignableFrom(actualType);
    }

    internal static bool TryConvertFromString(Type targetType, string? value, out object? result)
    {
        try
        {
            result = ConvertFromString(targetType, value);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    internal static bool TryConvertValue(Type targetType, object? value, out object? result, out string? errorMessage)
    {
        try
        {
            result = ConvertValue(targetType, value);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            result = null;
            errorMessage = ex.GetBaseException().Message;
            return false;
        }
    }

    internal static object? ConvertFromString(Type targetType, string? value)
    {
        if (targetType == typeof(string))
        {
            return value;
        }

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var actualType = nullableType ?? targetType;

        if (string.IsNullOrWhiteSpace(value))
        {
            return nullableType is not null ? null : GetDefaultValue(actualType);
        }

        if (actualType.IsEnum)
        {
            return Enum.Parse(actualType, value!, ignoreCase: true);
        }

        var converter = TypeDescriptor.GetConverter(actualType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
        }

        return Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);
    }

    internal static object? ConvertValue(Type targetType, object? value)
    {
        if (value is string text)
        {
            return ConvertFromString(targetType, text);
        }

        if (value is null)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType is not null || !targetType.IsValueType)
            {
                return null;
            }

            return GetDefaultValue(targetType);
        }

        var nullableTargetType = Nullable.GetUnderlyingType(targetType);
        var actualType = nullableTargetType ?? targetType;

        if (actualType.IsInstanceOfType(value))
        {
            return value;
        }

        if (typeof(Brush).IsAssignableFrom(actualType) && value is Color brushColor)
        {
            return new SolidColorBrush(brushColor);
        }

        if (actualType == typeof(Color) && value is SolidColorBrush solidBrush)
        {
            return solidBrush.Color;
        }

        if (actualType.IsEnum)
        {
            return value switch
            {
                string enumText => Enum.Parse(actualType, enumText, ignoreCase: true),
                _ => Enum.ToObject(actualType, value),
            };
        }

        var converter = TypeDescriptor.GetConverter(actualType);
        if (converter.CanConvertFrom(value.GetType()))
        {
            return converter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
        }

        if (value is IConvertible)
        {
            return Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static object? GetDefaultValue(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    internal static bool IsNumericType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType == typeof(byte) ||
               actualType == typeof(sbyte) ||
               actualType == typeof(short) ||
               actualType == typeof(ushort) ||
               actualType == typeof(int) ||
               actualType == typeof(uint) ||
               actualType == typeof(long) ||
               actualType == typeof(ulong) ||
               actualType == typeof(float) ||
               actualType == typeof(double) ||
               actualType == typeof(decimal);
    }

    internal static string FormatValue(object? value)
    {
        if (ReferenceEquals(value, DependencyProperty.UnsetValue))
        {
            return "(unset)";
        }

        return value switch
        {
            null => "(null)",
            string text when string.IsNullOrEmpty(text) => "\"\"",
            string text => text,
            Exception ex => ex.Message,
            _ => value.ToString() ?? value.GetType().Name,
        };
    }

    internal static string FormatShallowValue(object? value)
    {
        if (ReferenceEquals(value, DependencyProperty.UnsetValue))
        {
            return "(unset)";
        }

        return value switch
        {
            null => "(null)",
            string text when string.IsNullOrEmpty(text) => "\"\"",
            string text => text,
            Exception ex => ex.Message,
            Type type => type.FullName ?? type.Name,
            Uri uri => uri.OriginalString,
            FrameworkElement fe => string.IsNullOrWhiteSpace(fe.Name)
                ? fe.GetType().Name
                : $"{fe.GetType().Name} #{fe.Name}",
            ResourceDictionary dictionary => $"{dictionary.GetType().Name} ({dictionary.Count} direct, {dictionary.MergedDictionaries.Count} merged, {dictionary.ThemeDictionaries.Count} theme)",
            DependencyObject dependencyObject => dependencyObject.GetType().Name,
            ICollection collection => $"{value.GetType().Name} (Count={collection.Count})",
            IEnumerable => value.GetType().Name,
            _ when value.GetType().IsPrimitive ||
                   value.GetType().IsEnum ||
                   value.GetType().IsValueType ||
                   value is decimal ||
                   value is DateTime ||
                   value is DateTimeOffset ||
                   value is TimeSpan ||
                   value is Guid => value.ToString() ?? value.GetType().Name,
            _ => value.GetType().Name,
        };
    }

    private static string GetCurrentPrecedence(DependencyObject element, DependencyProperty property)
    {
        try
        {
            var store = (element as IDependencyObjectStoreProvider)?.Store;
            var result = StoreGetCurrentHighestValuePrecedenceMethod?.Invoke(store, [property]);
            return result?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string? GetDependencyPropertyName(DependencyProperty property)
        => DependencyPropertyNameProperty?.GetValue(property) as string;

    private static Type? GetDependencyPropertyType(DependencyProperty property)
        => DependencyPropertyTypeProperty?.GetValue(property) as Type;

    private static Type? GetDependencyPropertyOwnerType(DependencyProperty property)
        => DependencyPropertyOwnerTypeProperty?.GetValue(property) as Type;

    private static bool GetDependencyPropertyIsAttached(DependencyProperty property)
        => DependencyPropertyIsAttachedProperty?.GetValue(property) as bool? ?? false;

    private static string GetDependencyPropertyKey(DependencyProperty property)
    {
        var ownerType = GetDependencyPropertyOwnerType(property)?.FullName ?? property.GetType().FullName ?? string.Empty;
        var name = GetDependencyPropertyName(property) ?? property.ToString() ?? nameof(DependencyProperty);
        return $"{ownerType}:{name}";
    }

    private static T? SafeRead<T>(Func<T?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return default;
        }
    }
}
