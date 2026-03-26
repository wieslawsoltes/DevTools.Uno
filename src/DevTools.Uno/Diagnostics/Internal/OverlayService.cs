using System.Diagnostics;
using DevTools.Uno.Diagnostics.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace DevTools.Uno.Diagnostics.Internal;

internal sealed class OverlayService : IDisposable
{
    private readonly FrameworkElement _root;
    private readonly DevToolsOptions _options;
    private readonly Popup _popup;
    private readonly Canvas _canvas;
    private readonly Rectangle _treeHoverRect;
    private readonly Rectangle _inspectionRect;
    private readonly Rectangle _selectionRect;
    private readonly Rectangle _focusRect;
    private readonly Grid _layoutAdorner;
    private readonly Border _marginBorder;
    private readonly Border _paddingBorder;
    private readonly Border _contentBorder;
    private readonly Border _treeHoverBadge;
    private readonly TextBlock _treeHoverText;
    private readonly Border _inspectionBadge;
    private readonly TextBlock _inspectionText;
    private readonly Border _selectionBadge;
    private readonly TextBlock _selectionText;
    private readonly Border _focusBadge;
    private readonly TextBlock _focusText;
    private readonly Border _fpsBadge;
    private readonly TextBlock _fpsText;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();

    private XamlRoot? _attachedXamlRoot;
    private string? _treeHoverLabel;
    private DependencyObject? _treeHoverElement;
    private DependencyObject? _inspectionElement;
    private DependencyObject? _selectedElement;
    private DependencyObject? _focusedElement;
    private int _frameCount;
    private bool _showFps;
    private bool _showFocus;
    private bool _showMarginPadding;

    public OverlayService(FrameworkElement root, DevToolsOptions options)
    {
        _root = root;
        _options = options;

        _canvas = new Canvas
        {
            IsHitTestVisible = false,
            Width = root.XamlRoot?.Size.Width ?? root.ActualWidth,
            Height = root.XamlRoot?.Size.Height ?? root.ActualHeight,
        };

        var treeHoverStroke = new SolidColorBrush(Colors.Orange);
        _treeHoverRect = CreateRectangle(treeHoverStroke, 2, [6, 2], CreateFill(treeHoverStroke, 0.1));
        var inspectionStroke = options.InspectionHighlighterBrush ?? new SolidColorBrush(Colors.LimeGreen);
        _inspectionRect = CreateRectangle(inspectionStroke, 3, [8, 3], CreateFill(inspectionStroke, 0.08));
        _selectionRect = CreateRectangle(options.SelectionHighlighterBrush ?? new SolidColorBrush(Colors.DeepSkyBlue), 2, [4, 2]);
        _focusRect = CreateRectangle(options.FocusHighlighterBrush ?? new SolidColorBrush(Colors.Crimson), 2, [2, 2]);
        _contentBorder = new Border
        {
            Background = new SolidColorBrush(Colors.CornflowerBlue) { Opacity = 0.3 },
            IsHitTestVisible = false,
        };
        _paddingBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Colors.LimeGreen) { Opacity = 0.45 },
            IsHitTestVisible = false,
        };
        _marginBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Gold) { Opacity = 0.45 },
            IsHitTestVisible = false,
        };
        _layoutAdorner = new Grid
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        _layoutAdorner.Children.Add(_contentBorder);
        _layoutAdorner.Children.Add(_paddingBorder);
        _layoutAdorner.Children.Add(_marginBorder);
        _treeHoverText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.Black),
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
        };
        _treeHoverBadge = new Border
        {
            Background = new SolidColorBrush(Colors.Gold) { Opacity = 0.95 },
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(8),
            Child = _treeHoverText,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        _inspectionText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
        };
        _inspectionBadge = new Border
        {
            Background = new SolidColorBrush(Colors.Black) { Opacity = 0.85 },
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(8),
            Child = _inspectionText,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        _selectionText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
        };
        _selectionBadge = new Border
        {
            Background = new SolidColorBrush(Colors.DeepSkyBlue) { Opacity = 0.95 },
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(8),
            Child = _selectionText,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        _focusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
        };
        _focusBadge = new Border
        {
            Background = new SolidColorBrush(Colors.Crimson) { Opacity = 0.95 },
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(8),
            Child = _focusText,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

        _fpsText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
        };

        _fpsBadge = new Border
        {
            Background = new SolidColorBrush(Colors.Black) { Opacity = 0.75 },
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(8),
            Child = _fpsText,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

        _canvas.Children.Add(_layoutAdorner);
        _canvas.Children.Add(_selectionRect);
        _canvas.Children.Add(_treeHoverRect);
        _canvas.Children.Add(_inspectionRect);
        _canvas.Children.Add(_focusRect);
        _canvas.Children.Add(_treeHoverBadge);
        _canvas.Children.Add(_inspectionBadge);
        _canvas.Children.Add(_selectionBadge);
        _canvas.Children.Add(_focusBadge);
        _canvas.Children.Add(_fpsBadge);

        _popup = new Popup
        {
            IsLightDismissEnabled = false,
            Child = _canvas,
            IsOpen = true,
        };

        root.SizeChanged += OnRootSizeChanged;
        root.LayoutUpdated += OnLayoutUpdated;
        SyncXamlRoot();
        UpdateAll();
    }

    public bool ShowFocus
    {
        get => _showFocus;
        set
        {
            _showFocus = value;
            UpdateAll();
        }
    }

    public bool ShowMarginPadding
    {
        get => _showMarginPadding;
        set
        {
            _showMarginPadding = value;
            UpdateAll();
        }
    }

    public bool ShowFps
    {
        get => _showFps;
        set
        {
            if (_showFps == value)
            {
                return;
            }

            _showFps = value;
            if (value)
            {
                CompositionTarget.Rendering += OnRendering;
                _fpsBadge.Visibility = Visibility.Visible;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
                _fpsBadge.Visibility = Visibility.Collapsed;
            }
        }
    }

    public void SetSelection(DependencyObject? element)
    {
        _selectedElement = element;
        UpdateAll();
    }

    public void SetTreeHoverTarget(DependencyObject? element, string? label)
    {
        _treeHoverElement = element;
        _treeHoverLabel = label;
        UpdateAll();
    }

    public void SetInspectionTarget(DependencyObject? element)
    {
        _inspectionElement = element;
        UpdateAll();
    }

    public void SetFocusedElement(DependencyObject? element)
    {
        _focusedElement = element;
        UpdateAll();
    }

    public bool IsOwnElement(DependencyObject? element)
    {
        if (_popup.Child is null || element is null)
        {
            return false;
        }

        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, _popup.Child))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        CompositionTarget.Rendering -= OnRendering;
        _root.SizeChanged -= OnRootSizeChanged;
        _root.LayoutUpdated -= OnLayoutUpdated;
        if (_attachedXamlRoot is not null)
        {
            _attachedXamlRoot.Changed -= OnXamlRootChanged;
            _attachedXamlRoot = null;
        }

        _popup.IsOpen = false;
    }

    private static Rectangle CreateRectangle(Brush stroke, double thickness, DoubleCollection dashArray, Brush? fill = null)
        => new()
        {
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeDashArray = dashArray,
            Fill = fill,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

    private static Brush? CreateFill(Brush stroke, double opacity)
    {
        return stroke is SolidColorBrush solid
            ? new SolidColorBrush(solid.Color) { Opacity = opacity }
            : null;
    }

    private void OnLayoutUpdated(object? sender, object e)
    {
        SyncXamlRoot();
        UpdateAll();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SyncXamlRoot();
        _canvas.Width = e.NewSize.Width;
        _canvas.Height = e.NewSize.Height;
        UpdateAll();
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        UpdateCanvasSize();
        UpdateAll();
    }

    private void OnRendering(object? sender, object e)
    {
        _frameCount++;
        var elapsed = _fpsStopwatch.Elapsed;
        if (elapsed < TimeSpan.FromSeconds(1))
        {
            return;
        }

        var fps = _frameCount / elapsed.TotalSeconds;
        _fpsText.Text = $"FPS {fps:0.0}";
        Canvas.SetLeft(_fpsBadge, 12);
        Canvas.SetTop(_fpsBadge, 12);
        _frameCount = 0;
        _fpsStopwatch.Restart();
    }

    private void UpdateAll()
    {
        UpdateLayoutAdorner();
        UpdateTreeHover();
        UpdateInspection();
        UpdateSelection();
        UpdateFocus();
    }

    private void SyncXamlRoot()
    {
        if (ReferenceEquals(_attachedXamlRoot, _root.XamlRoot))
        {
            return;
        }

        if (_attachedXamlRoot is not null)
        {
            _attachedXamlRoot.Changed -= OnXamlRootChanged;
        }

        _attachedXamlRoot = _root.XamlRoot;
        _popup.XamlRoot = _attachedXamlRoot;

        if (_attachedXamlRoot is not null)
        {
            _attachedXamlRoot.Changed += OnXamlRootChanged;
        }

        UpdateCanvasSize();
    }

    private void UpdateCanvasSize()
    {
        _canvas.Width = _root.XamlRoot?.Size.Width ?? _root.ActualWidth;
        _canvas.Height = _root.XamlRoot?.Size.Height ?? _root.ActualHeight;
    }

    private void UpdateSelection()
    {
        if (_selectedElement is FrameworkElement fe && TryGetBounds(fe, out var bounds))
        {
            ShowRectangle(_selectionRect, bounds);
            if (!ReferenceEquals(_selectedElement, _inspectionElement) &&
                !ReferenceEquals(_selectedElement, _treeHoverElement))
            {
                ShowBadge(_selectionBadge, _selectionText, bounds, BuildAnnotationText("Selected", fe, bounds), preferBelow: true);
            }
            else
            {
                _selectionBadge.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            _selectionRect.Visibility = Visibility.Collapsed;
            _selectionBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateTreeHover()
    {
        if (_treeHoverElement is not FrameworkElement fe || !TryGetBounds(fe, out var bounds))
        {
            _treeHoverRect.Visibility = Visibility.Collapsed;
            _treeHoverBadge.Visibility = Visibility.Collapsed;
            return;
        }

        if (ReferenceEquals(_treeHoverElement, _inspectionElement))
        {
            _treeHoverRect.Visibility = Visibility.Collapsed;
            _treeHoverBadge.Visibility = Visibility.Collapsed;
            return;
        }

        ShowRectangle(_treeHoverRect, Expand(bounds, 3));
        ShowBadge(_treeHoverBadge, _treeHoverText, bounds, BuildAnnotationText(_treeHoverLabel ?? "Hover", fe, bounds), preferBelow: true);
    }

    private void UpdateInspection()
    {
        if (_inspectionElement is not FrameworkElement fe || !TryGetBounds(fe, out var bounds))
        {
            _inspectionRect.Visibility = Visibility.Collapsed;
            _inspectionBadge.Visibility = Visibility.Collapsed;
            return;
        }

        ShowRectangle(_inspectionRect, Expand(bounds, 1));
        ShowBadge(_inspectionBadge, _inspectionText, bounds, BuildAnnotationText("Inspect", fe, bounds), preferBelow: false);
    }

    private void UpdateFocus()
    {
        if (!_showFocus || _focusedElement is not FrameworkElement fe || !TryGetBounds(fe, out var bounds))
        {
            _focusRect.Visibility = Visibility.Collapsed;
            _focusBadge.Visibility = Visibility.Collapsed;
            return;
        }

        ShowRectangle(_focusRect, Expand(bounds, 2));
        if (!ReferenceEquals(_focusedElement, _inspectionElement) &&
            !ReferenceEquals(_focusedElement, _treeHoverElement) &&
            !ReferenceEquals(_focusedElement, _selectedElement))
        {
            ShowBadge(_focusBadge, _focusText, bounds, BuildAnnotationText("Focus", fe, bounds), preferBelow: false);
        }
        else
        {
            _focusBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateLayoutAdorner()
    {
        var target = _treeHoverElement ?? _inspectionElement ?? _selectedElement;
        if (!_showMarginPadding || target is not FrameworkElement fe || !TryGetBounds(fe, out var bounds))
        {
            _layoutAdorner.Visibility = Visibility.Collapsed;
            return;
        }

        var margin = ClampThickness(fe.Margin);
        var padding = fe is Control control ? ClampThickness(control.Padding) : default;

        _layoutAdorner.Visibility = Visibility.Visible;
        _layoutAdorner.Width = bounds.Width;
        _layoutAdorner.Height = bounds.Height;
        Canvas.SetLeft(_layoutAdorner, bounds.X);
        Canvas.SetTop(_layoutAdorner, bounds.Y);

        _contentBorder.Margin = padding;
        _paddingBorder.BorderThickness = padding;
        _marginBorder.BorderThickness = margin;
        _marginBorder.Margin = new Thickness(-margin.Left, -margin.Top, -margin.Right, -margin.Bottom);
    }

    private void ShowBadge(Border badge, TextBlock text, Rect bounds, string value, bool preferBelow)
    {
        text.Text = value;
        badge.Visibility = Visibility.Visible;
        badge.MaxWidth = Math.Max(200, _canvas.Width - 16);

        var estimatedWidth = Math.Min(420, Math.Max(200, _canvas.Width - 16));
        var badgeX = Math.Max(8, Math.Min(bounds.X + 8, Math.Max(8, _canvas.Width - estimatedWidth)));
        var badgeY = preferBelow ? bounds.Y + bounds.Height + 8 : bounds.Y - 44;

        if (preferBelow)
        {
            if (badgeY > _canvas.Height - 56)
            {
                badgeY = Math.Max(8, bounds.Y - 44);
            }
        }
        else if (badgeY < 8)
        {
            badgeY = Math.Max(8, Math.Min(bounds.Y + bounds.Height + 8, _canvas.Height - 56));
        }

        Canvas.SetLeft(badge, badgeX);
        Canvas.SetTop(badge, badgeY);
    }

    private static string BuildAnnotationText(string title, FrameworkElement element, Rect bounds)
        => $"{title}: {InspectableNode.BuildSelector(element)}\n{FormatSize(bounds)}";

    private static string FormatSize(Rect bounds)
        => $"{Math.Round(bounds.Width)} x {Math.Round(bounds.Height)}";

    private static Thickness ClampThickness(Thickness thickness)
        => new(
            Math.Max(0, thickness.Left),
            Math.Max(0, thickness.Top),
            Math.Max(0, thickness.Right),
            Math.Max(0, thickness.Bottom));

    private static bool TryGetBounds(FrameworkElement element, out Rect bounds)
    {
        bounds = default;

        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            bounds = element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            return bounds.Width > 0 && bounds.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ShowRectangle(FrameworkElement rectangle, Rect rect)
    {
        rectangle.Visibility = Visibility.Visible;
        rectangle.Width = rect.Width;
        rectangle.Height = rect.Height;
        Canvas.SetLeft(rectangle, rect.X);
        Canvas.SetTop(rectangle, rect.Y);
    }

    private static Rect Expand(Rect rect, double amount)
        => new(rect.X - amount, rect.Y - amount, rect.Width + (amount * 2), rect.Height + (amount * 2));
}
