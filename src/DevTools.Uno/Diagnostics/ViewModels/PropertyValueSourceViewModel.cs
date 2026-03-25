namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class PropertyValueSourceViewModel : ViewModelBase
{
    public required string Name { get; init; }
    public required string Value { get; init; }
    public required string Detail { get; init; }
    public bool IsActive { get; init; }
}
