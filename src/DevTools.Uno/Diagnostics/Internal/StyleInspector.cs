using System.Reflection;
using DevTools.Uno.Diagnostics.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DevTools.Uno.Diagnostics.Internal;

internal static class StyleInspector
{
    private static readonly BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo? FrameworkElementGetActiveStyleMethod =
        typeof(FrameworkElement).GetMethod("GetActiveStyle", InstanceNonPublic);

    private static readonly MethodInfo? FrameworkElementGetTemplatedParentMethod =
        typeof(FrameworkElement).GetMethod("GetTemplatedParent", InstanceNonPublic);

    private static readonly MethodInfo? ControlGetTemplateRootMethod =
        typeof(Control).GetMethod("GetTemplateRoot", InstanceNonPublic);

    private static readonly PropertyInfo? ControlDefaultStyleKeyProperty =
        typeof(Control).GetProperty("DefaultStyleKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly PropertyInfo? DependencyPropertyNameProperty =
        typeof(DependencyProperty).GetProperty("Name", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly PropertyInfo? StateTriggerCurrentPrecedenceProperty =
        typeof(StateTriggerBase).GetProperty("CurrentPrecedence", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly PropertyInfo? SetterThemeResourceKeyProperty =
        typeof(Setter).GetProperty("ThemeResourceKey", BindingFlags.Instance | BindingFlags.NonPublic);

    public static StyleInspectionSnapshot BuildSnapshot(FrameworkElement root, DependencyObject? inspectionTarget)
    {
        var targetElement = inspectionTarget as FrameworkElement ?? root;
        var isFallbackTarget = inspectionTarget is not FrameworkElement;
        var explicitStyle = ReadExplicitStyle(targetElement);
        var activeStyle = GetActiveStyle(targetElement) ?? explicitStyle;
        var control = targetElement as Control;
        var template = control?.Template;
        var templateRoot = GetTemplateRoot(control);
        var templatedParent = GetTemplatedParent(targetElement);
        var visualStateGroups = GetVisualStateGroups(templateRoot);
        var implicitStyleMatches = FindImplicitStyleMatches(root, targetElement, activeStyle);

        var snapshot = new StyleInspectionSnapshot
        {
            Root = root,
            TargetElement = targetElement,
            IsFallbackTarget = isFallbackTarget,
            ExplicitStyle = explicitStyle,
            ActiveStyle = activeStyle,
            IsActiveStyleExplicit = explicitStyle is not null && ReferenceEquals(explicitStyle, activeStyle),
            Control = control,
            ControlTemplate = template,
            TemplateRoot = templateRoot,
            TemplatedParent = templatedParent,
            DefaultStyleKey = control is not null ? SafeRead(() => ControlDefaultStyleKeyProperty?.GetValue(control)) : null,
            VisualStateGroups = visualStateGroups,
            ImplicitStyleMatches = implicitStyleMatches,
        };

        snapshot.Nodes = BuildScopeTree(snapshot).ToArray();
        return snapshot;
    }

    public static IReadOnlyList<StyleEntryViewModel> BuildEntries(
        StyleInspectionSnapshot snapshot,
        StyleScopeNode scope,
        FilterViewModel filter,
        string sortBy,
        bool sortDescending)
    {
        IEnumerable<StyleEntryViewModel> entries = scope.Category switch
        {
            StyleScopeCategory.Overview => BuildOverviewEntries(snapshot, scope.Path),
            StyleScopeCategory.Style => BuildStyleEntries(scope),
            StyleScopeCategory.ImplicitStyleScopes => BuildImplicitStyleScopeEntries(snapshot, scope.Path),
            StyleScopeCategory.Template => BuildTemplateEntries(snapshot, scope.Path),
            StyleScopeCategory.TemplateRoot => BuildFrameworkElementEntries(snapshot.TemplateRoot, scope.Path, scope.Name, scope.Kind),
            StyleScopeCategory.VisualStates => BuildVisualStateRootEntries(snapshot, scope.Path),
            StyleScopeCategory.VisualStateGroup => BuildVisualStateGroupEntries(scope, scope.Path),
            StyleScopeCategory.VisualState => BuildVisualStateEntries(scope, scope.Path),
            StyleScopeCategory.TemplatedParent => BuildObjectEntries(snapshot.TemplatedParent, scope.Path, scope.Name, scope.Kind, scope.Parent?.Name ?? "Styles"),
            StyleScopeCategory.DefaultStyleKey => BuildObjectEntries(snapshot.DefaultStyleKey, scope.Path, scope.Name, scope.Kind, scope.Parent?.Name ?? "Styles"),
            _ => Array.Empty<StyleEntryViewModel>(),
        };

        entries = entries.Where(entry => MatchesFilter(entry, filter));
        entries = SortEntries(entries, sortBy, sortDescending);
        return entries.ToArray();
    }

    private static IEnumerable<StyleScopeNode> BuildScopeTree(StyleInspectionSnapshot snapshot)
    {
        yield return new StyleScopeNode
        {
            Name = "Selection Overview",
            Kind = "Overview",
            Category = StyleScopeCategory.Overview,
            Summary = BuildOverviewSummary(snapshot),
            Path = "Selection",
            InspectionObject = snapshot.TargetElement,
        };

        if (snapshot.ActiveStyle is { } activeStyle)
        {
            var activeNode = new StyleScopeNode
            {
                Name = "Active Style",
                Kind = snapshot.IsActiveStyleExplicit ? "Explicit Style" : "Implicit Style",
                Category = StyleScopeCategory.Style,
                Summary = $"{DescribeStyle(activeStyle)} | Source: {DescribeStyleSource(snapshot)}",
                Path = "Selection/ActiveStyle",
                InspectionObject = activeStyle,
                IncludeBasedOnSetters = true,
            };

            var chainIndex = 0;
            foreach (var linkedStyle in EnumerateStyleChainCurrentFirst(activeStyle))
            {
                activeNode.AddChild(new StyleScopeNode
                {
                    Name = chainIndex == 0 ? "Current Style" : $"BasedOn {chainIndex}",
                    Kind = "Style Link",
                    Category = StyleScopeCategory.Style,
                    Summary = DescribeStyle(linkedStyle),
                    Path = $"Selection/ActiveStyle/Chain[{chainIndex}]",
                    InspectionObject = linkedStyle,
                });
                chainIndex++;
            }

            yield return activeNode;
        }

        if (snapshot.ImplicitStyleMatches.Count > 0)
        {
            var implicitNode = new StyleScopeNode
            {
                Name = "Implicit Style Providers",
                Kind = "Providers",
                Category = StyleScopeCategory.ImplicitStyleScopes,
                Summary = $"{snapshot.ImplicitStyleMatches.Count} matching resource scope(s)",
                Path = "Selection/ImplicitProviders",
                InspectionObject = snapshot.TargetElement,
            };

            for (var index = 0; index < snapshot.ImplicitStyleMatches.Count; index++)
            {
                var match = snapshot.ImplicitStyleMatches[index];
                implicitNode.AddChild(new StyleScopeNode
                {
                    Name = match.Name,
                    Kind = match.Kind,
                    Category = StyleScopeCategory.Style,
                    Summary = $"{match.MatchedType.Name} implicit style{(match.MatchesActiveStyle ? " | Active match" : string.Empty)}",
                    Path = $"Selection/ImplicitProviders[{index}]",
                    InspectionObject = match.Style,
                    IncludeBasedOnSetters = true,
                });
            }

            yield return implicitNode;
        }

        if (snapshot.ControlTemplate is not null)
        {
            yield return new StyleScopeNode
            {
                Name = "Control Template",
                Kind = "Template",
                Category = StyleScopeCategory.Template,
                Summary = BuildTemplateSummary(snapshot),
                Path = "Selection/Template",
                InspectionObject = snapshot.ControlTemplate,
            };
        }

        if (snapshot.TemplateRoot is not null)
        {
            yield return new StyleScopeNode
            {
                Name = "Template Root",
                Kind = "Element",
                Category = StyleScopeCategory.TemplateRoot,
                Summary = ResourceInspector.FormatElementName(snapshot.TemplateRoot),
                Path = "Selection/TemplateRoot",
                InspectionObject = snapshot.TemplateRoot,
            };
        }

        if (snapshot.TemplatedParent is not null)
        {
            yield return new StyleScopeNode
            {
                Name = "Templated Parent",
                Kind = "Parent",
                Category = StyleScopeCategory.TemplatedParent,
                Summary = DescribeObject(snapshot.TemplatedParent),
                Path = "Selection/TemplatedParent",
                InspectionObject = snapshot.TemplatedParent,
            };
        }

        if (snapshot.DefaultStyleKey is not null)
        {
            yield return new StyleScopeNode
            {
                Name = "Default Style Key",
                Kind = "Key",
                Category = StyleScopeCategory.DefaultStyleKey,
                Summary = DescribeObject(snapshot.DefaultStyleKey),
                Path = "Selection/DefaultStyleKey",
                InspectionObject = snapshot.DefaultStyleKey,
            };
        }

        if (snapshot.VisualStateGroups.Count > 0)
        {
            var visualStatesNode = new StyleScopeNode
            {
                Name = "Visual States",
                Kind = "State Groups",
                Category = StyleScopeCategory.VisualStates,
                Summary = $"{snapshot.VisualStateGroups.Count} group(s)",
                Path = "Selection/VisualStates",
                InspectionObject = snapshot.TemplateRoot ?? snapshot.TargetElement,
            };

            for (var groupIndex = 0; groupIndex < snapshot.VisualStateGroups.Count; groupIndex++)
            {
                var group = snapshot.VisualStateGroups[groupIndex];
                var groupNode = new StyleScopeNode
                {
                    Name = string.IsNullOrWhiteSpace(group.Name) ? $"Group {groupIndex + 1}" : group.Name,
                    Kind = "VisualStateGroup",
                    Category = StyleScopeCategory.VisualStateGroup,
                    Summary = BuildVisualStateGroupSummary(group),
                    Path = $"Selection/VisualStates/Group[{groupIndex}]",
                    InspectionObject = group,
                };

                for (var stateIndex = 0; stateIndex < group.States.Count; stateIndex++)
                {
                    groupNode.AddChild(new StyleScopeNode
                    {
                        Name = group.States[stateIndex].Name ?? $"State {stateIndex + 1}",
                        Kind = "VisualState",
                        Category = StyleScopeCategory.VisualState,
                        Summary = BuildVisualStateSummary(group.States[stateIndex], group.CurrentState),
                        Path = $"Selection/VisualStates/Group[{groupIndex}]/State[{stateIndex}]",
                        InspectionObject = group.States[stateIndex],
                    });
                }

                visualStatesNode.AddChild(groupNode);
            }

            yield return visualStatesNode;
        }
    }

    private static IEnumerable<StyleEntryViewModel> BuildOverviewEntries(StyleInspectionSnapshot snapshot, string basePath)
    {
        var target = snapshot.TargetElement;
        yield return CreateEntry(
            $"{basePath}/Type",
            "Target Type",
            target.GetType().FullName ?? target.GetType().Name,
            "Type",
            "Selection",
            "Metadata",
            target.GetType(),
            target.GetType(),
            "Runtime type of the inspected element.");

        yield return CreateEntry(
            $"{basePath}/Selector",
            "Selector",
            InspectableNode.BuildSelector(target),
            "String",
            "Selection",
            "Metadata",
            target,
            InspectableNode.BuildSelector(target),
            "Selector-style identifier for the inspected element.");

        yield return CreateEntry(
            $"{basePath}/StyleSource",
            "Style Source",
            DescribeStyleSource(snapshot),
            "String",
            "Selection",
            "Metadata",
            (object?)snapshot.ActiveStyle ?? target,
            DescribeStyleSource(snapshot),
            "Whether the active style came from a local or implicit style lookup.");

        yield return CreateEntry(
            $"{basePath}/ActiveStyle",
            "Active Style",
            snapshot.ActiveStyle is not null ? DescribeStyle(snapshot.ActiveStyle) : "(none)",
            snapshot.ActiveStyle?.GetType().Name ?? "(null)",
            "Selection",
            "Style",
            snapshot.ActiveStyle,
            snapshot.ActiveStyle,
            "The style currently applied to the selected element.");

        yield return CreateEntry(
            $"{basePath}/RequestedTheme",
            "Requested Theme",
            target.RequestedTheme.ToString(),
            nameof(ElementTheme),
            "Selection",
            "Metadata",
            target,
            target.RequestedTheme,
            "Requested theme on the selected element.");

        yield return CreateEntry(
            $"{basePath}/ActualTheme",
            "Actual Theme",
            target.ActualTheme.ToString(),
            nameof(ElementTheme),
            "Selection",
            "Metadata",
            target,
            target.ActualTheme,
            "Effective theme currently seen by the selected element.");

        yield return CreateEntry(
            $"{basePath}/ImplicitProviders",
            "Implicit Providers",
            snapshot.ImplicitStyleMatches.Count.ToString(),
            "Count",
            "Selection",
            "Metadata",
            target,
            snapshot.ImplicitStyleMatches.Count,
            "Number of resource scopes that contributed an implicit style candidate for this element type.");

        yield return CreateEntry(
            $"{basePath}/Template",
            "Template",
            snapshot.ControlTemplate is not null ? DescribeObject(snapshot.ControlTemplate) : "(none)",
            snapshot.ControlTemplate?.GetType().Name ?? "(null)",
            "Selection",
            "Template",
            snapshot.ControlTemplate,
            snapshot.ControlTemplate,
            "The current control template on the selected control.");

        yield return CreateEntry(
            $"{basePath}/TemplateRoot",
            "Template Root",
            snapshot.TemplateRoot is not null ? ResourceInspector.FormatElementName(snapshot.TemplateRoot) : "(none)",
            snapshot.TemplateRoot?.GetType().Name ?? "(null)",
            "Selection",
            "Element",
            snapshot.TemplateRoot,
            snapshot.TemplateRoot,
            "The realized root element of the current control template.");

        yield return CreateEntry(
            $"{basePath}/DefaultStyleKey",
            "Default Style Key",
            DescribeObject(snapshot.DefaultStyleKey),
            snapshot.DefaultStyleKey?.GetType().Name ?? "(null)",
            "Selection",
            "Key",
            snapshot.DefaultStyleKey,
            snapshot.DefaultStyleKey,
            "The default style key used for theme resource lookup.");

        yield return CreateEntry(
            $"{basePath}/TemplatedParent",
            "Templated Parent",
            DescribeObject(snapshot.TemplatedParent),
            snapshot.TemplatedParent?.GetType().Name ?? "(null)",
            "Selection",
            "Parent",
            snapshot.TemplatedParent,
            snapshot.TemplatedParent,
            "The templated parent, if the selected element was materialized from a template.");

        yield return CreateEntry(
            $"{basePath}/VisualStates",
            "Visual States",
            snapshot.VisualStateGroups.Count == 0
                ? "(none)"
                : string.Join(", ", snapshot.VisualStateGroups.Select(x => $"{(string.IsNullOrWhiteSpace(x.Name) ? "<unnamed>" : x.Name)}={x.CurrentState?.Name ?? "(none)"}")),
            "VisualStateGroup[]",
            "Selection",
            "States",
            snapshot.TemplateRoot ?? target,
            snapshot.VisualStateGroups.Count,
            "Current visual state by group on the template root.");
    }

    private static IEnumerable<StyleEntryViewModel> BuildStyleEntries(StyleScopeNode scope)
    {
        if (scope.InspectionObject is not Style style)
        {
            return [];
        }

        var entries = new List<StyleEntryViewModel>
        {
            CreateEntry(
                $"{scope.Path}/TargetType",
                "Target Type",
                style.TargetType?.FullName ?? "(none)",
                "Type",
                scope.Name,
                "Metadata",
                style.TargetType,
                style.TargetType,
                "The type targeted by this style."),
            CreateEntry(
                $"{scope.Path}/BasedOn",
                "Based On",
                style.BasedOn is not null ? DescribeStyle(style.BasedOn) : "(none)",
                style.BasedOn?.GetType().Name ?? "(null)",
                scope.Name,
                "Metadata",
                style.BasedOn,
                style.BasedOn,
                "Parent style in the BasedOn chain."),
            CreateEntry(
                $"{scope.Path}/IsSealed",
                "Is Sealed",
                style.IsSealed.ToString(),
                nameof(Boolean),
                scope.Name,
                "Metadata",
                style,
                style.IsSealed,
                "Whether this style has been sealed by the framework."),
        };

        var setters = GetSetters(style, scope.IncludeBasedOnSetters);
        if (setters.Count == 0)
        {
            entries.Add(CreateEntry(
                $"{scope.Path}/Setters",
                "Setters",
                "(none)",
                "Count",
                scope.Name,
                "Metadata",
                style,
                0,
                "This style does not currently expose any setter entries."));
            return entries;
        }

        for (var index = 0; index < setters.Count; index++)
        {
            var setter = setters[index];
            var value = GetSetterValue(setter.Setter);
            entries.Add(CreateEntry(
                $"{scope.Path}/Setter[{index}]",
                GetSetterName(setter.Setter),
                FormatSetterValue(setter.Setter, value),
                value?.GetType().Name ?? "(null)",
                setter.Origin,
                "Setter",
                setter.Setter,
                value,
                $"{GetSetterName(setter.Setter)} from {setter.Origin}."));
        }

        return entries;
    }

    private static IEnumerable<StyleEntryViewModel> BuildImplicitStyleScopeEntries(StyleInspectionSnapshot snapshot, string basePath)
    {
        for (var index = 0; index < snapshot.ImplicitStyleMatches.Count; index++)
        {
            var match = snapshot.ImplicitStyleMatches[index];
            yield return CreateEntry(
                $"{basePath}/Provider[{index}]",
                match.Name,
                DescribeStyle(match.Style),
                match.Style.GetType().Name,
                match.Kind,
                "Implicit Style",
                match.Style,
                match.Style,
                $"{match.MatchedType.Name} implicit style from {match.Path}{(match.MatchesActiveStyle ? " | active match" : string.Empty)}.");
        }
    }

    private static IEnumerable<StyleEntryViewModel> BuildTemplateEntries(StyleInspectionSnapshot snapshot, string basePath)
    {
        if (snapshot.ControlTemplate is null)
        {
            return [];
        }

        return
        [
            CreateEntry(
                $"{basePath}/Template",
                "Template",
                DescribeObject(snapshot.ControlTemplate),
                snapshot.ControlTemplate.GetType().Name,
                "Template",
                "Metadata",
                snapshot.ControlTemplate,
                snapshot.ControlTemplate,
                "The control template object applied to the selected control."),
            CreateEntry(
                $"{basePath}/TargetType",
                "Target Type",
                snapshot.ControlTemplate.TargetType?.FullName ?? "(none)",
                "Type",
                "Template",
                "Metadata",
                snapshot.ControlTemplate.TargetType,
                snapshot.ControlTemplate.TargetType,
                "Template target type."),
            CreateEntry(
                $"{basePath}/TemplateRoot",
                "Template Root",
                snapshot.TemplateRoot is not null ? ResourceInspector.FormatElementName(snapshot.TemplateRoot) : "(none)",
                snapshot.TemplateRoot?.GetType().Name ?? "(null)",
                "Template",
                "Element",
                snapshot.TemplateRoot,
                snapshot.TemplateRoot,
                "Realized root element for the current template instance."),
            CreateEntry(
                $"{basePath}/VisualStateGroups",
                "Visual State Groups",
                snapshot.VisualStateGroups.Count.ToString(),
                "Count",
                "Template",
                "Metadata",
                (object?)snapshot.TemplateRoot ?? snapshot.ControlTemplate,
                snapshot.VisualStateGroups.Count,
                "Number of visual state groups discovered on the template root."),
            CreateEntry(
                $"{basePath}/CurrentStates",
                "Current States",
                snapshot.VisualStateGroups.Count == 0
                    ? "(none)"
                    : string.Join(", ", snapshot.VisualStateGroups.Select(x => $"{(string.IsNullOrWhiteSpace(x.Name) ? "<unnamed>" : x.Name)}={x.CurrentState?.Name ?? "(none)"}")),
                "String",
                "Template",
                "States",
                (object?)snapshot.TemplateRoot ?? snapshot.ControlTemplate,
                snapshot.VisualStateGroups.Count,
                "Current visual state per group on the realized template root."),
        ];
    }

    private static IEnumerable<StyleEntryViewModel> BuildVisualStateRootEntries(StyleInspectionSnapshot snapshot, string basePath)
    {
        for (var index = 0; index < snapshot.VisualStateGroups.Count; index++)
        {
            var group = snapshot.VisualStateGroups[index];
            yield return CreateEntry(
                $"{basePath}/Group[{index}]",
                string.IsNullOrWhiteSpace(group.Name) ? $"Group {index + 1}" : group.Name,
                group.CurrentState?.Name ?? "(none)",
                nameof(VisualStateGroup),
                "Visual States",
                "Group",
                group,
                group.CurrentState,
                BuildVisualStateGroupSummary(group));
        }
    }

    private static IEnumerable<StyleEntryViewModel> BuildVisualStateGroupEntries(StyleScopeNode scope, string basePath)
    {
        if (scope.InspectionObject is not VisualStateGroup group)
        {
            return [];
        }

        var entries = new List<StyleEntryViewModel>
        {
            CreateEntry(
                $"{basePath}/CurrentState",
                "Current State",
                group.CurrentState?.Name ?? "(none)",
                nameof(VisualState),
                group.Name ?? scope.Name,
                "Metadata",
                (object?)group.CurrentState ?? group,
                group.CurrentState,
                "Currently active state in this group."),
            CreateEntry(
                $"{basePath}/Transitions",
                "Transitions",
                group.Transitions.Count.ToString(),
                "Count",
                group.Name ?? scope.Name,
                "Metadata",
                group,
                group.Transitions.Count,
                "Number of visual transitions declared on this group."),
        };

        for (var index = 0; index < group.States.Count; index++)
        {
            var state = group.States[index];
            entries.Add(CreateEntry(
                $"{basePath}/State[{index}]",
                state.Name ?? $"State {index + 1}",
                ReferenceEquals(state, group.CurrentState) ? "Current" : "Inactive",
                nameof(VisualState),
                group.Name ?? scope.Name,
                "State",
                state,
                state,
                BuildVisualStateSummary(state, group.CurrentState)));
        }

        return entries;
    }

    private static IEnumerable<StyleEntryViewModel> BuildVisualStateEntries(StyleScopeNode scope, string basePath)
    {
        if (scope.InspectionObject is not VisualState state)
        {
            return [];
        }

        var entries = new List<StyleEntryViewModel>
        {
            CreateEntry(
                $"{basePath}/Storyboard",
                "Storyboard",
                state.Storyboard is not null ? state.Storyboard.GetType().Name : "(none)",
                state.Storyboard?.GetType().Name ?? "(null)",
                state.Name ?? scope.Name,
                "Metadata",
                state.Storyboard,
                state.Storyboard,
                "Storyboard attached to this visual state."),
            CreateEntry(
                $"{basePath}/Setters",
                "Setters",
                state.Setters.Count.ToString(),
                "Count",
                state.Name ?? scope.Name,
                "Metadata",
                state,
                state.Setters.Count,
                "Number of setters in this state."),
            CreateEntry(
                $"{basePath}/Triggers",
                "Triggers",
                state.StateTriggers.Count.ToString(),
                "Count",
                state.Name ?? scope.Name,
                "Metadata",
                state,
                state.StateTriggers.Count,
                "Number of state triggers backing this state."),
        };

        var setterIndex = 0;
        foreach (var setter in state.Setters.OfType<Setter>())
        {
            var value = GetSetterValue(setter);
            entries.Add(CreateEntry(
                $"{basePath}/Setter[{setterIndex}]",
                GetSetterName(setter),
                FormatSetterValue(setter, value),
                value?.GetType().Name ?? "(null)",
                state.Name ?? scope.Name,
                "Setter",
                setter,
                value,
                $"{GetSetterName(setter)} declared on visual state {state.Name}."));
            setterIndex++;
        }

        for (var triggerIndex = 0; triggerIndex < state.StateTriggers.Count; triggerIndex++)
        {
            var trigger = state.StateTriggers[triggerIndex];
            entries.Add(CreateEntry(
                $"{basePath}/Trigger[{triggerIndex}]",
                trigger.GetType().Name,
                GetTriggerStatus(trigger),
                trigger.GetType().Name,
                state.Name ?? scope.Name,
                "Trigger",
                trigger,
                trigger,
                BuildTriggerSummary(trigger)));
        }

        return entries;
    }

    private static IEnumerable<StyleEntryViewModel> BuildFrameworkElementEntries(
        FrameworkElement? element,
        string basePath,
        string name,
        string origin)
    {
        if (element is null)
        {
            return [];
        }

        var entries = new List<StyleEntryViewModel>
        {
            CreateEntry(
                $"{basePath}/Element",
                name,
                ResourceInspector.FormatElementName(element),
                element.GetType().Name,
                origin,
                "Element",
                element,
                element,
                "Framework element available from the style/template inspection surface."),
            CreateEntry(
                $"{basePath}/Selector",
                "Selector",
                InspectableNode.BuildSelector(element),
                "String",
                origin,
                "Metadata",
                element,
                InspectableNode.BuildSelector(element),
                "Selector-style identifier for this element."),
            CreateEntry(
                $"{basePath}/RequestedTheme",
                "Requested Theme",
                element.RequestedTheme.ToString(),
                nameof(ElementTheme),
                origin,
                "Metadata",
                element,
                element.RequestedTheme,
                "Requested theme on this element."),
            CreateEntry(
                $"{basePath}/ActualTheme",
                "Actual Theme",
                element.ActualTheme.ToString(),
                nameof(ElementTheme),
                origin,
                "Metadata",
                element,
                element.ActualTheme,
                "Actual theme on this element."),
            CreateEntry(
                $"{basePath}/Children",
                "Visual Children",
                VisualTreeHelper.GetChildrenCount(element).ToString(),
                "Count",
                origin,
                "Metadata",
                element,
                VisualTreeHelper.GetChildrenCount(element),
                "Number of visual children currently realized under this element."),
        };

        if (element is Control control)
        {
            entries.Add(CreateEntry(
                $"{basePath}/Style",
                "Style",
                control.Style is not null ? DescribeStyle(control.Style) : "(none)",
                control.Style?.GetType().Name ?? "(null)",
                origin,
                "Style",
                control.Style,
                control.Style,
                "Local style on this control."));
        }

        return entries;
    }

    private static IEnumerable<StyleEntryViewModel> BuildObjectEntries(
        object? value,
        string basePath,
        string name,
        string kind,
        string owner)
    {
        if (value is FrameworkElement frameworkElement)
        {
            return BuildFrameworkElementEntries(frameworkElement, basePath, name, owner);
        }

        if (value is null)
        {
            return
            [
                CreateEntry(
                    $"{basePath}/Value",
                    name,
                    "(none)",
                    "(null)",
                    owner,
                    kind,
                    null,
                    null,
                    $"No {name.ToLowerInvariant()} object is currently available.")
            ];
        }

        return
        [
            CreateEntry(
                $"{basePath}/Value",
                name,
                DescribeObject(value),
                value.GetType().Name,
                owner,
                kind,
                value,
                value,
                $"{name} object exposed by the current style or template context.")
        ];
    }

    private static bool MatchesFilter(StyleEntryViewModel entry, FilterViewModel filter)
    {
        return filter.Filter(entry.Name) ||
               filter.Filter(entry.ValueText) ||
               filter.Filter(entry.TypeText) ||
               filter.Filter(entry.OriginText) ||
               filter.Filter(entry.Kind) ||
               filter.Filter(entry.Summary);
    }

    private static IEnumerable<StyleEntryViewModel> SortEntries(IEnumerable<StyleEntryViewModel> entries, string sortBy, bool sortDescending)
    {
        return sortBy switch
        {
            "Origin" => sortDescending
                ? entries.OrderByDescending(x => x.OriginText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(x => x.OriginText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "Type" => sortDescending
                ? entries.OrderByDescending(x => x.TypeText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(x => x.TypeText, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            _ => sortDescending
                ? entries.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.OriginText, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.OriginText, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static IReadOnlyList<StyleSetterDescriptor> GetSetters(Style style, bool includeBasedOnSetters)
    {
        if (!includeBasedOnSetters)
        {
            return style.Setters
                .OfType<Setter>()
                .Select((setter, index) => new StyleSetterDescriptor(setter, "Current Style", index))
                .ToArray();
        }

        var order = 0;
        var map = new Dictionary<string, StyleSetterDescriptor>(StringComparer.Ordinal);
        foreach (var linkedStyle in EnumerateStyleChainBaseFirst(style))
        {
            foreach (var setter in linkedStyle.Style.Setters.OfType<Setter>())
            {
                map[GetSetterIdentity(setter)] = new StyleSetterDescriptor(setter, linkedStyle.Label, order++);
            }
        }

        return map.Values.OrderBy(x => x.Order).ToArray();
    }

    private static IEnumerable<(Style Style, string Label)> EnumerateStyleChainBaseFirst(Style style)
    {
        var chain = new Stack<Style>();
        for (var current = style; current is not null; current = current.BasedOn)
        {
            chain.Push(current);
        }

        var level = 0;
        while (chain.Count > 0)
        {
            var current = chain.Pop();
            yield return (current, chain.Count == 0 ? "Current Style" : level == 0 ? "Base Style" : $"BasedOn {level}");
            level++;
        }
    }

    private static IEnumerable<Style> EnumerateStyleChainCurrentFirst(Style style)
    {
        for (var current = style; current is not null; current = current.BasedOn)
        {
            yield return current;
        }
    }

    private static IReadOnlyList<ImplicitStyleMatch> FindImplicitStyleMatches(FrameworkElement root, FrameworkElement target, Style? activeStyle)
    {
        var matches = new List<ImplicitStyleMatch>();
        var keyTypes = GetStyleKeyTypes(target.GetType()).ToArray();

        if (Application.Current is { } application)
        {
            CollectImplicitStyleMatches(
                application.Resources,
                keyTypes,
                "Application",
                "Application",
                "Application",
                activeStyle,
                matches,
                new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance));
        }

        foreach (var provider in EnumerateProviders(root, target))
        {
            CollectImplicitStyleMatches(
                provider.Resources,
                keyTypes,
                ResourceInspector.FormatElementName(provider),
                ReferenceEquals(provider, target) ? "Selected Element" : "Ancestor Element",
                $"Selection/{ResourceInspector.FormatElementName(provider)}",
                activeStyle,
                matches,
                new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance));
        }

        return matches;
    }

    private static void CollectImplicitStyleMatches(
        ResourceDictionary dictionary,
        IReadOnlyList<Type> keyTypes,
        string name,
        string kind,
        string path,
        Style? activeStyle,
        List<ImplicitStyleMatch> matches,
        HashSet<ResourceDictionary> visited)
    {
        if (!visited.Add(dictionary))
        {
            return;
        }

        if (TryFindDirectImplicitStyle(dictionary, keyTypes, out var matchedType, out var style))
        {
            matches.Add(new ImplicitStyleMatch
            {
                Name = name,
                Kind = kind,
                Path = path,
                MatchedType = matchedType,
                Style = style,
                MatchesActiveStyle = activeStyle is not null && ReferenceEquals(activeStyle, style),
            });
        }

        foreach (var themeDictionary in dictionary.ThemeDictionaries)
        {
            if (themeDictionary.Value is ResourceDictionary themeResources)
            {
                CollectImplicitStyleMatches(
                    themeResources,
                    keyTypes,
                    $"{name} / {ResourceInspector.FormatKey(themeDictionary.Key)} Theme",
                    "Theme Dictionary",
                    $"{path}/Theme[{ResourceInspector.FormatKey(themeDictionary.Key)}]",
                    activeStyle,
                    matches,
                    visited);
            }
        }

        for (var index = 0; index < dictionary.MergedDictionaries.Count; index++)
        {
            CollectImplicitStyleMatches(
                dictionary.MergedDictionaries[index],
                keyTypes,
                $"{name} / Merged {index + 1}",
                "Merged Dictionary",
                $"{path}/Merged[{index}]",
                activeStyle,
                matches,
                visited);
        }

        foreach (var pair in dictionary)
        {
            if (pair.Value is ResourceDictionary nestedDictionary)
            {
                CollectImplicitStyleMatches(
                    nestedDictionary,
                    keyTypes,
                    $"{name} / {ResourceInspector.FormatKey(pair.Key)}",
                    "Resource Dictionary",
                    $"{path}/Resource[{ResourceInspector.FormatKey(pair.Key)}]",
                    activeStyle,
                    matches,
                    visited);
            }
        }

        visited.Remove(dictionary);
    }

    private static bool TryFindDirectImplicitStyle(
        ResourceDictionary dictionary,
        IReadOnlyList<Type> keyTypes,
        out Type matchedType,
        out Style style)
    {
        foreach (var keyType in keyTypes)
        {
            foreach (var pair in dictionary)
            {
                if (pair.Value is Style candidate && MatchesStyleKey(pair.Key, keyType))
                {
                    matchedType = keyType;
                    style = candidate;
                    return true;
                }
            }
        }

        matchedType = typeof(FrameworkElement);
        style = null!;
        return false;
    }

    private static IEnumerable<FrameworkElement> EnumerateProviders(FrameworkElement root, FrameworkElement target)
    {
        var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        for (DependencyObject? current = target; current is not null && seen.Add(current); current = GetParent(current))
        {
            if (current is FrameworkElement provider)
            {
                yield return provider;
                if (ReferenceEquals(provider, root))
                {
                    yield break;
                }
            }
        }

        if (!seen.Contains(root))
        {
            yield return root;
        }
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkElement frameworkElement && frameworkElement.Parent is DependencyObject explicitParent)
        {
            return explicitParent;
        }

        return VisualTreeHelper.GetParent(current);
    }

    private static IReadOnlyList<Type> GetStyleKeyTypes(Type type)
    {
        var result = new List<Type>();
        for (var current = type; current is not null && typeof(FrameworkElement).IsAssignableFrom(current); current = current.BaseType)
        {
            result.Add(current);
        }

        return result;
    }

    private static Style? ReadExplicitStyle(FrameworkElement element)
    {
        var localValue = element.ReadLocalValue(FrameworkElement.StyleProperty);
        return localValue is Style style ? style : null;
    }

    private static Style? GetActiveStyle(FrameworkElement element)
        => SafeRead(() => FrameworkElementGetActiveStyleMethod?.Invoke(element, null) as Style);

    internal static DependencyObject? GetTemplatedParent(FrameworkElement element)
        => SafeRead(() => FrameworkElementGetTemplatedParentMethod?.Invoke(element, null) as DependencyObject);

    private static FrameworkElement? GetTemplateRoot(Control? control)
    {
        if (control is null)
        {
            return null;
        }

        return SafeRead(() => ControlGetTemplateRootMethod?.Invoke(control, null) as FrameworkElement);
    }

    private static IReadOnlyList<VisualStateGroup> GetVisualStateGroups(FrameworkElement? templateRoot)
    {
        if (templateRoot is null)
        {
            return [];
        }

        try
        {
            return VisualStateManager.GetVisualStateGroups(templateRoot).OfType<VisualStateGroup>().ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static bool MatchesStyleKey(object? key, Type targetType)
    {
        if (ReferenceEquals(key, targetType) || Equals(key, targetType))
        {
            return true;
        }

        var keyText = key?.ToString();
        return string.Equals(keyText, targetType.FullName, StringComparison.Ordinal) ||
               string.Equals(keyText, targetType.Name, StringComparison.Ordinal);
    }

    private static string BuildOverviewSummary(StyleInspectionSnapshot snapshot)
    {
        var parts = new List<string>
        {
            ResourceInspector.FormatElementName(snapshot.TargetElement),
            $"Style: {DescribeStyleSource(snapshot)}",
        };

        if (snapshot.ControlTemplate is not null)
        {
            parts.Add($"Template: {snapshot.ControlTemplate.GetType().Name}");
        }

        if (snapshot.VisualStateGroups.Count > 0)
        {
            parts.Add($"States: {snapshot.VisualStateGroups.Count}");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildTemplateSummary(StyleInspectionSnapshot snapshot)
    {
        var parts = new List<string>
        {
            snapshot.ControlTemplate is not null ? DescribeObject(snapshot.ControlTemplate) : "(none)",
        };

        if (snapshot.TemplateRoot is not null)
        {
            parts.Add($"Root: {ResourceInspector.FormatElementName(snapshot.TemplateRoot)}");
        }

        if (snapshot.VisualStateGroups.Count > 0)
        {
            parts.Add($"{snapshot.VisualStateGroups.Count} state group(s)");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildVisualStateGroupSummary(VisualStateGroup group)
    {
        return $"{group.States.Count} state(s) | Current: {group.CurrentState?.Name ?? "(none)"} | Transitions: {group.Transitions.Count}";
    }

    private static string BuildVisualStateSummary(VisualState state, VisualState? currentState)
    {
        var parts = new List<string>
        {
            ReferenceEquals(state, currentState) ? "Current" : "Inactive",
            $"{state.Setters.Count} setter(s)",
            $"{state.StateTriggers.Count} trigger(s)",
        };

        if (state.Storyboard is not null)
        {
            parts.Add("Storyboard");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildTriggerSummary(StateTriggerBase trigger)
    {
        var precedence = GetTriggerStatus(trigger);
        return trigger switch
        {
            AdaptiveTrigger adaptiveTrigger => $"Adaptive trigger | MinWidth: {adaptiveTrigger.MinWindowWidth:0.#} | MinHeight: {adaptiveTrigger.MinWindowHeight:0.#} | {precedence}",
            _ => $"{trigger.GetType().Name} | {precedence}",
        };
    }

    private static string GetTriggerStatus(StateTriggerBase trigger)
        => SafeRead(() => StateTriggerCurrentPrecedenceProperty?.GetValue(trigger)?.ToString()) ?? "(unknown)";

    private static string DescribeStyleSource(StyleInspectionSnapshot snapshot)
    {
        if (snapshot.ActiveStyle is null)
        {
            return "None";
        }

        return snapshot.IsActiveStyleExplicit ? "Explicit" : "Implicit";
    }

    private static string DescribeStyle(Style style)
    {
        var targetType = style.TargetType?.Name ?? "FrameworkElement";
        return $"{targetType} | {style.Setters.Count} setter(s)";
    }

    private static string DescribeObject(object? value)
    {
        return value switch
        {
            null => "(none)",
            FrameworkElement element => ResourceInspector.FormatElementName(element),
            Style style => DescribeStyle(style),
            Type type => type.FullName ?? type.Name,
            _ => PropertyInspector.FormatShallowValue(value),
        };
    }

    private static string GetSetterName(Setter setter)
    {
        if (setter.Property is { } property)
        {
            return SafeRead(() => DependencyPropertyNameProperty?.GetValue(property)?.ToString())
                   ?? property.ToString()
                   ?? nameof(DependencyProperty);
        }

        if (setter.Target?.Path?.Path is { } targetPath && !string.IsNullOrWhiteSpace(targetPath))
        {
            return targetPath;
        }

        return "Setter";
    }

    private static string GetSetterIdentity(Setter setter)
    {
        if (setter.Property is { } property)
        {
            return SafeRead(() => DependencyPropertyNameProperty?.GetValue(property)?.ToString())
                   ?? property.ToString()
                   ?? nameof(DependencyProperty);
        }

        return setter.Target?.Path?.Path ?? $"setter:{setter.GetHashCode()}";
    }

    private static object? GetSetterValue(Setter setter)
        => SafeRead(() => setter.Value);

    private static string FormatSetterValue(Setter setter, object? value)
    {
        var text = PropertyInspector.FormatShallowValue(value);
        var themeResourceKey = SafeRead(() => SetterThemeResourceKeyProperty?.GetValue(setter));
        if (themeResourceKey is not null)
        {
            return $"{text} | ThemeResource: {themeResourceKey}";
        }

        return text;
    }

    private static StyleEntryViewModel CreateEntry(
        string entryId,
        string name,
        string valueText,
        string typeText,
        string originText,
        string kind,
        object? inspectionObject,
        object? rawValue,
        string summary)
    {
        return new StyleEntryViewModel
        {
            EntryId = entryId,
            Name = name,
            ValueText = valueText,
            TypeText = typeText,
            OriginText = originText,
            Kind = kind,
            InspectionObject = inspectionObject,
            RawValue = rawValue,
            Summary = summary,
        };
    }

    private static T? SafeRead<T>(Func<T?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return default;
        }
    }

    private sealed record StyleSetterDescriptor(Setter Setter, string Origin, int Order);
}

internal sealed class StyleInspectionSnapshot
{
    public required FrameworkElement Root { get; init; }

    public required FrameworkElement TargetElement { get; init; }

    public required bool IsFallbackTarget { get; init; }

    public Style? ExplicitStyle { get; init; }

    public Style? ActiveStyle { get; init; }

    public required bool IsActiveStyleExplicit { get; init; }

    public Control? Control { get; init; }

    public ControlTemplate? ControlTemplate { get; init; }

    public FrameworkElement? TemplateRoot { get; init; }

    public DependencyObject? TemplatedParent { get; init; }

    public object? DefaultStyleKey { get; init; }

    public required IReadOnlyList<VisualStateGroup> VisualStateGroups { get; init; }

    public required IReadOnlyList<ImplicitStyleMatch> ImplicitStyleMatches { get; init; }

    public IReadOnlyList<StyleScopeNode> Nodes { get; set; } = [];
}

internal sealed class ImplicitStyleMatch
{
    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required string Path { get; init; }

    public required Type MatchedType { get; init; }

    public required Style Style { get; init; }

    public required bool MatchesActiveStyle { get; init; }
}
