using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DevToolsUno.Diagnostics.Internal;

internal static class DevToolsThemeManager
{
    private const string DevToolsGenericUri = "ms-appx:///DevToolsUno/Themes/Generic.xaml";
    private const string TreeDataGridGenericUri = "ms-appx:///TreeDataGrid.Uno/Themes/Generic.xaml";

    public static void EnsureResources()
    {
        if (Application.Current?.Resources is not { } resources)
        {
            return;
        }

        EnsureDictionary(resources, TreeDataGridGenericUri);
        EnsureDictionary(resources, DevToolsGenericUri);
    }

    public static Brush CreateBackdropBrush(ElementTheme theme)
        => new SolidColorBrush(IsLight(theme)
            ? Color.FromArgb(102, 255, 255, 255)
            : Color.FromArgb(160, 0, 0, 0));

    public static Brush CreateSurfaceBrush(ElementTheme theme)
        => new SolidColorBrush(IsLight(theme)
            ? Color.FromArgb(255, 248, 248, 251)
            : Color.FromArgb(255, 20, 20, 24));

    public static Brush CreateBorderBrush(ElementTheme theme)
        => new SolidColorBrush(IsLight(theme)
            ? Color.FromArgb(255, 209, 213, 219)
            : Color.FromArgb(255, 47, 47, 56));

    private static void EnsureDictionary(ResourceDictionary resources, string uri)
    {
        foreach (var dictionary in resources.MergedDictionaries)
        {
            if (string.Equals(dictionary.Source?.OriginalString, uri, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(uri),
        });
    }

    private static bool IsLight(ElementTheme theme)
        => theme == ElementTheme.Light;
}
