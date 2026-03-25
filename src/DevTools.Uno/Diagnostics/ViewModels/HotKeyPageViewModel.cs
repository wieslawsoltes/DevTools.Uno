using System.Collections.ObjectModel;
using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class HotKeyPageViewModel : ViewModelBase
{
    public ObservableCollection<HotKeyEntry> Entries { get; } = [];

    public string SummaryText => "Keyboard accelerators, access keys, and the DevTools launch gesture discovered in the current inspection root.";

    public string EntryCountText => Entries.Count == 1 ? "1 gesture discovered." : $"{Entries.Count} gestures discovered.";

    public void Refresh(FrameworkElement root, DevToolsOptions options)
    {
        Entries.Clear();
        Entries.Add(new HotKeyEntry
        {
            Element = "DevTools",
            Gesture = FormatGesture(options.Gesture, options.GestureModifiers),
            Scope = "Launch gesture",
        });

        foreach (var element in Enumerate(root))
        {
            if (element is UIElement ui && ui.KeyboardAccelerators is { Count: > 0 } accelerators)
            {
                foreach (var accelerator in accelerators)
                {
                    Entries.Add(new HotKeyEntry
                    {
                        Element = Describe(element),
                        Gesture = FormatGesture(accelerator.Key, accelerator.Modifiers),
                        Scope = "KeyboardAccelerator",
                    });
                }
            }

            var accessKey = element.GetType().GetProperty("AccessKey")?.GetValue(element) as string;
            if (!string.IsNullOrWhiteSpace(accessKey))
            {
                Entries.Add(new HotKeyEntry
                {
                    Element = Describe(element),
                    Gesture = accessKey!,
                    Scope = "AccessKey",
                });
            }
        }

        RaisePropertyChanged(nameof(EntryCountText));
    }

    private static IEnumerable<DependencyObject> Enumerate(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>();
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        if (root is FrameworkElement fe)
        {
            foreach (var popupChild in TreeInspector.GetOpenPopupChildren(fe.XamlRoot))
            {
                queue.Enqueue(popupChild);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!seen.Add(current))
            {
                continue;
            }

            yield return current;

            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var index = 0; index < count; index++)
            {
                if (VisualTreeHelper.GetChild(current, index) is { } child)
                {
                    queue.Enqueue(child);
                }
            }
        }
    }

    private static string Describe(DependencyObject element)
    {
        if (element is FrameworkElement fe && !string.IsNullOrWhiteSpace(fe.Name))
        {
            return $"{element.GetType().Name}#{fe.Name}";
        }

        return element.GetType().Name;
    }

    public static string FormatGesture(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Windows))
        {
            parts.Add("Meta");
        }

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
