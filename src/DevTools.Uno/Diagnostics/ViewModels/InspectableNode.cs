using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class InspectableNode : ViewModelBase
{
    private bool _isExpanded;

    public InspectableNode(DependencyObject element, InspectableNode? parent, bool isVisualTree)
    {
        Element = element;
        Parent = parent;
        IsVisualTree = isVisualTree;
        TypeName = element.GetType().Name;
        ElementName = element is FrameworkElement fe && !string.IsNullOrWhiteSpace(fe.Name) ? fe.Name : string.Empty;
        StateSummary = CreateStateSummary(element);
        Selector = CreateSelector(element);
    }

    public DependencyObject Element { get; }
    public InspectableNode? Parent { get; }
    public ObservableCollection<InspectableNode> Children { get; } = [];
    public bool IsVisualTree { get; }

    public string TypeName { get; }

    public string ElementName { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(ElementName) ? TypeName : $"{TypeName}  #{ElementName}";

    public string NameSuffix => string.IsNullOrWhiteSpace(ElementName) ? string.Empty : $"  #{ElementName}";

    public string StateSummary { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public string Selector { get; }

    internal static string BuildStateSummary(DependencyObject element)
        => CreateStateSummary(element);

    internal static string BuildSelector(DependencyObject element)
        => CreateSelector(element);

    private static string CreateStateSummary(DependencyObject element)
    {
        var parts = new List<string>();

        if (element is FrameworkElement fe)
        {
            parts.Add($"W:{fe.ActualWidth:0.#}");
            parts.Add($"H:{fe.ActualHeight:0.#}");

            if (fe.Visibility != Visibility.Visible)
            {
                parts.Add(fe.Visibility.ToString());
            }

            if (ToolTipService.GetToolTip(fe) is not null)
            {
                parts.Add("ToolTip");
            }
        }

        if (element is Control control && !control.IsEnabled)
        {
            parts.Add("Disabled");
        }

        if (element is Popup popup)
        {
            parts.Add(popup.IsOpen ? "Popup(Open)" : "Popup(Closed)");
        }

        return string.Join(" ", parts);
    }

    private static string CreateSelector(DependencyObject element)
    {
        var parts = new Stack<string>();
        DependencyObject? current = element;

        while (current is not null)
        {
            if (current is FrameworkElement fe)
            {
                var name = string.IsNullOrWhiteSpace(fe.Name) ? string.Empty : $"#{fe.Name}";
                parts.Push($"{current.GetType().Name}{name}");
            }
            else
            {
                parts.Push(current.GetType().Name);
            }

            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }

        return string.Join(" > ", parts);
    }
}
