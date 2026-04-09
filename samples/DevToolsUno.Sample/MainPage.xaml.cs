using Microsoft.UI.Xaml.Controls;

namespace DevToolsUno.Sample;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public string BindingInspectorHint =>
        "Press F12 to open DevTools. Hold Ctrl+Shift to inspect controls. This sample includes DataContext Binding, ElementName binding, x:Bind, ListView bindings, and a custom TemplateBinding tile.";

    private string FormatSelectedResult(BindingSampleItem? item)
        => item is null
            ? "x:Bind selection -> none"
            : $"x:Bind selection -> {item.Title}: {item.Description}";
}
