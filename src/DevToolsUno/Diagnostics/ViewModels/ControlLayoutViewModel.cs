using System;
using System.Collections.Generic;
using System.Reflection;
using DevToolsUno.Diagnostics.Internal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace DevToolsUno.Diagnostics.ViewModels;

internal sealed class ControlLayoutViewModel : ViewModelBase
{
    private readonly Action _refreshRequested;

    private string _actualSize = string.Empty;
    private string _layoutSlot = string.Empty;
    private string _widthConstraint = "Unconstrained";
    private string _heightConstraint = "Unconstrained";
    private string _alignment = string.Empty;
    private string _visibility = string.Empty;
    private string _opacity = string.Empty;
    private string _contentSize = string.Empty;
    private string _marginSummary = string.Empty;
    private string _paddingSummary = string.Empty;
    private string _borderSummary = string.Empty;
    private Thickness _marginThickness;
    private Thickness _paddingThickness;
    private Thickness _borderThickness;
    private Thickness _marginVisualizerThickness;
    private Thickness _paddingVisualizerThickness;
    private Thickness _borderVisualizerThickness;
    private bool _hasPadding;
    private bool _hasBorder;
    private PropertyGridNode _marginEditor;
    private PropertyGridNode _paddingEditor;
    private PropertyGridNode _borderThicknessEditor;
    private PropertyGridNode _horizontalAlignmentEditor;
    private PropertyGridNode _verticalAlignmentEditor;

    public ControlLayoutViewModel(Action refreshRequested)
    {
        _refreshRequested = refreshRequested;
        _marginEditor = CreateUnavailableNode("Margin", "FrameworkElement.Margin", "Select an element to inspect layout.", "Layout.Margin");
        _paddingEditor = CreateUnavailableNode("Padding", "Element.Padding", "Select an element to inspect layout.", "Layout.Padding");
        _borderThicknessEditor = CreateUnavailableNode("Border thickness", "Element.BorderThickness", "Select an element to inspect layout.", "Layout.BorderThickness");
        _horizontalAlignmentEditor = CreateUnavailableNode("Horizontal alignment", "FrameworkElement.HorizontalAlignment", "Select an element to inspect layout.", "Layout.HorizontalAlignment");
        _verticalAlignmentEditor = CreateUnavailableNode("Vertical alignment", "FrameworkElement.VerticalAlignment", "Select an element to inspect layout.", "Layout.VerticalAlignment");
    }

    public string ActualSize
    {
        get => _actualSize;
        private set => RaiseAndSetIfChanged(ref _actualSize, value);
    }

    public string LayoutSlot
    {
        get => _layoutSlot;
        private set => RaiseAndSetIfChanged(ref _layoutSlot, value);
    }

    public string WidthConstraint
    {
        get => _widthConstraint;
        private set => RaiseAndSetIfChanged(ref _widthConstraint, value);
    }

    public string HeightConstraint
    {
        get => _heightConstraint;
        private set => RaiseAndSetIfChanged(ref _heightConstraint, value);
    }

    public string Alignment
    {
        get => _alignment;
        private set => RaiseAndSetIfChanged(ref _alignment, value);
    }

    public string Visibility
    {
        get => _visibility;
        private set => RaiseAndSetIfChanged(ref _visibility, value);
    }

    public string Opacity
    {
        get => _opacity;
        private set => RaiseAndSetIfChanged(ref _opacity, value);
    }

    public string ContentSize
    {
        get => _contentSize;
        private set => RaiseAndSetIfChanged(ref _contentSize, value);
    }

    public string MarginSummary
    {
        get => _marginSummary;
        private set => RaiseAndSetIfChanged(ref _marginSummary, value);
    }

    public string PaddingSummary
    {
        get => _paddingSummary;
        private set => RaiseAndSetIfChanged(ref _paddingSummary, value);
    }

    public string BorderSummary
    {
        get => _borderSummary;
        private set => RaiseAndSetIfChanged(ref _borderSummary, value);
    }

    public Thickness MarginThickness
    {
        get => _marginThickness;
        private set => RaiseAndSetIfChanged(ref _marginThickness, value);
    }

    public Thickness PaddingThickness
    {
        get => _paddingThickness;
        private set => RaiseAndSetIfChanged(ref _paddingThickness, value);
    }

    public Thickness BorderThickness
    {
        get => _borderThickness;
        private set => RaiseAndSetIfChanged(ref _borderThickness, value);
    }

    public Thickness MarginVisualizerThickness
    {
        get => _marginVisualizerThickness;
        private set => RaiseAndSetIfChanged(ref _marginVisualizerThickness, value);
    }

    public Thickness PaddingVisualizerThickness
    {
        get => _paddingVisualizerThickness;
        private set => RaiseAndSetIfChanged(ref _paddingVisualizerThickness, value);
    }

    public Thickness BorderVisualizerThickness
    {
        get => _borderVisualizerThickness;
        private set => RaiseAndSetIfChanged(ref _borderVisualizerThickness, value);
    }

    public bool HasPadding
    {
        get => _hasPadding;
        private set => RaiseAndSetIfChanged(ref _hasPadding, value);
    }

    public bool HasBorder
    {
        get => _hasBorder;
        private set => RaiseAndSetIfChanged(ref _hasBorder, value);
    }

    public PropertyGridNode MarginEditor
    {
        get => _marginEditor;
        private set => RaiseAndSetIfChanged(ref _marginEditor, value);
    }

    public PropertyGridNode PaddingEditor
    {
        get => _paddingEditor;
        private set => RaiseAndSetIfChanged(ref _paddingEditor, value);
    }

    public PropertyGridNode BorderThicknessEditor
    {
        get => _borderThicknessEditor;
        private set => RaiseAndSetIfChanged(ref _borderThicknessEditor, value);
    }

    public PropertyGridNode HorizontalAlignmentEditor
    {
        get => _horizontalAlignmentEditor;
        private set => RaiseAndSetIfChanged(ref _horizontalAlignmentEditor, value);
    }

    public PropertyGridNode VerticalAlignmentEditor
    {
        get => _verticalAlignmentEditor;
        private set => RaiseAndSetIfChanged(ref _verticalAlignmentEditor, value);
    }

    public void Update(DependencyObject? element)
    {
        if (element is not FrameworkElement fe)
        {
            Reset();
            return;
        }

        var paddingAccessor = TryCreateThicknessAccessor(element, "Padding");
        var borderThicknessAccessor = TryCreateThicknessAccessor(element, "BorderThickness");
        var slot = LayoutInformation.GetLayoutSlot(fe);

        HasPadding = paddingAccessor is not null;
        HasBorder = borderThicknessAccessor is not null;

        MarginThickness = fe.Margin;
        PaddingThickness = paddingAccessor?.GetValue() ?? default;
        BorderThickness = borderThicknessAccessor?.GetValue() ?? default;

        MarginVisualizerThickness = ClampForVisualizer(MarginThickness);
        PaddingVisualizerThickness = HasPadding ? ClampForVisualizer(PaddingThickness) : default;
        BorderVisualizerThickness = HasBorder ? ClampForVisualizer(BorderThickness) : default;

        ActualSize = FormatSize(fe.ActualWidth, fe.ActualHeight);
        LayoutSlot = $"{slot.X:0.#}, {slot.Y:0.#}, {slot.Width:0.#}, {slot.Height:0.#}";
        WidthConstraint = FormatConstraint(fe.MinWidth, fe.MaxWidth);
        HeightConstraint = FormatConstraint(fe.MinHeight, fe.MaxHeight);
        Alignment = $"{fe.HorizontalAlignment} / {fe.VerticalAlignment}";
        Visibility = fe.Visibility.ToString();
        Opacity = fe.Opacity.ToString("0.###");
        ContentSize = FormatContentSize(fe.ActualWidth, fe.ActualHeight, BorderThickness, PaddingThickness);
        MarginSummary = FormatThickness(MarginThickness);
        PaddingSummary = HasPadding ? FormatThickness(PaddingThickness) : "Not supported by this control";
        BorderSummary = HasBorder ? FormatThickness(BorderThickness) : "Not supported by this control";

        MarginEditor = CreateEditableNode(
            "Margin",
            "FrameworkElement.Margin",
            "Layout.Margin",
            typeof(Thickness),
            () => fe.Margin,
            value => fe.Margin = value);

        PaddingEditor = paddingAccessor is not null
            ? CreateEditableNode(
                "Padding",
                paddingAccessor.SourceText,
                "Layout.Padding",
                typeof(Thickness),
                paddingAccessor.GetValue,
                paddingAccessor.SetValue)
            : CreateUnavailableNode("Padding", $"{element.GetType().Name}.Padding", "Not supported by this element.", "Layout.Padding");

        BorderThicknessEditor = borderThicknessAccessor is not null
            ? CreateEditableNode(
                "Border thickness",
                borderThicknessAccessor.SourceText,
                "Layout.BorderThickness",
                typeof(Thickness),
                borderThicknessAccessor.GetValue,
                borderThicknessAccessor.SetValue)
            : CreateUnavailableNode("Border thickness", $"{element.GetType().Name}.BorderThickness", "Not supported by this element.", "Layout.BorderThickness");

        HorizontalAlignmentEditor = CreateEditableNode(
            "Horizontal alignment",
            "FrameworkElement.HorizontalAlignment",
            "Layout.HorizontalAlignment",
            typeof(HorizontalAlignment),
            () => fe.HorizontalAlignment,
            value => fe.HorizontalAlignment = value);

        VerticalAlignmentEditor = CreateEditableNode(
            "Vertical alignment",
            "FrameworkElement.VerticalAlignment",
            "Layout.VerticalAlignment",
            typeof(VerticalAlignment),
            () => fe.VerticalAlignment,
            value => fe.VerticalAlignment = value);
    }

    private void Reset()
    {
        ActualSize = string.Empty;
        LayoutSlot = string.Empty;
        WidthConstraint = "Unconstrained";
        HeightConstraint = "Unconstrained";
        Alignment = string.Empty;
        Visibility = string.Empty;
        Opacity = string.Empty;
        ContentSize = string.Empty;
        MarginSummary = string.Empty;
        PaddingSummary = string.Empty;
        BorderSummary = string.Empty;
        MarginThickness = default;
        PaddingThickness = default;
        BorderThickness = default;
        MarginVisualizerThickness = default;
        PaddingVisualizerThickness = default;
        BorderVisualizerThickness = default;
        HasPadding = false;
        HasBorder = false;
        MarginEditor = CreateUnavailableNode("Margin", "FrameworkElement.Margin", "Select an element to inspect layout.", "Layout.Margin");
        PaddingEditor = CreateUnavailableNode("Padding", "Element.Padding", "Select an element to inspect layout.", "Layout.Padding");
        BorderThicknessEditor = CreateUnavailableNode("Border thickness", "Element.BorderThickness", "Select an element to inspect layout.", "Layout.BorderThickness");
        HorizontalAlignmentEditor = CreateUnavailableNode("Horizontal alignment", "FrameworkElement.HorizontalAlignment", "Select an element to inspect layout.", "Layout.HorizontalAlignment");
        VerticalAlignmentEditor = CreateUnavailableNode("Vertical alignment", "FrameworkElement.VerticalAlignment", "Select an element to inspect layout.", "Layout.VerticalAlignment");
    }

    private PropertyGridNode CreateEditableNode<T>(
        string name,
        string sourceText,
        string fullName,
        Type propertyType,
        Func<T> getter,
        Action<T> setter)
    {
        return new PropertyGridNode
        {
            Name = name,
            FullName = fullName,
            TypeText = propertyType.Name,
            ValueText = PropertyInspector.FormatValue(getter()),
            SourceText = sourceText,
            IsEditable = true,
            Editor = PropertyInspector.GetEditorMetadata(propertyType, getter(), isEditable: true),
            GetRawValue = () => getter(),
            GetSources = () =>
            [
                new PropertyValueSourceViewModel
                {
                    Name = sourceText,
                    Value = PropertyInspector.FormatValue(getter()),
                    Detail = "Layout explorer",
                    IsActive = true,
                },
            ],
            TrySetValue = value =>
            {
                if (!PropertyInspector.TryConvertValue(propertyType, value, out var converted, out var error))
                {
                    return PropertyEditorCommitResult.Failed(error);
                }

                if (converted is not T typedValue)
                {
                    return PropertyEditorCommitResult.Failed($"Expected a {propertyType.Name} value.");
                }

                try
                {
                    setter(typedValue);
                    return PropertyEditorCommitResult.Applied();
                }
                catch (Exception ex)
                {
                    return PropertyEditorCommitResult.Failed(ex.GetBaseException().Message);
                }
            },
            NotifyValueChanged = _refreshRequested,
        };
    }

    private static PropertyGridNode CreateUnavailableNode(string name, string sourceText, string message, string fullName)
    {
        return new PropertyGridNode
        {
            Name = name,
            FullName = fullName,
            TypeText = "Unavailable",
            ValueText = message,
            SourceText = sourceText,
            IsEditable = false,
            Editor = PropertyEditorMetadata.ReadOnly,
            GetRawValue = () => null,
            GetSources = () =>
            [
                new PropertyValueSourceViewModel
                {
                    Name = sourceText,
                    Value = message,
                    Detail = "Layout explorer",
                    IsActive = true,
                },
            ],
        };
    }

    private static string FormatConstraint(double min, double max)
    {
        var parts = new List<string>(2);
        if (min > 0)
        {
            parts.Add($"Min {min:0.#}");
        }

        if (!double.IsInfinity(max))
        {
            parts.Add($"Max {max:0.#}");
        }

        return parts.Count == 0 ? "Unconstrained" : string.Join(" • ", parts);
    }

    private static string FormatContentSize(double width, double height, Thickness border, Thickness padding)
    {
        var contentWidth = Math.Max(0, width - border.Left - border.Right - padding.Left - padding.Right);
        var contentHeight = Math.Max(0, height - border.Top - border.Bottom - padding.Top - padding.Bottom);
        return FormatSize(contentWidth, contentHeight);
    }

    private static string FormatSize(double width, double height)
        => $"{width:0.#} x {height:0.#}";

    private static string FormatThickness(Thickness thickness)
        => $"{thickness.Left:0.#}, {thickness.Top:0.#}, {thickness.Right:0.#}, {thickness.Bottom:0.#}";

    private static Thickness ClampForVisualizer(Thickness thickness)
    {
        return new Thickness(
            ClampSide(thickness.Left),
            ClampSide(thickness.Top),
            ClampSide(thickness.Right),
            ClampSide(thickness.Bottom));
    }

    private static double ClampSide(double value)
        => Math.Min(48, Math.Max(0, value));

    private static ThicknessPropertyAccessor? TryCreateThicknessAccessor(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.PropertyType != typeof(Thickness) ||
            property.GetMethod is null ||
            property.SetMethod is null)
        {
            return null;
        }

        return new ThicknessPropertyAccessor(
            $"{property.DeclaringType?.Name ?? target.GetType().Name}.{property.Name}",
            () => property.GetValue(target) is Thickness thickness ? thickness : default,
            value => property.SetValue(target, value));
    }

    private sealed record ThicknessPropertyAccessor(
        string SourceText,
        Func<Thickness> GetValue,
        Action<Thickness> SetValue);
}
