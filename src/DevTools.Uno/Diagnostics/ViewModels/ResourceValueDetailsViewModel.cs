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

internal sealed class ResourceValueDetailsViewModel : ViewModelBase
{
    private readonly ResourceEntryViewModel _entry;
    private readonly ISet<string> _pinnedProperties = new HashSet<string>(StringComparer.Ordinal);
    private bool _includeClrProperties;
    private PropertyGridNode? _selectedProperty;

    public ResourceValueDetailsViewModel(ResourceEntryViewModel entry, bool includeClrProperties)
    {
        _entry = entry;
        _includeClrProperties = includeClrProperties;
        Filter = new FilterViewModel();
        Filter.RefreshFilter += (_, _) => Refresh();

        CopySelectedPropertyValueCommand = new RelayCommand(async () =>
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

    public string SelectedResourceKey => _entry.KeyText;

    public string SelectedResourceType => _entry.TypeText;

    public string SelectedResourceValue => _entry.ValueText;

    public string ProviderName => _entry.ProviderName;

    public string ProviderKind => _entry.ProviderKind;

    public string ProviderPath => _entry.ProviderPath;

    public string DictionarySource => _entry.DictionarySource;

    public FilterViewModel Filter { get; }

    public HierarchicalTreeDataGridSource<PropertyGridNode> PropertySource { get; }

    public TreeDataGridRowSelectionModel<PropertyGridNode> Selection { get; }

    public FlatTreeDataGridSource<PropertyValueSourceViewModel> ValueSourceGrid { get; }

    public RelayCommand CopySelectedPropertyValueCommand { get; }

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
                CopySelectedPropertyValueCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public void Refresh()
    {
        var selectedFullName = SelectedProperty?.FullName;
        PropertySource.Items = BuildPropertyNodes().ToArray();

        if (selectedFullName is not null && TryFind(PropertySource.Items, selectedFullName, out var path, out var node))
        {
            Selection.SelectedIndex = path;
            SelectedProperty = node;
        }
        else
        {
            SelectedProperty = Selection.SelectedItem;
        }

        RaiseHeaderPropertiesChanged();
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

    private IEnumerable<PropertyGridNode> BuildPropertyNodes()
    {
        var value = _entry.GetValue();
        if (value is null || IsSimpleType(value.GetType()))
        {
            yield return CreateScalarGroup();
            yield break;
        }

        var includeClrProperties = _includeClrProperties || value is not DependencyObject;
        foreach (var node in PropertyInspector.BuildPropertyTree(value, _pinnedProperties, includeClrProperties, Filter))
        {
            AttachNotify(node);
            yield return node;
        }
    }

    private PropertyGridNode CreateScalarGroup()
    {
        var group = new PropertyGridNode
        {
            Name = "Resource Value",
            ValueText = string.Empty,
            TypeText = string.Empty,
            PriorityText = string.Empty,
            SourceText = string.Empty,
            IsGroup = true,
            IsEditable = false,
            FullName = $"RESOURCE-GROUP:{_entry.ResourceId}",
        };

        group.Children.Add(new PropertyGridNode
        {
            Name = "Value",
            ValueText = _entry.ValueText,
            TypeText = _entry.TypeText,
            PriorityText = "Resource",
            SourceText = _entry.ProviderKind,
            IsGroup = false,
            IsEditable = _entry.CanEditInline,
            FullName = $"RESOURCE:{_entry.ResourceId}",
            TrySetValue = value => _entry.ApplyValue(value),
            GetSources = () =>
            [
                new PropertyValueSourceViewModel
                {
                    Name = "Resource",
                    Value = _entry.ValueText,
                    Detail = $"{_entry.ProviderPath} · {_entry.DictionarySource}",
                    IsActive = true,
                },
            ],
            GetRawValue = () => _entry.GetValue(),
            NotifyValueChanged = OnPropertyValueChanged,
        });

        return group;
    }

    private void AttachNotify(PropertyGridNode node)
    {
        node.NotifyValueChanged = OnPropertyValueChanged;
        foreach (var child in node.Children)
        {
            AttachNotify(child);
        }
    }

    private void OnPropertyValueChanged()
    {
        _entry.RefreshFromOwner();
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
            _entry.RefreshFromOwner();
            RaiseHeaderPropertiesChanged();
            ReloadSelectedSources();
        }
    }

    private void RaiseHeaderPropertiesChanged()
    {
        RaisePropertyChanged(nameof(SelectedResourceType));
        RaisePropertyChanged(nameof(SelectedResourceValue));
    }

    private static bool IsSimpleType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType.IsPrimitive ||
               actualType.IsEnum ||
               actualType == typeof(string) ||
               actualType == typeof(decimal) ||
               actualType == typeof(DateTime) ||
               actualType == typeof(DateTimeOffset) ||
               actualType == typeof(TimeSpan) ||
               actualType == typeof(Guid);
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
