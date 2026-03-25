using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using IndexPath = Avalonia.Controls.IndexPath;
using AGridLength = Avalonia.GridLength;
using AGridUnitType = Avalonia.GridUnitType;
using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class TreePageViewModel : ViewModelBase
{
    private readonly FrameworkElement _root;
    private readonly MainViewModel _mainView;
    private readonly bool _isVisualTree;
    private InspectableNode? _hoveredNode;
    private InspectableNode? _selectedNode;
    private ControlDetailsViewModel? _details;
    private bool _suppressMainViewNotification;

    public TreePageViewModel(MainViewModel mainView, FrameworkElement root, bool isVisualTree, ISet<string> pinnedProperties)
    {
        _mainView = mainView;
        _root = root;
        _isVisualTree = isVisualTree;
        PinnedProperties = pinnedProperties;

        SelectRootCommand = new RelayCommand(Refresh);
        CopySelectorCommand = new RelayCommand(async () =>
        {
            if (SelectedNode is not null)
            {
                await PropertyInspector.CopyTextAsync(SelectedNode.Selector);
            }
        }, () => SelectedNode is not null);
        ExpandRecursivelyCommand = new RelayCommand(ExpandRecursively, () => SelectedNode is not null);
        CollapseChildrenCommand = new RelayCommand(CollapseChildren, () => SelectedNode is not null);
        BringIntoViewCommand = new RelayCommand(BringIntoView, () => SelectedNode?.Element is FrameworkElement);
        FocusCommand = new RelayCommand(FocusSelected, () => SelectedNode?.Element is Control);
        ScreenshotCommand = new RelayCommand(async () => await _mainView.CaptureSelectionAsync(), () => SelectedNode?.Element is FrameworkElement);

        var source = new HierarchicalTreeDataGridSource<InspectableNode>(Array.Empty<InspectableNode>());
        source.Columns.Add(
            new HierarchicalExpanderColumn<InspectableNode>(
                new TemplateColumn<InspectableNode>("Element", "TreeElementCellTemplate", width: new AGridLength(1, AGridUnitType.Star)),
                x => x.Children,
                x => x.Children.Count > 0,
                x => x.IsExpanded));

        TreeSource = source;
        Selection = new TreeDataGridRowSelectionModel<InspectableNode>(TreeSource)
        {
            SingleSelect = true,
        };
        Selection.SelectionChanged += (_, _) => SelectedNode = Selection.SelectedItem;
        TreeSource.Selection = Selection;

        Refresh();
    }

    public ISet<string> PinnedProperties { get; }

    public bool IsVisualTree => _isVisualTree;

    public string PageTitle => _isVisualTree ? "Visual Tree" : "Logical Tree";

    public string PageSummary => _isVisualTree
        ? "Inspect the rendered object hierarchy and keep the shared property inspector synchronized with hover and selection."
        : "Inspect the logical object hierarchy and keep the shared property inspector synchronized with hover and selection.";

    public HierarchicalTreeDataGridSource<InspectableNode> TreeSource { get; }

    public TreeDataGridRowSelectionModel<InspectableNode> Selection { get; }

    public RelayCommand SelectRootCommand { get; }

    public RelayCommand CopySelectorCommand { get; }

    public RelayCommand ExpandRecursivelyCommand { get; }

    public RelayCommand CollapseChildrenCommand { get; }

    public RelayCommand BringIntoViewCommand { get; }

    public RelayCommand FocusCommand { get; }

    public RelayCommand ScreenshotCommand { get; }

    public ControlDetailsViewModel? Details
    {
        get => _details;
        private set => RaiseAndSetIfChanged(ref _details, value);
    }

    public InspectableNode? SelectedNode
    {
        get => _selectedNode;
        private set
        {
            if (RaiseAndSetIfChanged(ref _selectedNode, value))
            {
                Details = value is not null ? new ControlDetailsViewModel(value.Element, PinnedProperties, _mainView.ShowClrProperties) : null;
                if (!_suppressMainViewNotification)
                {
                    _mainView.OnTreeSelectionChanged(this, value?.Element);
                }

                CopySelectorCommand.RaiseCanExecuteChanged();
                ExpandRecursivelyCommand.RaiseCanExecuteChanged();
                CollapseChildrenCommand.RaiseCanExecuteChanged();
                BringIntoViewCommand.RaiseCanExecuteChanged();
                FocusCommand.RaiseCanExecuteChanged();
                ScreenshotCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public void Refresh()
    {
        var selected = SelectedNode?.Element;
        TreeSource.Items = (_isVisualTree ? TreeInspector.BuildVisualTree(_root) : TreeInspector.BuildLogicalTree(_root)).ToArray();
        if (_hoveredNode is not null && !ContainsElement(_hoveredNode.Element))
        {
            ClearHoveredNode();
        }

        if (selected is not null)
        {
            SelectElement(selected, activateTab: false);
        }
    }

    public void UpdateIncludeClrProperties(bool includeClrProperties)
    {
        Details?.UpdateIncludeClrProperties(includeClrProperties);
    }

    public void UpdateHoveredNode(InspectableNode? node)
    {
        if (ReferenceEquals(_hoveredNode, node))
        {
            return;
        }

        _hoveredNode = node;
        _mainView.UpdateTreeHover(this, node?.Element);
    }

    public void ClearHoveredNode()
    {
        if (_hoveredNode is null)
        {
            return;
        }

        _hoveredNode = null;
        _mainView.ClearTreeHover();
    }

    public bool ContainsElement(DependencyObject element)
        => TryFind(TreeSource.Items, element, out _, out _);

    public bool SelectElement(DependencyObject? element, bool activateTab = true, bool notifyMainView = true)
    {
        if (element is null)
        {
            return false;
        }

        if (TryFind(TreeSource.Items, element, out var path, out var node))
        {
            ExpandAncestors(node);
            _suppressMainViewNotification = !notifyMainView;
            try
            {
                Selection.SelectedIndex = path;
                SelectedNode = node;
            }
            finally
            {
                _suppressMainViewNotification = false;
            }

            if (activateTab)
            {
                _mainView.SelectedTab = _isVisualTree ? 1 : 0;
            }

            return true;
        }

        return false;
    }

    private void ExpandRecursively()
    {
        if (SelectedNode is null)
        {
            return;
        }

        var stack = new Stack<InspectableNode>();
        stack.Push(SelectedNode);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            current.IsExpanded = true;
            foreach (var child in current.Children)
            {
                stack.Push(child);
            }
        }
    }

    private void CollapseChildren()
    {
        if (SelectedNode is null)
        {
            return;
        }

        var stack = new Stack<InspectableNode>();
        stack.Push(SelectedNode);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            current.IsExpanded = false;
            foreach (var child in current.Children)
            {
                stack.Push(child);
            }
        }
    }

    private void BringIntoView()
    {
        if (SelectedNode?.Element is FrameworkElement element)
        {
            element.StartBringIntoView();
        }
    }

    private void FocusSelected()
    {
        if (SelectedNode?.Element is Control control)
        {
            control.Focus(FocusState.Programmatic);
        }
    }

    private static void ExpandAncestors(InspectableNode? node)
    {
        while (node is not null)
        {
            node.IsExpanded = true;
            node = node.Parent;
        }
    }

    private static bool TryFind(IEnumerable<InspectableNode> roots, DependencyObject target, out IndexPath path, out InspectableNode? node)
    {
        var index = 0;
        foreach (var root in roots)
        {
            if (TryFind(root, target, new IndexPath(index), out path, out node))
            {
                return true;
            }

            index++;
        }

        path = default;
        node = null;
        return false;
    }

    private static bool TryFind(InspectableNode current, DependencyObject target, IndexPath currentPath, out IndexPath path, out InspectableNode? node)
    {
        if (ReferenceEquals(current.Element, target))
        {
            path = currentPath;
            node = current;
            return true;
        }

        for (var index = 0; index < current.Children.Count; index++)
        {
            if (TryFind(current.Children[index], target, currentPath.Append(index), out path, out node))
            {
                return true;
            }
        }

        path = default;
        node = null;
        return false;
    }
}
