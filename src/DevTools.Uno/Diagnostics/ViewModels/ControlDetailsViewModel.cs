using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using IndexPath = Avalonia.Controls.IndexPath;
using AGridLength = Avalonia.GridLength;
using AGridUnitType = Avalonia.GridUnitType;
using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class ControlDetailsViewModel : ViewModelBase
{
    private readonly ISet<string> _pinnedProperties;
    private bool _includeClrProperties;
    private PropertyGridNode? _selectedProperty;

    public ControlDetailsViewModel(DependencyObject element, ISet<string> pinnedProperties, bool includeClrProperties)
    {
        Element = element;
        _pinnedProperties = pinnedProperties;
        _includeClrProperties = includeClrProperties;
        Layout = new ControlLayoutViewModel();
        Metadata = new ControlMetadataViewModel();
        Filter = new FilterViewModel();
        Filter.RefreshFilter += (_, _) => Refresh();

        TogglePinCommand = new RelayCommand(TogglePinnedSelection, () => SelectedProperty is { IsGroup: false });
        CopyValueCommand = new RelayCommand(async () =>
        {
            if (SelectedProperty is not null)
            {
                await PropertyInspector.CopyTextAsync(SelectedProperty.ValueText);
            }
        }, () => SelectedProperty is not null);

        var source = new HierarchicalTreeDataGridSource<PropertyGridNode>(Array.Empty<PropertyGridNode>());
        source.Columns.Add(
            new HierarchicalExpanderColumn<PropertyGridNode>(
                new TextColumn<PropertyGridNode, string>("Property", x => x.Name, new AGridLength(2, AGridUnitType.Star)),
                x => x.Children,
                x => x.Children.Count > 0,
                x => x.IsExpanded));
        source.Columns.Add(new TextColumn<PropertyGridNode, string>("Value", x => x.ValueText, (row, value) => { row.ApplyValue(value); }, new AGridLength(2, AGridUnitType.Star)));
        source.Columns.Add(new TextColumn<PropertyGridNode, string>("Type", x => x.TypeText, new AGridLength(1, AGridUnitType.Star)));
        source.Columns.Add(new TextColumn<PropertyGridNode, string>("Priority", x => x.PriorityText, new AGridLength(1, AGridUnitType.Star)));
        source.Columns.Add(new TextColumn<PropertyGridNode, string>("Source", x => x.SourceText, new AGridLength(1, AGridUnitType.Star)));

        PropertySource = source;
        Selection = new TreeDataGridRowSelectionModel<PropertyGridNode>(PropertySource)
        {
            SingleSelect = true,
        };
        Selection.SelectionChanged += (_, _) => SelectedProperty = Selection.SelectedItem;
        PropertySource.Selection = Selection;
        ValueSourceGrid = PropertyValueSourceGridBuilder.Create();

        Refresh();
    }

    public DependencyObject Element { get; }

    public string SelectedElementName => Element is FrameworkElement fe && !string.IsNullOrWhiteSpace(fe.Name) ? fe.Name : "(unnamed)";

    public string SelectedElementType => Element.GetType().FullName ?? Element.GetType().Name;

    public FilterViewModel Filter { get; }

    public ControlLayoutViewModel Layout { get; }

    public ControlMetadataViewModel Metadata { get; }

    public HierarchicalTreeDataGridSource<PropertyGridNode> PropertySource { get; }

    public TreeDataGridRowSelectionModel<PropertyGridNode> Selection { get; }

    public FlatTreeDataGridSource<PropertyValueSourceViewModel> ValueSourceGrid { get; }

    public RelayCommand TogglePinCommand { get; }

    public RelayCommand CopyValueCommand { get; }

    public PropertyGridNode? SelectedProperty
    {
        get => _selectedProperty;
        private set
        {
            if (ReferenceEquals(_selectedProperty, value))
            {
                return;
            }

            if (_selectedProperty is not null)
            {
                _selectedProperty.PropertyChanged -= OnSelectedPropertyChanged;
            }

            if (RaiseAndSetIfChanged(ref _selectedProperty, value))
            {
                if (_selectedProperty is not null)
                {
                    _selectedProperty.PropertyChanged += OnSelectedPropertyChanged;
                }

                ReloadSelectedSources();

                TogglePinCommand.RaiseCanExecuteChanged();
                CopyValueCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public void Refresh()
    {
        var selectedFullName = SelectedProperty?.FullName;
        Layout.Update(Element);
        Metadata.Update(Element);
        PropertySource.Items = PropertyInspector.BuildPropertyTree(Element, _pinnedProperties, _includeClrProperties, Filter).ToArray();

        if (selectedFullName is not null && TryFind(PropertySource.Items, selectedFullName, out var path, out var node))
        {
            Selection.SelectedIndex = path;
            SelectedProperty = node;
        }
        else
        {
            SelectedProperty = Selection.SelectedItem;
        }
    }

    public void UpdateIncludeClrProperties(bool includeClrProperties)
    {
        if (_includeClrProperties == includeClrProperties)
        {
            return;
        }

        _includeClrProperties = includeClrProperties;
        Refresh();
    }

    private void TogglePinnedSelection()
    {
        if (SelectedProperty is not { IsGroup: false })
        {
            return;
        }

        if (!_pinnedProperties.Add(SelectedProperty.FullName))
        {
            _pinnedProperties.Remove(SelectedProperty.FullName);
        }

        Refresh();
    }

    private void ReloadSelectedSources()
    {
        ValueSourceGrid.Items = (SelectedProperty?.LoadSources() ?? []).ToArray();
    }

    private void OnSelectedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PropertyGridNode.ValueText) or nameof(PropertyGridNode.PriorityText))
        {
            Layout.Update(Element);
            Metadata.Update(Element);
            ReloadSelectedSources();
        }
    }

    private static bool TryFind(IEnumerable<PropertyGridNode> roots, string fullName, out IndexPath path, out PropertyGridNode? node)
    {
        var index = 0;
        foreach (var root in roots)
        {
            if (TryFind(root, fullName, new IndexPath(index), out path, out node))
            {
                return true;
            }

            index++;
        }

        path = default;
        node = null;
        return false;
    }

    private static bool TryFind(PropertyGridNode current, string fullName, IndexPath currentPath, out IndexPath path, out PropertyGridNode? node)
    {
        if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
        {
            path = currentPath;
            node = current;
            return true;
        }

        for (var index = 0; index < current.Children.Count; index++)
        {
            if (TryFind(current.Children[index], fullName, currentPath.Append(index), out path, out node))
            {
                return true;
            }
        }

        path = default;
        node = null;
        return false;
    }
}
