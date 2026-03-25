using Microsoft.UI.Xaml;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class EventRouteEntryViewModel
{
    private WeakReference<DependencyObject>? _elementReference;

    public required int StepIndex { get; init; }

    public required string StepText { get; init; }

    public required string ElementDisplayName { get; init; }

    public required string Selector { get; init; }

    public required string FlagsText { get; init; }

    public bool CanInspect => Element is not null;

    public DependencyObject? Element
    {
        get
        {
            if (_elementReference is null)
            {
                return null;
            }

            return _elementReference.TryGetTarget(out var target) ? target : null;
        }
    }

    public static EventRouteEntryViewModel Create(
        int stepIndex,
        DependencyObject? element,
        string stepText,
        string elementDisplayName,
        string selector,
        string flagsText)
        => new()
        {
            _elementReference = element is not null ? new WeakReference<DependencyObject>(element) : null,
            StepIndex = stepIndex,
            StepText = stepText,
            ElementDisplayName = elementDisplayName,
            Selector = selector,
            FlagsText = flagsText,
        };
}
