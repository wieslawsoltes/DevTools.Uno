using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevToolsUno.Sample;

public sealed class BindingShowcaseTile : Control
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(BindingShowcaseTile),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(
            nameof(Body),
            typeof(string),
            typeof(BindingShowcaseTile),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Body
    {
        get => (string)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
}
