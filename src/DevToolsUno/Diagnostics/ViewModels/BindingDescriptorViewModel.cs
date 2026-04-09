using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DevToolsUno.Diagnostics.ViewModels;

internal sealed class BindingDescriptorViewModel
{
    public required string BindingId { get; init; }

    public required string PropertyName { get; init; }

    public required string Kind { get; init; }

    public required string PathText { get; init; }

    public required string SourceText { get; init; }

    public required string ValueText { get; init; }

    public required string Summary { get; init; }

    public required string TargetOwnerText { get; init; }

    public required string TargetTypeText { get; init; }

    public required string ModeText { get; init; }

    public required string UpdateSourceTriggerText { get; init; }

    public string RelativeSourceText { get; init; } = "(none)";

    public string ElementNameText { get; init; } = "(none)";

    public string SourceObjectText { get; init; } = "(none)";

    public string DataContextText { get; init; } = "(none)";

    public string DataItemText { get; init; } = "(none)";

    public string ConverterText { get; init; } = "(none)";

    public string ConverterParameterText { get; init; } = "(none)";

    public string ConverterLanguageText { get; init; } = "(none)";

    public string FallbackValueText { get; init; } = "(none)";

    public string TargetNullValueText { get; init; } = "(none)";

    public string CompiledSourceText { get; init; } = "(none)";

    public string XBindPathsText { get; init; } = "(none)";

    public string HasBackChannelText { get; init; } = "False";

    public required bool IsXBind { get; init; }

    public required bool IsTemplateBinding { get; init; }

    public required DependencyObject TargetElement { get; init; }

    public required DependencyProperty TargetProperty { get; init; }

    public required BindingExpression BindingExpression { get; init; }

    public required Binding Binding { get; init; }

    public object? CurrentValue { get; init; }

    public object? SourceObject { get; init; }

    public object? RelativeSourceObject { get; init; }

    public object? ElementNameObject { get; init; }

    public object? DataContextObject { get; init; }

    public object? DataItemObject { get; init; }

    public object? ConverterObject { get; init; }

    public object? ConverterParameterObject { get; init; }

    public object? FallbackValueObject { get; init; }

    public object? TargetNullValueObject { get; init; }

    public object? CompiledSourceObject { get; init; }
}
