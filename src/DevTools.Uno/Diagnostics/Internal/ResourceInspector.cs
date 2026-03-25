using DevTools.Uno.Diagnostics.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace DevTools.Uno.Diagnostics.Internal;

internal static class ResourceInspector
{
    public static IReadOnlyList<ResourceProviderNode> BuildProviderTree(FrameworkElement root, DependencyObject? inspectionTarget)
    {
        var result = new List<ResourceProviderNode>();

        if (Application.Current is { } application)
        {
            result.Add(CreateDictionaryProvider(
                application.Resources,
                "Application",
                "Application",
                "Application",
                application));
        }

        result.Add(CreateElementProvider(root, "Host Root", "HostRoot"));

        if (inspectionTarget is FrameworkElement selectedElement && !ReferenceEquals(selectedElement, root))
        {
            var selectionNode = new ResourceProviderNode
            {
                Name = "Selection Scope",
                Kind = "Selection",
                Summary = FormatElementName(selectedElement),
                Path = "Selection",
            };

            var depth = 0;
            foreach (var element in GetAncestorChain(selectedElement))
            {
                selectionNode.AddChild(CreateElementProvider(
                    element,
                    depth == 0 ? "Selected Element" : "Ancestor Element",
                    $"Selection/{depth}"));
                depth++;
            }

            if (selectionNode.Children.Count > 0)
            {
                result.Add(selectionNode);
            }
        }

        if (root.XamlRoot is { } xamlRoot)
        {
            var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot);
            if (popups.Count > 0)
            {
                var popupsNode = new ResourceProviderNode
                {
                    Name = "Open Popups",
                    Kind = "Popups",
                    Summary = $"{popups.Count} open",
                    Path = "Popups",
                };

                for (var index = 0; index < popups.Count; index++)
                {
                    var popup = popups[index];
                    if (popup.Child is FrameworkElement child)
                    {
                        popupsNode.AddChild(CreateElementProvider(
                            child,
                            $"Popup {index + 1}",
                            $"Popups/{index}"));
                    }
                }

                if (popupsNode.Children.Count > 0)
                {
                    result.Add(popupsNode);
                }
            }
        }

        return result;
    }

    public static IReadOnlyList<ResourceEntryViewModel> BuildResources(
        ResourceProviderNode provider,
        FilterViewModel filter,
        bool includeNested,
        string sortBy,
        bool sortDescending,
        Action<ResourceEntryViewModel>? valueReplaced)
    {
        var resources = new List<ResourceEntryViewModel>();
        var visited = new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance);
        CollectResources(provider, includeNested, visited, resources, valueReplaced);

        IEnumerable<ResourceEntryViewModel> query = resources.Where(x => MatchesFilter(x, filter));
        query = sortBy switch
        {
            "Type" => sortDescending
                ? query.OrderByDescending(x => x.TypeText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.KeyText, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.TypeText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.KeyText, StringComparer.OrdinalIgnoreCase),
            _ => sortDescending
                ? query.OrderByDescending(x => x.KeyText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.TypeText, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.KeyText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.TypeText, StringComparer.OrdinalIgnoreCase),
        };

        return query.ToArray();
    }

    public static string FormatElementName(FrameworkElement element)
    {
        return string.IsNullOrWhiteSpace(element.Name)
            ? element.GetType().Name
            : $"{element.GetType().Name} #{element.Name}";
    }

    public static string FormatKey(object? key)
    {
        return key switch
        {
            null => "(null)",
            Type type => type.FullName ?? type.Name,
            _ => key.ToString() ?? key.GetType().Name,
        };
    }

    private static ResourceProviderNode CreateElementProvider(FrameworkElement element, string kind, string path)
    {
        var node = new ResourceProviderNode
        {
            Name = FormatElementName(element),
            Kind = kind,
            Summary = GetDictionarySummary(element.Resources),
            Path = path,
            Provider = element,
            Dictionary = element.Resources,
        };

        PopulateDictionaryChildren(node, element.Resources, path, new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance));
        return node;
    }

    private static ResourceProviderNode CreateDictionaryProvider(
        ResourceDictionary dictionary,
        string name,
        string kind,
        string path,
        object? provider)
    {
        var node = new ResourceProviderNode
        {
            Name = name,
            Kind = kind,
            Summary = GetDictionarySummary(dictionary),
            Path = path,
            Provider = provider,
            Dictionary = dictionary,
        };

        PopulateDictionaryChildren(node, dictionary, path, new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance));
        return node;
    }

    private static void PopulateDictionaryChildren(
        ResourceProviderNode parent,
        ResourceDictionary dictionary,
        string path,
        HashSet<ResourceDictionary> stack)
    {
        if (!stack.Add(dictionary))
        {
            return;
        }

        var themeDictionaries = new List<KeyValuePair<object, object?>>();
        foreach (var pair in dictionary.ThemeDictionaries)
        {
            themeDictionaries.Add(new KeyValuePair<object, object?>(pair.Key, pair.Value));
        }

        themeDictionaries.Sort((x, y) =>
            StringComparer.OrdinalIgnoreCase.Compare(FormatKey(x.Key), FormatKey(y.Key)));

        foreach (var pair in themeDictionaries)
        {
            if (pair.Value is not ResourceDictionary themeDictionary)
            {
                continue;
            }

            parent.AddChild(CreateNestedDictionaryProvider(
                themeDictionary,
                $"{FormatKey(pair.Key)} Theme",
                "Theme Dictionary",
                $"{path}/Theme[{FormatKey(pair.Key)}]",
                pair.Key,
                stack));
        }

        for (var index = 0; index < dictionary.MergedDictionaries.Count; index++)
        {
            parent.AddChild(CreateNestedDictionaryProvider(
                dictionary.MergedDictionaries[index],
                GetDictionaryDisplayName(dictionary.MergedDictionaries[index], $"Merged {index + 1}"),
                "Merged Dictionary",
                $"{path}/Merged[{index}]",
                null,
                stack));
        }

        foreach (var pair in dictionary)
        {
            if (pair.Value is not ResourceDictionary nestedDictionary)
            {
                continue;
            }

            parent.AddChild(CreateNestedDictionaryProvider(
                nestedDictionary,
                FormatKey(pair.Key),
                "Resource Dictionary",
                $"{path}/Resource[{FormatKey(pair.Key)}]",
                pair.Key,
                stack));
        }

        stack.Remove(dictionary);
    }

    private static ResourceProviderNode CreateNestedDictionaryProvider(
        ResourceDictionary dictionary,
        string name,
        string kind,
        string path,
        object? provider,
        HashSet<ResourceDictionary> stack)
    {
        var node = new ResourceProviderNode
        {
            Name = name,
            Kind = kind,
            Summary = GetDictionarySummary(dictionary),
            Path = path,
            Provider = provider ?? dictionary,
            Dictionary = dictionary,
        };

        PopulateDictionaryChildren(node, dictionary, path, stack);
        return node;
    }

    private static void CollectResources(
        ResourceProviderNode provider,
        bool includeNested,
        HashSet<ResourceDictionary> visited,
        List<ResourceEntryViewModel> resources,
        Action<ResourceEntryViewModel>? valueReplaced)
    {
        if (provider.Dictionary is { } dictionary && visited.Add(dictionary))
        {
            foreach (var pair in dictionary)
            {
                resources.Add(new ResourceEntryViewModel(
                    pair.Key,
                    pair.Value,
                    dictionary,
                    provider.Name,
                    provider.Kind,
                    provider.Path,
                    dictionary.Source?.OriginalString ?? "(inline)",
                    $"{provider.Path}::{FormatKey(pair.Key)}",
                    valueReplaced));
            }
        }

        if (!includeNested)
        {
            return;
        }

        foreach (var child in provider.Children)
        {
            CollectResources(child, includeNested: true, visited, resources, valueReplaced);
        }
    }

    private static bool MatchesFilter(ResourceEntryViewModel entry, FilterViewModel filter)
    {
        return filter.Filter(entry.KeyText) ||
               filter.Filter(entry.TypeText) ||
               filter.Filter(entry.ProviderName) ||
               filter.Filter(entry.ProviderKind) ||
               filter.Filter(entry.ProviderPath);
    }

    private static string GetDictionaryDisplayName(ResourceDictionary dictionary, string fallback)
    {
        return dictionary.Source is { } source
            ? source.OriginalString
            : fallback;
    }

    private static string GetDictionarySummary(ResourceDictionary dictionary)
    {
        return $"{dictionary.Count} direct, {dictionary.MergedDictionaries.Count} merged, {dictionary.ThemeDictionaries.Count} theme";
    }

    private static IEnumerable<FrameworkElement> GetAncestorChain(FrameworkElement element)
    {
        var current = element;
        var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);

        while (current is not null && seen.Add(current))
        {
            yield return current;
            current = GetParent(current) as FrameworkElement;
        }
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is FrameworkElement frameworkElement && frameworkElement.Parent is DependencyObject logicalParent)
        {
            return logicalParent;
        }

        return VisualTreeHelper.GetParent(element);
    }
}
