using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using DevTools.Uno.Diagnostics.ViewModels;
using AGridLength = Avalonia.GridLength;
using AGridUnitType = Avalonia.GridUnitType;

namespace DevTools.Uno.Diagnostics.Internal;

internal static class PropertyValueSourceGridBuilder
{
    public static FlatTreeDataGridSource<PropertyValueSourceViewModel> Create()
    {
        var source = new FlatTreeDataGridSource<PropertyValueSourceViewModel>(Array.Empty<PropertyValueSourceViewModel>());
        source.Columns.Add(new TextColumn<PropertyValueSourceViewModel, string>("Source", x => x.IsActive ? $"{x.Name} (Active)" : x.Name, new AGridLength(1.15, AGridUnitType.Star)));
        source.Columns.Add(new TextColumn<PropertyValueSourceViewModel, string>("Value", x => x.Value, new AGridLength(1.35, AGridUnitType.Star)));
        source.Columns.Add(new TextColumn<PropertyValueSourceViewModel, string>("Detail", x => x.Detail, new AGridLength(2.1, AGridUnitType.Star)));
        return source;
    }
}
