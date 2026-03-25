using System.Reflection;
using DevTools.Uno.Diagnostics.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace DevTools.Uno.Diagnostics.Internal;

internal static class BindingInspector
{
    private static readonly BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
    private static readonly BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly PropertyInfo? DependencyPropertyNameProperty =
        typeof(DependencyProperty).GetProperty("Name", NonPublicInstance);

    private static readonly PropertyInfo? DependencyPropertyTypeProperty =
        typeof(DependencyProperty).GetProperty("Type", NonPublicInstance);

    private static readonly PropertyInfo? DependencyPropertyOwnerTypeProperty =
        typeof(DependencyProperty).GetProperty("OwnerType", NonPublicInstance);

    private static readonly PropertyInfo? BindingIsXBindProperty =
        typeof(Binding).GetProperty("IsXBind", NonPublicInstance);

    private static readonly PropertyInfo? BindingXBindPropertyPathsProperty =
        typeof(Binding).GetProperty("XBindPropertyPaths", NonPublicInstance);

    private static readonly PropertyInfo? BindingXBindBackProperty =
        typeof(Binding).GetProperty("XBindBack", NonPublicInstance);

    private static readonly PropertyInfo? ElementNameSubjectActualElementInstanceProperty =
        typeof(ElementNameSubject).GetProperty("ActualElementInstance", NonPublicInstance);

    public static BindingInspectionSnapshot BuildSnapshot(FrameworkElement root, DependencyObject? inspectionTarget)
    {
        var target = inspectionTarget ?? root;
        if (target is not IDependencyObjectStoreProvider)
        {
            target = root;
        }

        return new BindingInspectionSnapshot
        {
            Target = target,
            IsFallbackTarget = inspectionTarget is null || !ReferenceEquals(target, inspectionTarget),
            Bindings = BuildBindings(target).ToArray(),
        };
    }

    public static IReadOnlyList<BindingDescriptorViewModel> ApplyFilterAndSort(
        IReadOnlyList<BindingDescriptorViewModel> bindings,
        FilterViewModel filter,
        string sortBy,
        bool sortDescending)
    {
        IEnumerable<BindingDescriptorViewModel> query = bindings.Where(binding => MatchesFilter(binding, filter));
        query = sortBy switch
        {
            "Kind" => sortDescending
                ? query.OrderByDescending(x => x.Kind, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.Kind, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase),
            "Path" => sortDescending
                ? query.OrderByDescending(x => x.PathText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.PathText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase),
            "Source" => sortDescending
                ? query.OrderByDescending(x => x.SourceText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.SourceText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase),
            _ => sortDescending
                ? query.OrderByDescending(x => x.PropertyName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Kind, StringComparer.OrdinalIgnoreCase),
        };

        return query.ToArray();
    }

    public static IReadOnlyList<BindingFactViewModel> BuildFacts(BindingDescriptorViewModel binding)
    {
        return
        [
            new BindingFactViewModel
            {
                Name = "Binding Expression",
                Value = binding.Kind,
                Detail = $"Target: {binding.PropertyName}",
                InspectionObject = binding.BindingExpression,
            },
            new BindingFactViewModel
            {
                Name = "Binding Object",
                Value = binding.Binding.GetType().Name,
                Detail = $"Mode: {binding.ModeText} | Update: {binding.UpdateSourceTriggerText}",
                InspectionObject = binding.Binding,
            },
            new BindingFactViewModel
            {
                Name = "Target Property",
                Value = binding.PropertyName,
                Detail = $"{binding.TargetOwnerText} | {binding.TargetTypeText}",
                InspectionObject = binding.TargetProperty,
            },
            new BindingFactViewModel
            {
                Name = "Current Value",
                Value = binding.ValueText,
                Detail = $"Target Type: {binding.TargetTypeText}",
                InspectionObject = binding.CurrentValue,
            },
            new BindingFactViewModel
            {
                Name = "Path",
                Value = binding.PathText,
                Detail = binding.IsXBind && binding.XBindPathsText != "(none)"
                    ? $"Observed: {binding.XBindPathsText}"
                    : binding.Kind,
                InspectionObject = binding.Binding,
            },
            new BindingFactViewModel
            {
                Name = "Mode",
                Value = binding.ModeText,
                Detail = $"Update: {binding.UpdateSourceTriggerText}",
                InspectionObject = binding.Binding,
            },
            new BindingFactViewModel
            {
                Name = "Source Strategy",
                Value = binding.SourceText,
                Detail = binding.Kind,
                InspectionObject = binding.SourceObject ?? binding.DataContextObject ?? binding.Binding,
            },
            new BindingFactViewModel
            {
                Name = "Source Object",
                Value = binding.SourceObjectText,
                Detail = binding.SourceText,
                InspectionObject = binding.SourceObject ?? binding.CompiledSourceObject ?? binding.ElementNameObject,
            },
            new BindingFactViewModel
            {
                Name = "Data Context",
                Value = binding.DataContextText,
                Detail = "BindingExpression.DataContext",
                InspectionObject = binding.DataContextObject,
            },
            new BindingFactViewModel
            {
                Name = "Data Item",
                Value = binding.DataItemText,
                Detail = "BindingExpression.DataItem",
                InspectionObject = binding.DataItemObject,
            },
            new BindingFactViewModel
            {
                Name = "Relative Source",
                Value = binding.RelativeSourceText,
                Detail = "Binding.RelativeSource",
                InspectionObject = binding.RelativeSourceObject,
            },
            new BindingFactViewModel
            {
                Name = "Element Name",
                Value = binding.ElementNameText,
                Detail = "ElementName resolution",
                InspectionObject = binding.ElementNameObject,
            },
            new BindingFactViewModel
            {
                Name = "Converter",
                Value = binding.ConverterText,
                Detail = binding.ConverterLanguageText,
                InspectionObject = binding.ConverterObject,
            },
            new BindingFactViewModel
            {
                Name = "Converter Parameter",
                Value = binding.ConverterParameterText,
                Detail = "Binding.ConverterParameter",
                InspectionObject = binding.ConverterParameterObject,
            },
            new BindingFactViewModel
            {
                Name = "Fallback Value",
                Value = binding.FallbackValueText,
                Detail = "Binding.FallbackValue",
                InspectionObject = binding.FallbackValueObject,
            },
            new BindingFactViewModel
            {
                Name = "Target Null Value",
                Value = binding.TargetNullValueText,
                Detail = "Binding.TargetNullValue",
                InspectionObject = binding.TargetNullValueObject,
            },
            new BindingFactViewModel
            {
                Name = "Compiled Source",
                Value = binding.CompiledSourceText,
                Detail = binding.IsXBind ? "x:Bind compiled source" : "(none)",
                InspectionObject = binding.CompiledSourceObject,
            },
            new BindingFactViewModel
            {
                Name = "x:Bind Paths",
                Value = binding.XBindPathsText,
                Detail = binding.IsXBind ? "Observed source paths" : "(none)",
                InspectionObject = binding.Binding,
            },
            new BindingFactViewModel
            {
                Name = "Back Channel",
                Value = binding.HasBackChannelText,
                Detail = "TwoWay mode or x:Bind back callback",
                InspectionObject = binding.Binding,
            },
        ];
    }

    public static string FormatInspectionTargetSummary(BindingInspectionSnapshot? snapshot, FrameworkElement root)
    {
        if (snapshot is null)
        {
            return "Selection: (none)";
        }

        var text = DescribeDependencyObject(snapshot.Target);
        return snapshot.IsFallbackTarget
            ? $"Selection: (none, using host root {text})"
            : $"Selection: {text}";
    }

    private static IEnumerable<BindingDescriptorViewModel> BuildBindings(DependencyObject target)
    {
        var store = (target as IDependencyObjectStoreProvider)?.Store;
        if (store is null)
        {
            yield break;
        }

        foreach (var property in EnumerateDependencyProperties(target))
        {
            var expression = SafeRead(() => store.GetBindingExpression(property));
            if (expression is null)
            {
                continue;
            }

            var binding = expression.ParentBinding;
            var propertyName = expression.TargetName ?? GetDependencyPropertyName(property) ?? property.ToString() ?? "Property";
            var propertyType = GetDependencyPropertyType(property) ?? typeof(object);
            var ownerType = GetDependencyPropertyOwnerType(property) ?? target.GetType();
            var currentValue = SafeRead(() => target.GetValue(property));
            var isXBind = IsXBind(binding);
            var isTemplateBinding = binding.RelativeSource?.Mode == RelativeSourceMode.TemplatedParent;
            var xBindPaths = GetXBindPaths(binding);
            var sourceResolution = ResolveSource(target, binding, expression);

            yield return new BindingDescriptorViewModel
            {
                BindingId = $"BINDING:{ownerType.FullName}.{propertyName}",
                PropertyName = propertyName,
                Kind = GetBindingKind(isXBind, isTemplateBinding),
                PathText = GetPathText(binding, xBindPaths),
                SourceText = sourceResolution.Strategy,
                ValueText = PropertyInspector.FormatValue(currentValue),
                Summary = BuildSummary(propertyName, binding, sourceResolution.Strategy, currentValue, xBindPaths, isXBind),
                TargetOwnerText = ownerType.Name,
                TargetTypeText = propertyType.Name,
                ModeText = binding.Mode.ToString(),
                UpdateSourceTriggerText = ResolveUpdateSourceTrigger(binding, property),
                RelativeSourceText = binding.RelativeSource?.Mode.ToString() ?? "(none)",
                ElementNameText = sourceResolution.ElementNameText,
                SourceObjectText = DescribeObject(sourceResolution.SourceObject),
                DataContextText = DescribeObject(SafeRead(() => expression.DataContext)),
                DataItemText = DescribeObject(SafeRead(() => expression.DataItem)),
                ConverterText = DescribeObject(binding.Converter),
                ConverterParameterText = PropertyInspector.FormatValue(binding.ConverterParameter),
                ConverterLanguageText = string.IsNullOrWhiteSpace(binding.ConverterLanguage) ? "(none)" : binding.ConverterLanguage,
                FallbackValueText = PropertyInspector.FormatValue(binding.FallbackValue),
                TargetNullValueText = PropertyInspector.FormatValue(binding.TargetNullValue),
                CompiledSourceText = DescribeObject(binding.CompiledSource),
                XBindPathsText = xBindPaths.Count > 0 ? string.Join(", ", xBindPaths) : "(none)",
                HasBackChannelText = HasBackChannel(binding).ToString(),
                IsXBind = isXBind,
                IsTemplateBinding = isTemplateBinding,
                TargetElement = target,
                TargetProperty = property,
                BindingExpression = expression,
                Binding = binding,
                CurrentValue = currentValue,
                SourceObject = sourceResolution.SourceObject,
                RelativeSourceObject = binding.RelativeSource,
                ElementNameObject = sourceResolution.ElementNameObject,
                DataContextObject = SafeRead(() => expression.DataContext),
                DataItemObject = SafeRead(() => expression.DataItem),
                ConverterObject = binding.Converter,
                ConverterParameterObject = binding.ConverterParameter,
                FallbackValueObject = binding.FallbackValue,
                TargetNullValueObject = binding.TargetNullValue,
                CompiledSourceObject = binding.CompiledSource,
            };
        }
    }

    private static IEnumerable<DependencyProperty> EnumerateDependencyProperties(DependencyObject target)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in target.GetType().GetFields(AllStatic))
        {
            if (!field.Name.EndsWith("Property", StringComparison.Ordinal) ||
                field.GetValue(null) is not DependencyProperty property)
            {
                continue;
            }

            var owner = GetDependencyPropertyOwnerType(property) ?? target.GetType();
            var name = GetDependencyPropertyName(property) ?? field.Name[..^"Property".Length];
            if (!seen.Add($"{owner.FullName}:{name}"))
            {
                continue;
            }

            yield return property;
        }
    }

    private static string BuildSummary(
        string propertyName,
        Binding binding,
        string sourceStrategy,
        object? currentValue,
        IReadOnlyList<string> xBindPaths,
        bool isXBind)
    {
        var parts = new List<string>
        {
            $"{GetBindingKind(isXBind, binding.RelativeSource?.Mode == RelativeSourceMode.TemplatedParent)} -> {propertyName}",
            $"Source: {sourceStrategy}",
        };

        if (!string.IsNullOrWhiteSpace(binding.Path?.Path))
        {
            parts.Add($"Path: {binding.Path.Path}");
        }
        else if (xBindPaths.Count > 0)
        {
            parts.Add($"Observed: {string.Join(", ", xBindPaths)}");
        }

        parts.Add($"Value: {PropertyInspector.FormatValue(currentValue)}");
        return string.Join(" | ", parts);
    }

    private static SourceResolution ResolveSource(DependencyObject target, Binding binding, BindingExpression expression)
    {
        if (binding.ElementName is ElementNameSubject elementNameSubject)
        {
            var sourceObject = ResolveElementNameInstance(elementNameSubject);
            return new SourceResolution(
                Strategy: $"ElementName:{elementNameSubject.Name ?? "(unnamed)"}",
                SourceObject: sourceObject,
                ElementNameText: elementNameSubject.Name ?? "(unnamed)",
                ElementNameObject: sourceObject ?? elementNameSubject);
        }

        if (binding.ElementName is string elementName && !string.IsNullOrWhiteSpace(elementName))
        {
            return new SourceResolution(
                Strategy: $"ElementName:{elementName}",
                SourceObject: null,
                ElementNameText: elementName,
                ElementNameObject: null);
        }

        if (binding.Source is ElementNameSubject sourceSubject)
        {
            var sourceObject = ResolveElementNameInstance(sourceSubject);
            return new SourceResolution(
                Strategy: $"ElementName:{sourceSubject.Name ?? "(unnamed)"}",
                SourceObject: sourceObject,
                ElementNameText: sourceSubject.Name ?? "(unnamed)",
                ElementNameObject: sourceObject ?? sourceSubject);
        }

        if (binding.Source is string sourceElementName && !string.IsNullOrWhiteSpace(sourceElementName))
        {
            return new SourceResolution(
                Strategy: $"ElementName:{sourceElementName}",
                SourceObject: null,
                ElementNameText: sourceElementName,
                ElementNameObject: null);
        }

        if (binding.RelativeSource?.Mode == RelativeSourceMode.TemplatedParent)
        {
            var templatedParent = SafeRead(() => expression.DataContext);
            return new SourceResolution(
                Strategy: $"RelativeSource:{binding.RelativeSource.Mode}",
                SourceObject: templatedParent,
                ElementNameText: "(none)",
                ElementNameObject: null);
        }

        if (binding.RelativeSource?.Mode == RelativeSourceMode.Self)
        {
            return new SourceResolution(
                Strategy: $"RelativeSource:{binding.RelativeSource.Mode}",
                SourceObject: target,
                ElementNameText: "(none)",
                ElementNameObject: null);
        }

        if (binding.RelativeSource is not null)
        {
            var relativeSourceObject = SafeRead(() => expression.DataContext);
            return new SourceResolution(
                Strategy: $"RelativeSource:{binding.RelativeSource.Mode}",
                SourceObject: relativeSourceObject,
                ElementNameText: "(none)",
                ElementNameObject: null);
        }

        if (binding.Source is not null)
        {
            return new SourceResolution(
                Strategy: $"Source:{DescribeObject(binding.Source)}",
                SourceObject: binding.Source,
                ElementNameText: "(none)",
                ElementNameObject: null);
        }

        if (binding.CompiledSource is not null)
        {
            return new SourceResolution(
                Strategy: $"CompiledSource:{DescribeObject(binding.CompiledSource)}",
                SourceObject: binding.CompiledSource,
                ElementNameText: "(none)",
                ElementNameObject: null);
        }

        var dataContext = SafeRead(() => expression.DataContext);
        return new SourceResolution(
            Strategy: dataContext is not null ? $"DataContext:{DescribeObject(dataContext)}" : "Unresolved",
            SourceObject: dataContext,
            ElementNameText: "(none)",
            ElementNameObject: null);
    }

    private static bool MatchesFilter(BindingDescriptorViewModel binding, FilterViewModel filter)
    {
        return filter.Filter(binding.PropertyName) ||
               filter.Filter(binding.Kind) ||
               filter.Filter(binding.PathText) ||
               filter.Filter(binding.SourceText) ||
               filter.Filter(binding.ValueText) ||
               filter.Filter(binding.Summary);
    }

    private static string GetBindingKind(bool isXBind, bool isTemplateBinding)
    {
        if (isXBind)
        {
            return "x:Bind";
        }

        if (isTemplateBinding)
        {
            return "TemplateBinding";
        }

        return "Binding";
    }

    private static bool IsXBind(Binding binding)
        => SafeRead(() => BindingIsXBindProperty?.GetValue(binding) as bool?) ?? binding.CompiledSource is not null;

    private static bool HasBackChannel(Binding binding)
        => binding.Mode == BindingMode.TwoWay || SafeRead(() => BindingXBindBackProperty?.GetValue(binding)) is not null;

    private static IReadOnlyList<string> GetXBindPaths(Binding binding)
    {
        if (SafeRead(() => BindingXBindPropertyPathsProperty?.GetValue(binding) as string[]) is { Length: > 0 } paths)
        {
            return paths;
        }

        return [];
    }

    private static string ResolveUpdateSourceTrigger(Binding binding, DependencyProperty property)
    {
        if (binding.UpdateSourceTrigger != UpdateSourceTrigger.Default)
        {
            return binding.UpdateSourceTrigger.ToString();
        }

        return property == TextBox.TextProperty
            ? $"{UpdateSourceTrigger.Explicit} (resolved default)"
            : $"{UpdateSourceTrigger.PropertyChanged} (resolved default)";
    }

    private static string GetPathText(Binding binding, IReadOnlyList<string> xBindPaths)
    {
        if (!string.IsNullOrWhiteSpace(binding.Path?.Path))
        {
            return binding.Path.Path;
        }

        if (xBindPaths.Count > 0)
        {
            return string.Join(", ", xBindPaths);
        }

        return "(implicit)";
    }

    private static string DescribeObject(object? value)
    {
        return value switch
        {
            null => "(none)",
            FrameworkElement element => ResourceInspector.FormatElementName(element),
            ElementNameSubject subject when !string.IsNullOrWhiteSpace(subject.Name) => subject.Name!,
            DependencyObject dependencyObject => dependencyObject.GetType().FullName ?? dependencyObject.GetType().Name,
            Type type => type.FullName ?? type.Name,
            _ => PropertyInspector.FormatValue(value),
        };
    }

    private static string DescribeDependencyObject(DependencyObject value)
    {
        return value switch
        {
            FrameworkElement element => ResourceInspector.FormatElementName(element),
            _ => value.GetType().FullName ?? value.GetType().Name,
        };
    }

    private static string? GetDependencyPropertyName(DependencyProperty property)
        => SafeRead(() => DependencyPropertyNameProperty?.GetValue(property) as string);

    private static Type? GetDependencyPropertyType(DependencyProperty property)
        => SafeRead(() => DependencyPropertyTypeProperty?.GetValue(property) as Type);

    private static Type? GetDependencyPropertyOwnerType(DependencyProperty property)
        => SafeRead(() => DependencyPropertyOwnerTypeProperty?.GetValue(property) as Type);

    private static object? ResolveElementNameInstance(ElementNameSubject subject)
        => subject.ElementInstance ?? SafeRead(() => ElementNameSubjectActualElementInstanceProperty?.GetValue(subject));

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

    private sealed record SourceResolution(string Strategy, object? SourceObject, string ElementNameText, object? ElementNameObject);
}

internal sealed class BindingInspectionSnapshot
{
    public required DependencyObject Target { get; init; }

    public required bool IsFallbackTarget { get; init; }

    public required IReadOnlyList<BindingDescriptorViewModel> Bindings { get; init; }
}
