using System.Linq;
using DevTools.Uno.Diagnostics.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace DevTools.Uno.Diagnostics.Internal;

internal static class TreeInspector
{
    public static IReadOnlyList<InspectableNode> BuildVisualTree(FrameworkElement root)
    {
        var top = new InspectableNode(root, null, isVisualTree: true)
        {
            IsExpanded = true,
        };

        var attachedPopups = new HashSet<DependencyObject>();
        PopulateVisualChildren(top, root, attachedPopups);

        foreach (var popupChild in GetOpenPopupChildren(root.XamlRoot))
        {
            if (!attachedPopups.Add(popupChild))
            {
                continue;
            }

            var node = new InspectableNode(popupChild, top, isVisualTree: true);
            top.Children.Add(node);
            PopulateVisualChildren(node, popupChild, attachedPopups);
        }

        return [top];
    }

    public static IReadOnlyList<InspectableNode> BuildLogicalTree(FrameworkElement root)
    {
        var top = new InspectableNode(root, null, isVisualTree: false)
        {
            IsExpanded = true,
        };

        PopulateLogicalChildren(top, root, isRoot: true);
        return [top];
    }

    public static IReadOnlyList<DependencyObject> GetOpenPopupChildren(XamlRoot? xamlRoot)
    {
        if (xamlRoot is null)
        {
            return [];
        }

        return VisualTreeHelper
            .GetOpenPopupsForXamlRoot(xamlRoot)
            .Select(x => x.Child)
            .Where(x => x is not null)
            .Cast<DependencyObject>()
            .ToArray();
    }

    private static void PopulateVisualChildren(InspectableNode parent, DependencyObject element, ISet<DependencyObject> attachedPopups)
    {
        var count = VisualTreeHelper.GetChildrenCount(element);

        for (var index = 0; index < count; index++)
        {
            if (VisualTreeHelper.GetChild(element, index) is not DependencyObject child)
            {
                continue;
            }

            var node = new InspectableNode(child, parent, isVisualTree: true);
            parent.Children.Add(node);
            PopulateVisualChildren(node, child, attachedPopups);
        }

        if (element is FrameworkElement fe && fe.XamlRoot is not null)
        {
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(fe.XamlRoot))
            {
                if (popup.Child is not null && popup.Parent == element && attachedPopups.Add(popup.Child))
                {
                    var node = new InspectableNode(popup.Child, parent, isVisualTree: true);
                    parent.Children.Add(node);
                    PopulateVisualChildren(node, popup.Child, attachedPopups);
                }
            }
        }
    }

    private static void PopulateLogicalChildren(InspectableNode parent, DependencyObject element, bool isRoot = false)
    {
        foreach (var child in GetLogicalChildren(element, isRoot))
        {
            var node = new InspectableNode(child, parent, isVisualTree: false);
            parent.Children.Add(node);
            PopulateLogicalChildren(node, child);
        }
    }

    private static IReadOnlyList<DependencyObject> GetLogicalChildren(DependencyObject element, bool isRoot)
    {
        var result = new List<DependencyObject>();
        var seen = new HashSet<DependencyObject>();

        void Add(object? value)
        {
            switch (value)
            {
                case null:
                case string:
                    return;
                case DependencyObject d when seen.Add(d):
                    result.Add(d);
                    break;
                case System.Collections.IEnumerable enumerable:
                    foreach (var item in enumerable)
                    {
                        Add(item);
                    }

                    break;
            }
        }

        switch (element)
        {
            case Panel panel:
                Add(panel.Children);
                break;
            case Border border:
                Add(border.Child);
                break;
            case ContentControl contentControl:
                Add(contentControl.Content);
                break;
            case ContentPresenter presenter:
                Add(presenter.Content);
                break;
            case ItemsControl itemsControl:
                Add(itemsControl.ItemsPanelRoot);
                break;
            case Popup popup:
                Add(popup.Child);
                break;
        }

        if (element is FrameworkElement fe)
        {
            Add(GetPropertyValue(fe, "Header"));
            Add(GetPropertyValue(fe, "Footer"));
            Add(ToolTipService.GetToolTip(fe));
            Add(FlyoutBase.GetAttachedFlyout(fe));

            if (isRoot && fe.XamlRoot is not null)
            {
                foreach (var popupChild in GetOpenPopupChildren(fe.XamlRoot))
                {
                    Add(popupChild);
                }
            }
        }

        if (element is UIElement uiElement &&
            uiElement.ReadLocalValue(UIElement.ContextFlyoutProperty) != DependencyProperty.UnsetValue)
        {
            Add(uiElement.ContextFlyout);
        }

        if (result.Count == 0)
        {
            var visualCount = VisualTreeHelper.GetChildrenCount(element);
            for (var index = 0; index < visualCount; index++)
            {
                Add(VisualTreeHelper.GetChild(element, index));
            }
        }

        return result;
    }

    private static object? GetPropertyValue(object instance, string name)
        => instance.GetType().GetProperty(name)?.GetValue(instance);
}
