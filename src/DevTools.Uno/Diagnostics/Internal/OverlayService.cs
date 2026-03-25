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
    private readonly Rectangle _marginRect;
    private readonly Rectangle _paddingRect;
    private readonly Border _treeHoverBadge;
    private readonly TextBlock _treeHoverText;
    private readonly Border _inspectionBadge;
    private readonly TextBlock _inspectionText;
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
        _marginRect = CreateRectangle(new SolidColorBrush(Colors.DarkOrange), 1, [6, 2]);
        _paddingRect = CreateRectangle(new SolidColorBrush(Colors.Goldenrod), 1, [1, 2]);
        _treeHoverText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.Black),
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
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

        _canvas.Children.Add(_marginRect);
        _canvas.Children.Add(_paddingRect);
        _canvas.Children.Add(_selectionRect);
        _canvas.Children.Add(_treeHoverRect);
        _canvas.Children.Add(_inspectionRect);
        _canvas.Children.Add(_focusRect);
        _canvas.Children.Add(_treeHoverBadge);
        _canvas.Children.Add(_inspectionBadge);
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
        UpdateTreeHover();
        UpdateInspection();
        UpdateSelection();
        UpdateFocus();
        UpdateMarginPadding();
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
        }
        else
        {
            _selectionRect.Visibility = Visibility.Collapsed;
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

        ShowRectangle(_treeHoverRect, Expand(bounds, 3));
        _treeHoverText.Text = _treeHoverLabel ?? InspectableNode.BuildSelector(fe);
        _treeHoverBadge.Visibility = Visibility.Visible;

        var badgeX = Math.Max(8, Math.Min(bounds.X + 8, Math.Max(8, _canvas.Width - 320)));
        var badgeY = Math.Max(8, bounds.Y + bounds.Height + 8);
        if (badgeY > _canvas.Height - 40)
        {
            badgeY = Math.Max(8, bounds.Y - 32);
        }

        Canvas.SetLeft(_treeHoverBadge, badgeX);
        Canvas.SetTop(_treeHoverBadge, badgeY);
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
        _inspectionText.Text = InspectableNode.BuildSelector(fe);

        _inspectionBadge.Visibility = Visibility.Visible;
        var badgeX = Math.Max(8, Math.Min(bounds.X + 8, Math.Max(8, _canvas.Width - 240)));
        var badgeY = Math.Max(8, bounds.Y - 32);
        Canvas.SetLeft(_inspectionBadge, badgeX);
        Canvas.SetTop(_inspectionBadge, badgeY);
    }

    private void UpdateFocus()
    {
        if (!_showFocus || _focusedElement is not FrameworkElement fe || !TryGetBounds(fe, out var bounds))
        {
            _focusRect.Visibility = Visibility.Collapsed;
            return;
        }

        ShowRectangle(_focusRect, Expand(bounds, 2));
    }

    private void UpdateMarginPadding()
    {
        var target = _inspectionElement ?? _selectedElement;
        if (!_showMarginPadding || target is not FrameworkElement fe)
        {
            _marginRect.Visibility = Visibility.Collapsed;
            _paddingRect.Visibility = Visibility.Collapsed;
            return;
        }

        var slot = Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(fe);
        if (slot.Width > 0 && slot.Height > 0)
        {
            ShowRectangle(_marginRect, slot);
        }
        else
        {
            _marginRect.Visibility = Visibility.Collapsed;
        }

        if (fe is Control control && TryGetBounds(control, out var actual))
        {
            var padding = control.Padding;
            var inner = new Rect(
                actual.X + padding.Left,
                actual.Y + padding.Top,
                Math.Max(0, actual.Width - padding.Left - padding.Right),
                Math.Max(0, actual.Height - padding.Top - padding.Bottom));
            ShowRectangle(_paddingRect, inner);
        }
        else
        {
            _paddingRect.Visibility = Visibility.Collapsed;
        }
    }

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
