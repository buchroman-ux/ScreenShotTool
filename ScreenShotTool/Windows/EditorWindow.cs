using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScreenShotTool.Models;
using ScreenShotTool.Services;
using ModelThickness = ScreenShotTool.Models.StrokeThickness;

namespace ScreenShotTool.Windows;

public sealed class EditorWindow : Window
{
    private const double HandleSize = 11;
    private const double AxisLockRatio = 2.2;
    private const double ToolbarGroupMinHeight = 58;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 1.12;
    private readonly BitmapSource _screenshot;
    private readonly SaveService _saveService;
    private readonly ClipboardService _clipboardService;
    private readonly Action _openSettings;
    private readonly Action _openHelp;
    private readonly double _canvasWidth;
    private readonly double _canvasHeight;
    private readonly Grid _surface = new();
    private readonly Border _surfaceHost = new();
    private readonly Canvas _annotationCanvas = new();
    private ScrollViewer? _surfaceScrollViewer;
    private readonly List<AnnotationItem> _items = [];
    private readonly Stack<List<AnnotationItem>> _undo = new();
    private readonly Stack<List<AnnotationItem>> _redo = new();
    private readonly List<ToggleButton> _toolButtons = [];
    private readonly List<ToggleButton> _colorButtons = [];
    private readonly List<ToggleButton> _thicknessButtons = [];
    private readonly List<ToggleButton> _textSizeButtons = [];
    private readonly List<ToggleButton> _horizontalTextButtons = [];
    private readonly List<ToggleButton> _verticalTextButtons = [];
    private readonly List<ToolbarGroupInfo> _toolbarGroups = [];
    private readonly DispatcherTimer _caretTimer = new();
    private readonly Dictionary<TextMeasureKey, double> _textMeasureCache = [];

    private AnnotationTool _activeTool = AnnotationTool.Arrow;
    private AnnotationColor _activeColor = AnnotationColor.Red;
    private ModelThickness _activeThickness = ModelThickness.Medium;
    private TextSize _activeTextSize = TextSize.Medium;
    private TextHorizontalPlacement _activeHorizontalPlacement = TextHorizontalPlacement.Center;
    private TextVerticalPlacement _activeVerticalPlacement = TextVerticalPlacement.Middle;
    private Guid? _selectedId;
    private readonly HashSet<Guid> _selectedIds = [];
    private AnnotationItem? _draftItem;
    private List<AnnotationItem> _copiedItems = [];
    private bool _isMoving;
    private ResizeHandle? _activeHandle;
    private AnnotationItem? _resizeStartItem;
    private bool _isEditingText;
    private bool _isSelectingText;
    private bool _isSelectingArea;
    private int _textSelectionAnchor;
    private Point _dragStart;
    private Point _lastMousePosition;
    private Rect _selectionArea;
    private DateTime _lastEscapeAt = DateTime.MinValue;
    private bool _mouseInsideCanvas;
    private List<AnnotationItem> _moveSnapshot = [];
    private double _zoom = 1.0;
    private bool _showCaret = true;
    private bool _renderPending;
    private double _pixelsPerDip = 1;

    public EditorWindow(
        BitmapSource screenshot,
        SaveService saveService,
        ClipboardService clipboardService,
        Action openSettings,
        Action openHelp)
    {
        _screenshot = screenshot;
        _saveService = saveService;
        _clipboardService = clipboardService;
        _openSettings = openSettings;
        _openHelp = openHelp;
        (_canvasWidth, _canvasHeight) = CalculateCanvasSize(screenshot);

        Title = "ScreenShotTool - Editor";
        Width = Math.Min(SystemParameters.WorkArea.Width * 0.9, Math.Max(1040, _canvasWidth + 120));
        Height = Math.Min(SystemParameters.WorkArea.Height * 0.9, Math.Max(560, _canvasHeight + 130));
        MinWidth = Math.Min(980, SystemParameters.WorkArea.Width * 0.9);
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");

        Content = BuildContent();
        ConfigureShortcuts();
        _caretTimer.Interval = TimeSpan.FromMilliseconds(530);
        _caretTimer.Tick += (_, _) =>
        {
            if (TryGetSelectedTextTarget(out _))
            {
                _showCaret = !_showCaret;
                RequestRender();
            }
        };
        _caretTimer.Start();
        RenderAnnotations();
    }

    public string FinalizeImage()
    {
        var image = RenderFinalImage();
        var savedPath = _saveService.SavePng(image);
        _clipboardService.SetImage(image);
        Close();
        return savedPath;
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel();
        root.SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
            Content = BuildSurface()
        };
        _surfaceScrollViewer = scroll;
        scroll.Loaded += (_, _) => CenterCanvasViewportOnScreenshot();
        scroll.PreviewMouseWheel += OnSurfacePreviewMouseWheel;
        root.Children.Add(scroll);
        return root;
    }

    private UIElement BuildToolbar()
    {
        var toolbar = new Grid
        {
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Center
        };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        left.Children.Add(ToolGroup("tools", collapseOrder: 1, compactColumns: 3,
            ToolButton(AnnotationTool.Select, IconSelect(), "Select"),
            ToolButton(AnnotationTool.Arrow, IconArrow(), "Arrow"),
            ToolButton(AnnotationTool.Pencil, IconPencil(), "Pencil"),
            ToolButton(AnnotationTool.Rectangle, IconRectangle(), "Rectangle"),
            ToolButton(AnnotationTool.Ellipse, IconEllipse(), "Ellipse"),
            ToolButton(AnnotationTool.Text, IconText(), "Text")));

        left.Children.Add(ToolGroup("colors", collapseOrder: 2, compactColumns: 2,
            ColorButton(AnnotationColor.Red),
            ColorButton(AnnotationColor.Blue),
            ColorButton(AnnotationColor.Green),
            ColorButton(AnnotationColor.Yellow)));

        left.Children.Add(ToolGroup("thickness", collapseOrder: 3, compactColumns: 2,
            ThicknessButton(ModelThickness.Thin),
            ThicknessButton(ModelThickness.Medium),
            ThicknessButton(ModelThickness.Thick),
            ThicknessButton(ModelThickness.ExtraThick)));

        left.Children.Add(ToolGroup("text-size", collapseOrder: 4, compactColumns: 3,
            TextSizeButton(TextSize.Small),
            TextSizeButton(TextSize.Medium),
            TextSizeButton(TextSize.Large),
            TextSizeButton(TextSize.XLarge),
            TextSizeButton(TextSize.XXLarge),
            TextSizeButton(TextSize.Huge)));

        left.Children.Add(TextPositionGroups(collapseOrder: 5,
            HorizontalTextButton(TextHorizontalPlacement.Left),
            HorizontalTextButton(TextHorizontalPlacement.Center),
            HorizontalTextButton(TextHorizontalPlacement.Right),
            VerticalTextButton(TextVerticalPlacement.Top),
            VerticalTextButton(TextVerticalPlacement.Middle),
            VerticalTextButton(TextVerticalPlacement.Bottom)));

        left.Children.Add(ToolGroup("history-clipboard", collapseOrder: 0, compactColumns: 2,
            ActionIconButton(IconUndo(), "Undo", (_, _) => Undo()),
            ActionIconButton(IconRedo(), "Redo", (_, _) => Redo()),
            ActionIconButton(IconCopy(), "Copy", (_, _) => CopySelected()),
            ActionIconButton(IconPaste(), "Paste", (_, _) => PasteCopied())));
        Grid.SetColumn(left, 0);
        toolbar.Children.Add(left);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        right.Children.Add(IconButton(IconHelp(), "Help / Shortcuts", (_, _) => _openHelp()));
        right.Children.Add(IconButton(IconSettings(), "Settings", (_, _) => _openSettings()));
        Grid.SetColumn(right, 1);
        toolbar.Children.Add(right);
        toolbar.SizeChanged += (_, _) => UpdateToolbarGroupWidths(toolbar.ActualWidth - right.ActualWidth - 28);

        RefreshToolbarSelection();
        return toolbar;
    }

    private UIElement BuildSurface()
    {
        _surface.Width = _canvasWidth;
        _surface.Height = _canvasHeight;
        _surface.Margin = new Thickness(18);
        _surface.Background = Brushes.Transparent;

        var image = new Image
        {
            Source = _screenshot,
            Width = _screenshot.PixelWidth,
            Height = _screenshot.PixelHeight,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _surface.Children.Add(image);

        _annotationCanvas.Width = _canvasWidth;
        _annotationCanvas.Height = _canvasHeight;
        _annotationCanvas.Background = Brushes.Transparent;
        _annotationCanvas.MouseLeftButtonDown += OnCanvasMouseLeftButtonDown;
        _annotationCanvas.MouseMove += OnCanvasMouseMove;
        _annotationCanvas.MouseLeftButtonUp += OnCanvasMouseLeftButtonUp;
        _annotationCanvas.MouseEnter += (_, _) => _mouseInsideCanvas = true;
        _annotationCanvas.MouseLeave += (_, _) => _mouseInsideCanvas = false;
        _surface.Children.Add(_annotationCanvas);

        _surfaceHost.Child = _surface;
        _surfaceHost.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        _surfaceHost.HorizontalAlignment = HorizontalAlignment.Left;
        _surfaceHost.VerticalAlignment = VerticalAlignment.Top;
        return _surfaceHost;
    }

    private ToggleButton ToolButton(AnnotationTool tool, UIElement icon, string tooltip)
    {
        var button = ToggleIconButton(icon, tooltip);
        button.Tag = tool;
        button.Click += (_, _) =>
        {
            _activeTool = tool;
            if (tool != AnnotationTool.Select)
            {
                ResetDrawingDefaults();
                if (tool == AnnotationTool.Pencil)
                {
                    _activeThickness = ModelThickness.Thin;
                }
            }
            RefreshToolbarSelection();
        };
        _toolButtons.Add(button);
        return button;
    }

    private ToggleButton ColorButton(AnnotationColor color)
    {
        var swatch = new Rectangle
        {
            Width = 18,
            Height = 18,
            RadiusX = 2,
            RadiusY = 2,
            Fill = color.ToBrush(),
            Stroke = Brushes.White,
            StrokeThickness = 1
        };
        var button = ToggleIconButton(swatch, color.ToString());
        button.Tag = color;
        button.Click += (_, _) =>
        {
            if (TryGetSelectedTextTarget(out var textTarget) &&
                (_isEditingText || textTarget.TextSelectionLength > 0))
            {
                if (textTarget.TextSelectionLength > 0)
                {
                    PushUndo();
                    ApplyTextColor(textTarget, textTarget.TextSelectionStart, textTarget.TextSelectionLength, color);
                    ResetTextSelection(textTarget);
                    ShowCaretNow();
                    RenderAnnotations();
                }

                textTarget.TextColor = color;
                _activeColor = color;
                _selectedId = textTarget.Id;
                _selectedIds.Clear();
                _selectedIds.Add(textTarget.Id);
                _isEditingText = true;
                Focus();
            }
            else
            {
                _activeColor = color;
                ApplyColorToSelected(color);
            }
            RefreshToolbarSelection();
        };
        _colorButtons.Add(button);
        return button;
    }

    private ToggleButton ThicknessButton(ModelThickness thickness)
    {
        var button = ToggleIconButton(IconThickness(thickness), thickness.ToString());
        button.Tag = thickness;
        button.Click += (_, _) =>
        {
            _activeThickness = thickness;
            ApplyThicknessToSelected(thickness);
            RefreshToolbarSelection();
        };
        _thicknessButtons.Add(button);
        return button;
    }

    private ToggleButton TextSizeButton(TextSize size)
    {
        var button = ToggleIconButton(IconTextSize(size), $"Text {size}");
        button.Tag = size;
        button.Click += (_, _) =>
        {
            _activeTextSize = size;
            if (TryGetSelectedTextTarget(out var textTarget) &&
                (_isEditingText || textTarget.TextSelectionLength > 0))
            {
                if (textTarget.TextSelectionLength > 0)
                {
                    PushUndo();
                    ApplyTextSize(textTarget, textTarget.TextSelectionStart, textTarget.TextSelectionLength, size);
                    ResetTextSelection(textTarget);
                    FitTextItemToContent(textTarget);
                    ShowCaretNow();
                    RenderAnnotations();
                }

                _selectedId = textTarget.Id;
                _selectedIds.Clear();
                _selectedIds.Add(textTarget.Id);
                _isEditingText = true;
                Focus();
            }
            else
            {
                ApplyStyleToSelected(item =>
                {
                    PreserveExistingTextSize(item);
                    item.TextSize = size;
                });
            }
            RefreshToolbarSelection();
        };
        _textSizeButtons.Add(button);
        return button;
    }

    private ToggleButton HorizontalTextButton(TextHorizontalPlacement placement)
    {
        var button = ToggleIconButton(IconTextHorizontal(placement), $"Text {placement}");
        button.Tag = placement;
        button.Click += (_, _) =>
        {
            _activeHorizontalPlacement = placement;
            ApplyStyleToSelected(item => item.TextHorizontalPlacement = placement);
            RefreshToolbarSelection();
        };
        _horizontalTextButtons.Add(button);
        return button;
    }

    private ToggleButton VerticalTextButton(TextVerticalPlacement placement)
    {
        var button = ToggleIconButton(IconTextVertical(placement), $"Text {placement}");
        button.Tag = placement;
        button.Click += (_, _) =>
        {
            _activeVerticalPlacement = placement;
            ApplyStyleToSelected(item => item.TextVerticalPlacement = placement);
            RefreshToolbarSelection();
        };
        _verticalTextButtons.Add(button);
        return button;
    }

    private Border ToolGroup(string name, int collapseOrder, int compactColumns, params UIElement[] children)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Width = children.Length * 44
        };

        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        var border = new Border
        {
            Child = panel,
            Padding = new Thickness(4, 3, 0, 3),
            Margin = new Thickness(0, 0, 18, 12),
            CornerRadius = new CornerRadius(6),
            MinHeight = ToolbarGroupMinHeight,
            VerticalAlignment = VerticalAlignment.Top
        };
        border.SetResourceReference(BorderBrushProperty, "BorderBrush");
        border.SetResourceReference(BackgroundProperty, "PanelBackgroundBrush");
        border.BorderThickness = new Thickness(1);
        _toolbarGroups.Add(new ToolbarGroupInfo(name, collapseOrder, compactColumns, children.Length, panel));
        return border;
    }

    private Border TextPositionGroups(int collapseOrder, params UIElement[] children)
    {
        var horizontalPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Width = 3 * 44
        };
        var verticalPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Width = 3 * 44
        };

        for (var i = 0; i < children.Length; i++)
        {
            if (i < 3)
            {
                horizontalPanel.Children.Add(children[i]);
            }
            else
            {
                verticalPanel.Children.Add(children[i]);
            }
        }

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top
        };
        stack.Children.Add(GroupShell(horizontalPanel, new Thickness(0, 0, 18, 0)));
        stack.Children.Add(GroupShell(verticalPanel, new Thickness(0)));

        var border = new Border
        {
            Child = stack,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 18, 12),
            VerticalAlignment = VerticalAlignment.Top
        };

        _toolbarGroups.Add(new ToolbarGroupInfo(
            "text-position",
            collapseOrder,
            CompactColumns: 3,
            ChildCount: children.Length,
            Panel: null,
            FullWidthOverride: 282,
            CompactWidthOverride: 150,
            SetCompact: isCompact =>
            {
                if (isCompact)
                {
                    stack.Orientation = Orientation.Vertical;
                    if (stack.Children[0] is Border horizontalBorder)
                    {
                        horizontalBorder.Margin = new Thickness(0, 0, 0, 12);
                    }
                }
                else
                {
                    stack.Orientation = Orientation.Horizontal;
                    if (stack.Children[0] is Border horizontalBorder)
                    {
                        horizontalBorder.Margin = new Thickness(0, 0, 18, 0);
                    }
                }
            }));
        return border;
    }

    private static Border GroupShell(UIElement child, Thickness margin)
    {
        var border = new Border
        {
            Child = child,
            Padding = new Thickness(4, 3, 0, 3),
            Margin = margin,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            MinHeight = ToolbarGroupMinHeight,
            VerticalAlignment = VerticalAlignment.Top
        };
        border.SetResourceReference(BorderBrushProperty, "BorderBrush");
        border.SetResourceReference(BackgroundProperty, "PanelBackgroundBrush");
        return border;
    }

    private void UpdateToolbarGroupWidths(double availableWidth)
    {
        if (availableWidth <= 0 || _toolbarGroups.Count == 0)
        {
            return;
        }

        const double outerGroupMargin = 18;
        var totalFullWidth = _toolbarGroups.Sum(group => group.FullWidth + outerGroupMargin);
        foreach (var group in _toolbarGroups)
        {
            group.SetCompact?.Invoke(false);
            if (group.Panel is not null)
            {
                group.Panel.Width = group.FullWidth;
            }
        }

        foreach (var group in _toolbarGroups.OrderBy(group => group.CollapseOrder))
        {
            if (totalFullWidth <= availableWidth)
            {
                break;
            }

            if (group.CurrentWidth > group.CompactWidth)
            {
                totalFullWidth -= group.CurrentWidth - group.CompactWidth;
                group.SetCompact?.Invoke(true);
                if (group.Panel is not null)
                {
                    group.Panel.Width = group.CompactWidth;
                }
            }
        }
    }

    private static Border Spacer(double width) => new()
    {
        Width = width,
        Background = Brushes.Transparent
    };

    private static ToggleButton ToggleIconButton(UIElement icon, string tooltip) => new()
    {
        Content = icon,
        ToolTip = tooltip,
        Width = 38,
        Height = 34,
        Margin = new Thickness(0, 0, 6, 6),
        Padding = new Thickness(4),
        Cursor = Cursors.Hand,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Button IconButton(UIElement icon, string tooltip, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = icon,
            ToolTip = tooltip,
            Width = 42,
            Height = 38,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(4),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += click;
        return button;
    }

    private static Button ActionIconButton(UIElement icon, string tooltip, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = icon,
            ToolTip = tooltip,
            Width = 38,
            MinHeight = 32,
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = Cursors.Hand
        };
        button.Click += click;
        return button;
    }

    private void RefreshToolbarSelection()
    {
        foreach (var button in _toolButtons)
        {
            button.IsChecked = button.Tag is AnnotationTool tool && tool == _activeTool;
        }

        foreach (var button in _colorButtons)
        {
            button.IsChecked = button.Tag is AnnotationColor color && color == _activeColor;
        }

        foreach (var button in _thicknessButtons)
        {
            button.IsChecked = button.Tag is ModelThickness thickness && thickness == _activeThickness;
        }

        foreach (var button in _textSizeButtons)
        {
            button.IsChecked = button.Tag is TextSize size && size == _activeTextSize;
        }

        foreach (var button in _horizontalTextButtons)
        {
            button.IsChecked = button.Tag is TextHorizontalPlacement placement && placement == _activeHorizontalPlacement;
        }

        foreach (var button in _verticalTextButtons)
        {
            button.IsChecked = button.Tag is TextVerticalPlacement placement && placement == _activeVerticalPlacement;
        }
    }

    private static Canvas IconSelect()
    {
        var canvas = IconCanvas();
        var pointer = new Polygon
        {
            Points = [new Point(6, 3), new Point(6, 22), new Point(12, 17), new Point(16, 25), new Point(20, 23), new Point(16, 15), new Point(23, 15)]
        };
        ThemedFill(pointer);
        canvas.Children.Add(pointer);
        return canvas;
    }

    private static Canvas IconArrow()
    {
        var canvas = IconCanvas();
        var line = new Line
        {
            X1 = 5,
            Y1 = 5,
            X2 = 21,
            Y2 = 21,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ThemedStroke(line);
        canvas.Children.Add(line);
        var head = new Polygon
        {
            Points = [new Point(21, 21), new Point(12, 18), new Point(18, 12)]
        };
        ThemedFill(head);
        canvas.Children.Add(head);
        return canvas;
    }

    private static Canvas IconPencil()
    {
        var canvas = IconCanvas();

        var outline = new Path
        {
            Data = Geometry.Parse("M 4 22 L 5.7 16.1 L 17.4 4.4 C 18.3 3.5 19.7 3.5 20.6 4.4 L 21.6 5.4 C 22.5 6.3 22.5 7.7 21.6 8.6 L 9.9 20.3 Z"),
            Fill = Brushes.Transparent,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ThemedStroke(outline);
        canvas.Children.Add(outline);

        var eraserSeam = new Line
        {
            X1 = 16.2,
            Y1 = 5.6,
            X2 = 20.4,
            Y2 = 9.8,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ThemedStroke(eraserSeam);
        canvas.Children.Add(eraserSeam);

        var tipFold = new Line
        {
            X1 = 5.7,
            Y1 = 16.1,
            X2 = 9.9,
            Y2 = 20.3,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ThemedStroke(tipFold);
        canvas.Children.Add(tipFold);

        var lead = new Path
        {
            Data = Geometry.Parse("M 4 22 L 6.4 21.3"),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ThemedStroke(lead);
        canvas.Children.Add(lead);

        var drawMark = new Path
        {
            Data = Geometry.Parse("M 12 22 L 21 22"),
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ThemedStroke(drawMark);
        canvas.Children.Add(drawMark);
        return canvas;
    }

    private static Canvas IconRectangle()
    {
        var canvas = IconCanvas();
        var shape = new Rectangle
        {
            Width = 18,
            Height = 13,
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        ThemedStroke(shape);
        Canvas.SetLeft(shape, 4);
        Canvas.SetTop(shape, 7);
        canvas.Children.Add(shape);
        return canvas;
    }

    private static Canvas IconEllipse()
    {
        var canvas = IconCanvas();
        var shape = new Ellipse
        {
            Width = 18,
            Height = 14,
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        ThemedStroke(shape);
        Canvas.SetLeft(shape, 4);
        Canvas.SetTop(shape, 6);
        canvas.Children.Add(shape);
        return canvas;
    }

    private static TextBlock IconText()
    {
        var text = new TextBlock
        {
            Text = "T",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return text;
    }

    private static Canvas IconThickness(ModelThickness thickness)
    {
        var canvas = IconCanvas();
        var line = new Line
        {
            X1 = 4,
            Y1 = 13,
            X2 = 22,
            Y2 = 13,
            StrokeThickness = thickness.ToPixels(),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ThemedStroke(line);
        canvas.Children.Add(line);
        return canvas;
    }

    private static TextBlock IconTextSize(TextSize size)
    {
        var text = new TextBlock
        {
            Text = "A",
            FontSize = size switch
            {
                TextSize.Small => 13,
                TextSize.Large => 19,
                TextSize.XLarge => 21,
                TextSize.XXLarge => 23,
                TextSize.Huge => 25,
                _ => 17
            },
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return text;
    }

    private static Canvas IconTextHorizontal(TextHorizontalPlacement placement)
    {
        var canvas = IconCanvas();
        var widths = new[] { 16d, 11d, 15d };
        for (var i = 0; i < widths.Length; i++)
        {
            var width = widths[i];
            var x = placement switch
            {
                TextHorizontalPlacement.Left => 5,
                TextHorizontalPlacement.Right => 21 - width,
                _ => 13 - width / 2
            };
            AddIconLine(canvas, x, 7 + i * 5, width, horizontal: true);
        }
        return canvas;
    }

    private static Canvas IconTextVertical(TextVerticalPlacement placement)
    {
        var canvas = IconCanvas();
        var startY = placement switch
        {
            TextVerticalPlacement.Top => 5,
            TextVerticalPlacement.Bottom => 15,
            _ => 10
        };
        AddIconLine(canvas, 6, startY, 14, horizontal: true);
        AddIconLine(canvas, 8, startY + 5, 10, horizontal: true);
        var box = new Rectangle
        {
            Width = 20,
            Height = 20,
            StrokeThickness = 1,
            StrokeDashArray = [2, 2],
            Fill = Brushes.Transparent,
            Opacity = 0.55
        };
        ThemedStroke(box);
        Canvas.SetLeft(box, 3);
        Canvas.SetTop(box, 3);
        canvas.Children.Insert(0, box);
        return canvas;
    }

    private static void AddIconLine(Canvas canvas, double x, double y, double length, bool horizontal)
    {
        var line = new Line
        {
            X1 = x,
            Y1 = y,
            X2 = horizontal ? x + length : x,
            Y2 = horizontal ? y : y + length,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ThemedStroke(line);
        canvas.Children.Add(line);
    }

    private static Canvas IconUndo()
    {
        var canvas = IconCanvas();
        var path = new Path
        {
            Data = Geometry.Parse("M 8 8 L 4 13 L 8 18 M 5 13 H 15 C 20 13 22 17 20 21"),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        ThemedStroke(path);
        canvas.Children.Add(path);
        return canvas;
    }

    private static Canvas IconRedo()
    {
        var canvas = IconCanvas();
        var path = new Path
        {
            Data = Geometry.Parse("M 18 8 L 22 13 L 18 18 M 21 13 H 11 C 6 13 4 17 6 21"),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        ThemedStroke(path);
        canvas.Children.Add(path);
        return canvas;
    }

    private static Canvas IconCopy()
    {
        var canvas = IconCanvas();
        AddPaper(canvas, 6, 8);
        AddPaper(canvas, 10, 4);
        return canvas;
    }

    private static Canvas IconPaste()
    {
        var canvas = IconCanvas();
        var board = new Rectangle
        {
            Width = 16,
            Height = 18,
            RadiusX = 2,
            RadiusY = 2,
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        ThemedStroke(board);
        canvas.Children.Add(board);
        Canvas.SetLeft(board, 5);
        Canvas.SetTop(board, 6);
        var clip = new Rectangle
        {
            Width = 9,
            Height = 4,
            RadiusX = 1.5,
            RadiusY = 1.5,
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        ThemedStroke(clip);
        canvas.Children.Add(clip);
        Canvas.SetLeft(clip, 8.5);
        Canvas.SetTop(clip, 3);
        var pageLine = new Line { X1 = 9, X2 = 17, Y1 = 13, Y2 = 13, StrokeThickness = 1.5 };
        ThemedStroke(pageLine);
        canvas.Children.Add(pageLine);
        pageLine = new Line { X1 = 9, X2 = 16, Y1 = 17, Y2 = 17, StrokeThickness = 1.5 };
        ThemedStroke(pageLine);
        canvas.Children.Add(pageLine);
        return canvas;
    }

    private static void AddPaper(Canvas canvas, double left, double top)
    {
        var paper = new Rectangle
        {
            Width = 12,
            Height = 15,
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        ThemedStroke(paper);
        Canvas.SetLeft(paper, left);
        Canvas.SetTop(paper, top);
        canvas.Children.Add(paper);
    }

    private static Canvas IconHelp()
    {
        var canvas = IconCanvas();
        var ring = new Ellipse
        {
            Width = 22,
            Height = 22,
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        ThemedStroke(ring);
        Canvas.SetLeft(ring, 2);
        Canvas.SetTop(ring, 2);
        canvas.Children.Add(ring);

        var question = new TextBlock
        {
            Text = "?",
            Width = 22,
            Height = 22,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            LineHeight = 20,
            VerticalAlignment = VerticalAlignment.Center
        };
        question.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        Canvas.SetLeft(question, 2);
        Canvas.SetTop(question, 2);
        canvas.Children.Add(question);
        return canvas;
    }

    private static Canvas IconSettings()
    {
        var canvas = IconCanvas();
        AddIconLine(canvas, 4, 7, 18, horizontal: true);
        AddIconLine(canvas, 4, 13, 18, horizontal: true);
        AddIconLine(canvas, 4, 19, 18, horizontal: true);
        AddSliderKnob(canvas, 9, 7);
        AddSliderKnob(canvas, 17, 13);
        AddSliderKnob(canvas, 12, 19);
        return canvas;
    }

    private static void AddSliderKnob(Canvas canvas, double centerX, double centerY)
    {
        var knob = new Ellipse
        {
            Width = 6,
            Height = 6,
            StrokeThickness = 1.6
        };
        knob.SetResourceReference(Shape.FillProperty, "ButtonBackgroundBrush");
        ThemedStroke(knob);
        Canvas.SetLeft(knob, centerX - 3);
        Canvas.SetTop(knob, centerY - 3);
        canvas.Children.Add(knob);
    }

    private static Canvas IconCanvas() => new()
    {
        Width = 26,
        Height = 26,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static void ThemedStroke(Shape shape) =>
        shape.SetResourceReference(Shape.StrokeProperty, "TextBrush");

    private static void ThemedFill(Shape shape) =>
        shape.SetResourceReference(Shape.FillProperty, "TextBrush");

    private void ConfigureShortcuts()
    {
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                HandleEscape();
                e.Handled = true;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Z)
            {
                Undo();
                e.Handled = true;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Y)
            {
                Redo();
                e.Handled = true;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
            {
                CopySelected();
                e.Handled = true;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
            {
                PasteCopied();
                e.Handled = true;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.A && TryGetSelectedTextTarget(out var selectAllTarget))
            {
                selectAllTarget.TextSelectionStart = 0;
                selectAllTarget.TextSelectionLength = selectAllTarget.Text.Length;
                _isEditingText = true;
                _showCaret = false;
                RequestRender();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && _selectedIds.Count > 0)
            {
                PushUndo();
                _items.RemoveAll(item => _selectedIds.Contains(item.Id));
                ClearSelection();
                RenderAnnotations();
                e.Handled = true;
            }
            else if (e.Key == Key.Back && TryGetSelectedTextTarget(out var backspaceTarget) && backspaceTarget.Text.Length > 0)
            {
                PushUndo();
                DeleteBackspace(backspaceTarget);
                FitTextItemToContent(backspaceTarget);
                _isEditingText = true;
                ShowCaretNow();
                RenderAnnotations();
                e.Handled = true;
            }
            else if ((e.Key == Key.Enter || e.Key == Key.Return) && TryGetSelectedTextTarget(out var enterTarget))
            {
                PushUndo();
                ReplaceSelectedText(enterTarget, Environment.NewLine, _activeColor, _activeTextSize);
                FitTextItemToContent(enterTarget);
                _isEditingText = true;
                ShowCaretNow();
                RenderAnnotations();
                e.Handled = true;
            }
        };
        TextInput += OnTextInput;
    }

    private void HandleEscape()
    {
        var now = DateTime.UtcNow;
        var isDoubleEscape = now - _lastEscapeAt <= TimeSpan.FromMilliseconds(650);
        _lastEscapeAt = now;

        if (!isDoubleEscape)
        {
            _activeTool = AnnotationTool.Select;
            ClearSelection();
            _draftItem = null;
            _isMoving = false;
            _isEditingText = false;
            _isSelectingText = false;
            _isSelectingArea = false;
            _activeHandle = null;
            _resizeStartItem = null;
            if (_annotationCanvas.IsMouseCaptured)
            {
                _annotationCanvas.ReleaseMouseCapture();
            }
            RefreshToolbarSelection();
            RenderAnnotations();
            return;
        }

        var result = MessageBox.Show(
            this,
            "Close the screenshot editor? Unsaved annotation changes will be lost.",
            "Close editor",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.OK);

        if (result == MessageBoxResult.OK)
        {
            Close();
        }
        else
        {
            _lastEscapeAt = DateTime.MinValue;
        }
    }

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        _lastMousePosition = e.GetPosition(_annotationCanvas);
        _dragStart = _lastMousePosition;

        if (FindHandleFromSource(e.OriginalSource) is { } handle)
        {
            SelectOnly(handle.ItemId);
            var selected = _items.FirstOrDefault(item => item.Id == handle.ItemId);
            if (selected is null)
            {
                return;
            }

            PushUndo();
            _activeHandle = handle.Handle;
            _resizeStartItem = selected.Clone();
            _annotationCanvas.CaptureMouse();
            RenderAnnotations();
            return;
        }

        var hit = FindTextAnnotationAtPoint(_lastMousePosition) ?? FindAnnotationFromSource(e.OriginalSource);
        if (_activeTool == AnnotationTool.Select)
        {
            if (hit is not null)
            {
                if (!_selectedIds.Contains(hit.Id))
                {
                    SelectOnly(hit.Id);
                }
                else
                {
                    _selectedId = hit.Id;
                }

                if (_selectedIds.Count == 1 && TryPointToTextIndex(hit, _lastMousePosition, out var index))
                {
                    _isSelectingText = true;
                    _isEditingText = true;
                    _textSelectionAnchor = index;
                    hit.TextSelectionStart = index;
                    hit.TextSelectionLength = 0;
                    ShowCaretNow();
                    _annotationCanvas.CaptureMouse();
                    RenderAnnotations();
                    return;
                }

                PushUndo();
                _isMoving = true;
                _moveSnapshot = CloneItems(_items);
                _annotationCanvas.CaptureMouse();
            }
            else
            {
                ClearSelection();
                _isSelectingArea = true;
                _selectionArea = new Rect(_dragStart, _dragStart);
                _annotationCanvas.CaptureMouse();
            }
            RenderAnnotations();
            return;
        }

        PushUndo();
        _draftItem = new AnnotationItem
        {
            Kind = ToolToKind(_activeTool),
            Color = SelectedColor(),
            Thickness = SelectedThickness(),
            TextSize = _activeTextSize,
            TextHorizontalPlacement = _activeHorizontalPlacement,
            TextVerticalPlacement = _activeVerticalPlacement,
            Start = _dragStart,
            End = _dragStart
        };
        if (_draftItem.Kind == AnnotationKind.Pencil)
        {
            _draftItem.Points.Add(_dragStart);
        }
        _items.Add(_draftItem);
        SelectOnly(_draftItem.Id);
        _annotationCanvas.CaptureMouse();
        RenderAnnotations();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        _lastMousePosition = e.GetPosition(_annotationCanvas);

        if (_draftItem is not null)
        {
            var point = ClampToCanvas(_lastMousePosition);
            _draftItem.End = point;
            if (_draftItem.Kind == AnnotationKind.Pencil &&
                (_draftItem.Points.Count == 0 || (point - _draftItem.Points[^1]).Length >= 2))
            {
                _draftItem.Points.Add(point);
            }
            RequestRender();
            return;
        }

        if (_activeHandle is not null && _resizeStartItem is not null && _selectedId is not null)
        {
            var selected = _items.FirstOrDefault(item => item.Id == _selectedId.Value);
            if (selected is not null)
            {
                ApplyResize(selected, _resizeStartItem, _activeHandle.Value, _lastMousePosition);

                RequestRender();
            }
            return;
        }

        if (_isSelectingText && _selectedId is not null)
        {
            var selected = _items.FirstOrDefault(item => item.Id == _selectedId.Value);
            if (selected is not null && TryPointToTextIndex(selected, _lastMousePosition, out var index))
            {
                selected.TextSelectionStart = Math.Min(_textSelectionAnchor, index);
                selected.TextSelectionLength = Math.Abs(index - _textSelectionAnchor);
                if (selected.TextSelectionLength > 0)
                {
                    _isEditingText = true;
                }
                RequestRender();
            }
            return;
        }

        if (_isSelectingArea)
        {
            _selectionArea = new Rect(_dragStart, _lastMousePosition);
            RequestRender();
            return;
        }

        if (_isMoving && _selectedIds.Count > 0)
        {
            var delta = _lastMousePosition - _dragStart;
            _items.Clear();
            foreach (var item in _moveSnapshot)
            {
                var clone = item.Clone();
                if (_selectedIds.Contains(clone.Id))
                {
                    clone.Offset(delta);
                }
                _items.Add(clone);
            }
            RequestRender();
        }
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draftItem is not null)
        {
            var point = ClampToCanvas(e.GetPosition(_annotationCanvas));
            _draftItem.End = point;
            if (_draftItem.Kind == AnnotationKind.Pencil &&
                (_draftItem.Points.Count == 0 || _draftItem.Points[^1] != point))
            {
                _draftItem.Points.Add(point);
            }

            AnnotationItem? completedItem = _draftItem;
            if (_draftItem.Kind == AnnotationKind.Pencil
                ? _draftItem.Points.Count < 2
                : _draftItem.Bounds.Width < 4 || _draftItem.Bounds.Height < 4)
            {
                _items.Remove(_draftItem);
                completedItem = null;
            }

            _draftItem = null;
            _annotationCanvas.ReleaseMouseCapture();
            if (completedItem is not null)
            {
                if (completedItem.Kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Text)
                {
                    completedItem.TextColor = _activeColor;
                }

                SelectOnly(completedItem.Id);
                _activeTool = completedItem.Kind == AnnotationKind.Arrow
                    ? AnnotationTool.Rectangle
                    : completedItem.Kind == AnnotationKind.Pencil
                        ? AnnotationTool.Pencil
                    : AnnotationTool.Select;
                RefreshToolbarSelection();
            }
            RenderAnnotations();
        }

        if (_isMoving)
        {
            _isMoving = false;
            _annotationCanvas.ReleaseMouseCapture();
        }

        if (_activeHandle is not null)
        {
            _activeHandle = null;
            _resizeStartItem = null;
            _annotationCanvas.ReleaseMouseCapture();
        }

        if (_isSelectingText)
        {
            if (_selectedId is not null &&
                _items.FirstOrDefault(item => item.Id == _selectedId.Value) is { TextSelectionLength: 0 })
            {
                _isEditingText = true;
                ShowCaretNow();
            }
            _isSelectingText = false;
            _annotationCanvas.ReleaseMouseCapture();
        }

        if (_isSelectingArea)
        {
            CompleteAreaSelection();
            _isSelectingArea = false;
            _annotationCanvas.ReleaseMouseCapture();
            RenderAnnotations();
        }
    }

    private void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control ||
            string.IsNullOrEmpty(e.Text) ||
            !TryGetSelectedTextTarget(out var item))
        {
            return;
        }

        PushUndo();
        ReplaceSelectedText(item, e.Text, _activeColor, _activeTextSize);
        FitTextItemToContent(item);
        _isEditingText = true;
        ShowCaretNow();
        RenderAnnotations();
        e.Handled = true;
    }

    private void RequestRender()
    {
        if (_renderPending)
        {
            return;
        }

        _renderPending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _renderPending = false;
            RenderAnnotations();
        }, DispatcherPriority.Render);
    }

    private void ShowCaretNow()
    {
        _showCaret = true;
        _caretTimer.Stop();
        _caretTimer.Start();
    }

    private void RenderAnnotations()
    {
        _renderPending = false;
        _textMeasureCache.Clear();
        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        _annotationCanvas.Children.Clear();

        foreach (var item in _items)
        {
            AddAnnotationVisual(item);
            if (_selectedIds.Contains(item.Id))
            {
                AddSelectionVisual(item.Bounds, item.Kind != AnnotationKind.Pencil);
            }
        }

        if (_isSelectingArea)
        {
            AddAreaSelectionVisual(_selectionArea);
        }
    }

    private void AddAnnotationVisual(AnnotationItem item)
    {
        switch (item.Kind)
        {
            case AnnotationKind.Arrow:
                AddArrow(item);
                break;
            case AnnotationKind.Pencil:
                AddPencil(item);
                break;
            case AnnotationKind.Rectangle:
                AddRectangle(item);
                AddShapeText(item);
                break;
            case AnnotationKind.Ellipse:
                AddEllipse(item);
                AddShapeText(item);
                break;
            case AnnotationKind.Text:
                AddTextVisual(item);
                break;
        }
    }

    private void AddArrow(AnnotationItem item)
    {
        var brush = item.Color.ToBrush();
        var thickness = item.Thickness.ToPixels();
        var vector = item.End - item.Start;
        if (vector.Length < 0.1)
        {
            return;
        }

        vector.Normalize();
        var headSize = Math.Max(14, thickness * 4.2);
        var lineEnd = item.End - vector * (headSize * 0.42);
        var line = new Line
        {
            X1 = item.Start.X,
            Y1 = item.Start.Y,
            X2 = lineEnd.X,
            Y2 = lineEnd.Y,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Flat,
            Tag = item.Id
        };
        _annotationCanvas.Children.Add(line);

        var head = BuildArrowHead(item.Start, item.End, headSize, brush);
        head.Tag = item.Id;
        _annotationCanvas.Children.Add(head);
    }

    private void AddPencil(AnnotationItem item)
    {
        var points = item.Points.Count > 0
            ? item.Points
            : new List<Point> { item.Start, item.End };
        if (points.Count < 2)
        {
            return;
        }

        var pointCollection = new PointCollection();
        foreach (var point in points)
        {
            pointCollection.Add(point);
        }

        var stroke = new Polyline
        {
            Points = pointCollection,
            Stroke = item.Color.ToBrush(),
            StrokeThickness = item.Thickness.ToPixels(),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Tag = item.Id
        };
        _annotationCanvas.Children.Add(stroke);
    }

    private static Polygon BuildArrowHead(Point start, Point end, double size, Brush brush)
    {
        var direction = start - end;
        if (direction.Length < 0.1)
        {
            direction = new Vector(-1, 0);
        }
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        return new Polygon
        {
            Fill = brush,
            Points =
            [
                end,
                end + direction * size + normal * (size * 0.45),
                end + direction * size - normal * (size * 0.45)
            ]
        };
    }

    private void AddRectangle(AnnotationItem item)
    {
        var rect = item.Bounds;
        var shape = new Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Stroke = item.Color.ToBrush(),
            StrokeThickness = item.Thickness.ToPixels(),
            Fill = Brushes.Transparent,
            Tag = item.Id
        };
        Canvas.SetLeft(shape, rect.Left);
        Canvas.SetTop(shape, rect.Top);
        _annotationCanvas.Children.Add(shape);
    }

    private void AddEllipse(AnnotationItem item)
    {
        var rect = item.Bounds;
        var shape = new Ellipse
        {
            Width = rect.Width,
            Height = rect.Height,
            Stroke = item.Color.ToBrush(),
            StrokeThickness = item.Thickness.ToPixels(),
            Fill = Brushes.Transparent,
            Tag = item.Id
        };
        Canvas.SetLeft(shape, rect.Left);
        Canvas.SetTop(shape, rect.Top);
        _annotationCanvas.Children.Add(shape);
    }

    private void AddShapeText(AnnotationItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Text) && _selectedId != item.Id)
        {
            return;
        }

        if (!TryGetTextContentRect(item, out var contentRect))
        {
            return;
        }
        AddTextContent(item, contentRect);
    }

    private void AddTextVisual(AnnotationItem item)
    {
        var rect = item.Bounds;
        var outline = new Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Stroke = item.Color.ToBrush(),
            StrokeThickness = item.Thickness.ToPixels(),
            StrokeDashArray = [5, 4],
            Fill = Brushes.Transparent,
            Tag = item.Id
        };
        Canvas.SetLeft(outline, rect.Left);
        Canvas.SetTop(outline, rect.Top);
        _annotationCanvas.Children.Add(outline);

        var padding = TextPadding(item);
        var contentRect = new Rect(
            rect.Left + padding,
            rect.Top + padding,
            Math.Max(1, rect.Width - padding * 2),
            Math.Max(1, rect.Height - padding * 2));
        AddTextContent(item, contentRect);
    }

    private void AddTextContent(AnnotationItem item, Rect contentRect)
    {
        var lines = BuildTextLayout(item, contentRect);
        var layer = new Canvas
        {
            Width = contentRect.Width,
            Height = contentRect.Height,
            Clip = new RectangleGeometry(new Rect(0, 0, contentRect.Width, contentRect.Height)),
            Tag = item.Id
        };
        Canvas.SetLeft(layer, contentRect.Left);
        Canvas.SetTop(layer, contentRect.Top);

        AddTextSelectionHighlight(layer, item, contentRect, lines);

        foreach (var line in lines)
        {
            if (line.Top >= contentRect.Bottom || line.Top + line.Height <= contentRect.Top)
            {
                continue;
            }

            var text = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap,
                LineHeight = line.Height,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                ClipToBounds = true,
                Width = Math.Max(1, contentRect.Right - line.Left),
                Height = line.Height,
                Tag = item.Id
            };
            AddStyledRuns(text, item, line.Start, line.Length);
            Canvas.SetLeft(text, line.Left - contentRect.Left);
            Canvas.SetTop(text, line.Top - contentRect.Top);
            layer.Children.Add(text);
        }

        AddCaretIfNeeded(layer, item, contentRect, lines);
        _annotationCanvas.Children.Add(layer);
    }

    private static void AddStyledRuns(TextBlock textBlock, AnnotationItem item, int rangeStart, int rangeLength)
    {
        textBlock.Inlines.Clear();
        if (string.IsNullOrEmpty(item.Text) || rangeLength <= 0)
        {
            return;
        }

        var rangeEnd = Math.Clamp(rangeStart + rangeLength, 0, item.Text.Length);
        rangeStart = Math.Clamp(rangeStart, 0, item.Text.Length);
        if (rangeEnd <= rangeStart)
        {
            return;
        }

        var boundaries = new SortedSet<int> { rangeStart, rangeEnd };
        foreach (var span in NormalizeTextSpans(item))
        {
            var spanStart = Math.Clamp(span.Start, rangeStart, rangeEnd);
            var spanEnd = Math.Clamp(span.Start + span.Length, rangeStart, rangeEnd);
            boundaries.Add(spanStart);
            boundaries.Add(spanEnd);
        }

        foreach (var span in NormalizeTextSizeSpans(item))
        {
            var spanStart = Math.Clamp(span.Start, rangeStart, rangeEnd);
            var spanEnd = Math.Clamp(span.Start + span.Length, rangeStart, rangeEnd);
            boundaries.Add(spanStart);
            boundaries.Add(spanEnd);
        }

        var points = boundaries.ToList();
        for (var i = 0; i < points.Count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            if (end <= start)
            {
                continue;
            }

            textBlock.Inlines.Add(new Run(item.Text[start..end])
            {
                Foreground = TextColorAt(item, start).ToBrush(),
                FontSize = TextSizeAt(item, start).ToPixels()
            });
        }
    }

    private static AnnotationColor TextColorAt(AnnotationItem item, int index)
    {
        foreach (var span in NormalizeTextSpans(item))
        {
            if (index >= span.Start && index < span.Start + span.Length)
            {
                return span.Color;
            }
        }

        return item.TextColor;
    }

    private static TextSize TextSizeAt(AnnotationItem item, int index)
    {
        foreach (var span in NormalizeTextSizeSpans(item))
        {
            if (index >= span.Start && index < span.Start + span.Length)
            {
                return span.Size;
            }
        }

        return item.TextSize;
    }

    private void AddSelectionVisual(Rect rect, bool showResizeHandles)
    {
        var selection = new Rectangle
        {
            Width = rect.Width + 8,
            Height = rect.Height + 8,
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 1,
            StrokeDashArray = [4, 3],
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(selection, rect.Left - 4);
        Canvas.SetTop(selection, rect.Top - 4);
        _annotationCanvas.Children.Add(selection);

        if (showResizeHandles)
        {
            AddResizeHandle(rect.TopLeft, ResizeHandle.TopLeft);
            AddResizeHandle(rect.TopRight, ResizeHandle.TopRight);
            AddResizeHandle(rect.BottomRight, ResizeHandle.BottomRight);
            AddResizeHandle(rect.BottomLeft, ResizeHandle.BottomLeft);
        }
    }

    private void AddAreaSelectionVisual(Rect rect)
    {
        var normalized = NormalizeRect(rect);
        var selection = new Rectangle
        {
            Width = normalized.Width,
            Height = normalized.Height,
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 1,
            StrokeDashArray = [4, 3],
            Fill = Brushes.DeepSkyBlue,
            Opacity = 0.18,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(selection, normalized.Left);
        Canvas.SetTop(selection, normalized.Top);
        _annotationCanvas.Children.Add(selection);
    }

    private AnnotationItem? FindAnnotationFromSource(object source)
    {
        var element = source as FrameworkElement;
        while (element is not null)
        {
            if (element.Tag is Guid id)
            {
                return _items.LastOrDefault(item => item.Id == id);
            }
            element = VisualTreeHelper.GetParent(element) as FrameworkElement;
        }
        return null;
    }

    private AnnotationItem? FindTextAnnotationAtPoint(Point point)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (item.Kind is not (AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Text))
            {
                continue;
            }

            if ((TryGetTextContentRect(item, out var textRect) && textRect.Contains(point)) ||
                item.Bounds.Contains(point))
            {
                return item;
            }
        }

        return null;
    }

    private HandleTag? FindHandleFromSource(object source)
    {
        var element = source as FrameworkElement;
        while (element is not null)
        {
            if (element.Tag is HandleTag handle)
            {
                return handle;
            }

            element = VisualTreeHelper.GetParent(element) as FrameworkElement;
        }

        return null;
    }

    private void AddResizeHandle(Point point, ResizeHandle handle)
    {
        if (_selectedId is null)
        {
            return;
        }

        var dot = new Ellipse
        {
            Width = HandleSize,
            Height = HandleSize,
            Fill = Brushes.White,
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 2,
            Tag = new HandleTag(_selectedId.Value, handle),
            Cursor = Cursors.SizeAll
        };
        Canvas.SetLeft(dot, point.X - HandleSize / 2);
        Canvas.SetTop(dot, point.Y - HandleSize / 2);
        _annotationCanvas.Children.Add(dot);
    }

    private void ApplyResize(AnnotationItem selected, AnnotationItem startItem, ResizeHandle handle, Point current)
    {
        var rect = startItem.Bounds;
        var delta = current - _dragStart;
        var lockHorizontal = Math.Abs(delta.X) > Math.Abs(delta.Y) * AxisLockRatio;
        var lockVertical = Math.Abs(delta.Y) > Math.Abs(delta.X) * AxisLockRatio;

        if (lockHorizontal)
        {
            delta.Y = 0;
        }
        else if (lockVertical)
        {
            delta.X = 0;
        }

        var left = rect.Left;
        var right = rect.Right;
        var top = rect.Top;
        var bottom = rect.Bottom;

        if (handle is ResizeHandle.TopLeft or ResizeHandle.BottomLeft)
        {
            left += delta.X;
        }
        else
        {
            right += delta.X;
        }

        if (handle is ResizeHandle.TopLeft or ResizeHandle.TopRight)
        {
            top += delta.Y;
        }
        else
        {
            bottom += delta.Y;
        }

        if (right - left < 12)
        {
            if (handle is ResizeHandle.TopLeft or ResizeHandle.BottomLeft)
            {
                left = right - 12;
            }
            else
            {
                right = left + 12;
            }
        }

        if (bottom - top < 12)
        {
            if (handle is ResizeHandle.TopLeft or ResizeHandle.TopRight)
            {
                top = bottom - 12;
            }
            else
            {
                bottom = top + 12;
            }
        }

        SetBounds(selected, new Rect(new Point(left, top), new Point(right, bottom)));
    }

    private static void SetBounds(AnnotationItem item, Rect rect)
    {
        item.Start = rect.TopLeft;
        item.End = rect.BottomRight;
    }

    private static double TextPadding(AnnotationItem item) => Math.Max(8, item.Thickness.ToPixels() + 7);

    private void FitTextItemToContent(AnnotationItem item)
    {
        if (item.Kind != AnnotationKind.Text ||
            !TryGetTextContentRect(item, out var contentRect))
        {
            return;
        }

        var lines = BuildTextLayout(item, contentRect);
        var contentHeight = Math.Max(1, lines.Sum(line => line.Height));
        var requiredHeight = contentHeight + TextPadding(item) * 2;
        var rect = item.Bounds;
        if (requiredHeight <= rect.Height + 0.5)
        {
            return;
        }

        var maxHeight = Math.Max(rect.Height, _canvasHeight - rect.Top);
        var nextHeight = Math.Min(requiredHeight, maxHeight);
        SetBounds(item, new Rect(rect.Left, rect.Top, rect.Width, nextHeight));
    }

    private List<TextVisualLine> BuildTextLayout(AnnotationItem item, Rect contentRect)
    {
        var rawLines = BuildRawTextLines(item.Text);
        var visualLines = new List<TextVisualLine>();
        foreach (var rawLine in rawLines)
        {
            AddWrappedTextLines(item, contentRect, rawLine.Start, rawLine.Length, visualLines);
        }

        if (visualLines.Count == 0)
        {
            visualLines.Add(CreateVisualLine(item, contentRect, 0, 0));
        }

        var totalHeight = visualLines.Sum(line => line.Height);
        var top = item.TextVerticalPlacement switch
        {
            TextVerticalPlacement.Bottom when totalHeight <= contentRect.Height => contentRect.Bottom - totalHeight,
            TextVerticalPlacement.Middle when totalHeight <= contentRect.Height => contentRect.Top + (contentRect.Height - totalHeight) / 2,
            _ => contentRect.Top
        };

        for (var i = 0; i < visualLines.Count; i++)
        {
            var line = visualLines[i];
            visualLines[i] = line with { Top = top };
            top += line.Height;
        }

        return visualLines;
    }

    private static List<RawTextLine> BuildRawTextLines(string text)
    {
        var lines = new List<RawTextLine>();
        if (string.IsNullOrEmpty(text))
        {
            lines.Add(new RawTextLine(0, 0));
            return lines;
        }

        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\r' && text[i] != '\n')
            {
                continue;
            }

            lines.Add(new RawTextLine(start, i - start));
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            start = i + 1;
        }

        lines.Add(new RawTextLine(start, text.Length - start));
        return lines;
    }

    private void AddWrappedTextLines(
        AnnotationItem item,
        Rect contentRect,
        int rawStart,
        int rawLength,
        List<TextVisualLine> visualLines)
    {
        if (rawLength == 0)
        {
            visualLines.Add(CreateVisualLine(item, contentRect, rawStart, 0));
            return;
        }

        var lineStart = rawStart;
        var lineEnd = rawStart;
        var rawEnd = rawStart + rawLength;
        while (lineStart < rawEnd)
        {
            lineEnd = lineStart;
            while (lineEnd < rawEnd)
            {
                var nextEnd = lineEnd + 1;
                var width = MeasureTextRange(item, lineStart, nextEnd - lineStart);
                if (width > contentRect.Width && lineEnd > lineStart)
                {
                    break;
                }

                lineEnd = nextEnd;
            }

            if (lineEnd == lineStart)
            {
                lineEnd = Math.Min(rawEnd, lineStart + 1);
            }

            visualLines.Add(CreateVisualLine(item, contentRect, lineStart, lineEnd - lineStart));
            lineStart = lineEnd;
        }
    }

    private TextVisualLine CreateVisualLine(AnnotationItem item, Rect contentRect, int start, int length)
    {
        var width = Math.Min(contentRect.Width, MeasureTextRange(item, start, length));
        var height = Math.Max(1, MaxTextSizeInRange(item, start, length) * 1.3);
        var left = item.TextHorizontalPlacement switch
        {
            TextHorizontalPlacement.Right => contentRect.Right - width,
            TextHorizontalPlacement.Center => contentRect.Left + Math.Max(0, (contentRect.Width - width) / 2),
            _ => contentRect.Left
        };
        return new TextVisualLine(start, length, left, 0, width, height);
    }

    private double MeasureTextRange(AnnotationItem item, int start, int length)
    {
        if (length <= 0 || string.IsNullOrEmpty(item.Text))
        {
            return 0;
        }

        start = Math.Clamp(start, 0, item.Text.Length);
        var end = Math.Clamp(start + length, start, item.Text.Length);
        if (end <= start)
        {
            return 0;
        }

        var total = 0d;
        for (var i = start; i < end; i++)
        {
            total += MeasureTextWidth(item.Text[i].ToString(), TextSizeAt(item, i));
        }

        return total;
    }

    private double MeasureTextWidth(string text, TextSize size)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var key = new TextMeasureKey(text, size, _pixelsPerDip);
        if (_textMeasureCache.TryGetValue(key, out var cachedWidth))
        {
            return cachedWidth;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            size.ToPixels(),
            Brushes.Black,
            _pixelsPerDip);
        var width = formatted.WidthIncludingTrailingWhitespace;
        _textMeasureCache[key] = width;
        return width;
    }

    private static double MaxTextSizeInRange(AnnotationItem item, int start, int length)
    {
        if (length <= 0)
        {
            return item.TextSize.ToPixels();
        }

        var end = Math.Clamp(start + length, 0, item.Text.Length);
        start = Math.Clamp(start, 0, item.Text.Length);
        var maxSize = item.TextSize.ToPixels();
        for (var i = start; i < end; i++)
        {
            maxSize = Math.Max(maxSize, TextSizeAt(item, i).ToPixels());
        }

        return maxSize;
    }

    private void AddCaretIfNeeded(Canvas layer, AnnotationItem item, Rect contentRect, IReadOnlyList<TextVisualLine> lines)
    {
        if (_selectedId != item.Id || !_showCaret)
        {
            return;
        }

        if (item.TextSelectionLength > 0)
        {
            return;
        }

        var caretIndex = Math.Clamp(item.TextSelectionStart, 0, item.Text.Length);
        var line = FindLineForTextIndex(lines, caretIndex);
        if (line.Top >= contentRect.Bottom || line.Top + line.Height <= contentRect.Top)
        {
            return;
        }

        var x = Math.Clamp(
            line.Left + MeasureTextRange(item, line.Start, caretIndex - line.Start),
            contentRect.Left,
            contentRect.Right - 1);
        var y = Math.Max(line.Top, contentRect.Top);
        var height = Math.Min(line.Height - (y - line.Top), contentRect.Bottom - y);
        if (height <= 0)
        {
            return;
        }

        var caret = new Line
        {
            X1 = x - contentRect.Left,
            X2 = x - contentRect.Left,
            Y1 = y - contentRect.Top,
            Y2 = y - contentRect.Top + height,
            Stroke = item.Color.ToBrush(),
            StrokeThickness = 1.5,
            IsHitTestVisible = false
        };
        layer.Children.Add(caret);
    }

    private void CopySelected()
    {
        if (TryGetSelectedTextTarget(out var textTarget) && textTarget.TextSelectionLength > 0)
        {
            var start = Math.Clamp(textTarget.TextSelectionStart, 0, textTarget.Text.Length);
            var length = Math.Min(textTarget.TextSelectionLength, textTarget.Text.Length - start);
            if (length > 0)
            {
                _clipboardService.SetText(textTarget.Text.Substring(start, length));
                _copiedItems = [];
            }
            return;
        }

        if (_selectedIds.Count == 0)
        {
            return;
        }

        _copiedItems = _items
            .Where(item => _selectedIds.Contains(item.Id))
            .Select(item => item.Clone(newId: true))
            .ToList();
    }

    private void PasteCopied()
    {
        if (TryGetSelectedTextTarget(out var textTarget) &&
            (_isEditingText || textTarget.TextSelectionLength > 0) &&
            _clipboardService.TryGetText(out var clipboardText))
        {
            PushUndo();
            ReplaceSelectedText(textTarget, clipboardText, _activeColor, _activeTextSize);
            FitTextItemToContent(textTarget);
            _isEditingText = true;
            ShowCaretNow();
            RenderAnnotations();
            return;
        }

        if (_copiedItems.Count == 0)
        {
            return;
        }

        PushUndo();
        var clones = _copiedItems.Select(item => item.Clone(newId: true)).ToList();
        var bounds = BoundsForItems(clones);
        var target = _mouseInsideCanvas ? _lastMousePosition : bounds.TopLeft + new Vector(18, 18);
        var currentCenter = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        var delta = target - currentCenter;
        foreach (var clone in clones)
        {
            clone.Offset(delta);
            _items.Add(clone);
        }

        _selectedIds.Clear();
        foreach (var clone in clones)
        {
            _selectedIds.Add(clone.Id);
        }
        _selectedId = clones.Count == 1 ? clones[0].Id : null;
        _isEditingText = false;
        RenderAnnotations();
    }

    private static Rect BoundsForItems(IEnumerable<AnnotationItem> items)
    {
        var bounds = Rect.Empty;
        foreach (var item in items)
        {
            bounds = bounds.IsEmpty ? item.Bounds : Rect.Union(bounds, item.Bounds);
        }

        return bounds.IsEmpty ? new Rect(0, 0, 1, 1) : bounds;
    }

    private bool TryGetSelectedTextTarget(out AnnotationItem item)
    {
        item = null!;
        if (_selectedId is null || _selectedIds.Count > 1)
        {
            return false;
        }

        var selected = _items.FirstOrDefault(candidate => candidate.Id == _selectedId.Value);
        if (selected is null ||
            selected.Kind is not (AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Text))
        {
            return false;
        }

        item = selected;
        return true;
    }

    private void ApplyStyleToSelected(Action<AnnotationItem> apply)
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var selectedItems = _items.Where(candidate => _selectedIds.Contains(candidate.Id)).ToList();
        if (selectedItems.Count == 0)
        {
            return;
        }

        PushUndo();
        foreach (var selected in selectedItems)
        {
            apply(selected);
            FitTextItemToContent(selected);
        }
        RenderAnnotations();
    }

    private void ApplyColorToSelected(AnnotationColor color)
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var selectedItems = _items
            .Where(candidate => _selectedIds.Contains(candidate.Id) && candidate.Kind != AnnotationKind.Pencil)
            .ToList();
        if (selectedItems.Count == 0)
        {
            return;
        }

        PushUndo();
        foreach (var selected in selectedItems)
        {
            PreserveExistingTextColor(selected);
            selected.Color = color;
            if (selected.Kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Text)
            {
                selected.TextColor = color;
            }
            FitTextItemToContent(selected);
        }
        RenderAnnotations();
    }

    private void ApplyThicknessToSelected(ModelThickness thickness)
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var selectedItems = _items
            .Where(candidate => _selectedIds.Contains(candidate.Id) && candidate.Kind != AnnotationKind.Pencil)
            .ToList();
        if (selectedItems.Count == 0)
        {
            return;
        }

        PushUndo();
        foreach (var selected in selectedItems)
        {
            selected.Thickness = thickness;
            FitTextItemToContent(selected);
        }
        RenderAnnotations();
    }

    private void SelectOnly(Guid id)
    {
        _selectedIds.Clear();
        _selectedIds.Add(id);
        _selectedId = id;
        _isEditingText = false;
        var selected = _items.FirstOrDefault(item => item.Id == id);
        if (selected?.Kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Text)
        {
            selected.TextSelectionStart = selected.Text.Length;
            selected.TextSelectionLength = 0;
        }
    }

    private void ClearSelection()
    {
        _selectedIds.Clear();
        _selectedId = null;
        _isEditingText = false;
        foreach (var item in _items)
        {
            ResetTextSelection(item);
        }
    }

    private void CompleteAreaSelection()
    {
        var selection = NormalizeRect(_selectionArea);
        var leftToRight = _lastMousePosition.X >= _dragStart.X;
        _selectedIds.Clear();
        foreach (var item in _items)
        {
            var bounds = item.Bounds;
            if (leftToRight ? ContainsRect(selection, bounds) : selection.IntersectsWith(bounds))
            {
                _selectedIds.Add(item.Id);
            }
        }

        _selectedId = _selectedIds.Count == 1 ? _selectedIds.First() : null;
        _isEditingText = false;
    }

    private static Rect NormalizeRect(Rect rect) =>
        new(
            Math.Min(rect.Left, rect.Right),
            Math.Min(rect.Top, rect.Bottom),
            Math.Abs(rect.Width),
            Math.Abs(rect.Height));

    private static bool ContainsRect(Rect outer, Rect inner) =>
        outer.Left <= inner.Left &&
        outer.Top <= inner.Top &&
        outer.Right >= inner.Right &&
        outer.Bottom >= inner.Bottom;

    private void ResetDrawingDefaults()
    {
        _activeColor = AnnotationColor.Red;
        foreach (var item in _items)
        {
            ResetTextSelection(item);
        }
    }

    private static void ResetTextSelection(AnnotationItem item)
    {
        item.TextSelectionStart = 0;
        item.TextSelectionLength = 0;
    }

    private static void PreserveExistingTextColor(AnnotationItem item)
    {
        if (item.Kind is not (AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Text) ||
            string.IsNullOrEmpty(item.Text))
        {
            return;
        }

        var covered = new bool[item.Text.Length];
        foreach (var span in NormalizeTextSpans(item))
        {
            var end = Math.Min(item.Text.Length, span.Start + span.Length);
            for (var i = span.Start; i < end; i++)
            {
                covered[i] = true;
            }
        }

        var next = item.TextColorSpans.Select(span => span.Clone()).ToList();
        var start = -1;
        for (var i = 0; i < covered.Length; i++)
        {
            if (!covered[i] && start < 0)
            {
                start = i;
            }
            else if (covered[i] && start >= 0)
            {
                next.Add(new TextColorSpan
                {
                    Start = start,
                    Length = i - start,
                    Color = item.TextColor
                });
                start = -1;
            }
        }

        if (start >= 0)
        {
            next.Add(new TextColorSpan
            {
                Start = start,
                Length = covered.Length - start,
                Color = item.TextColor
            });
        }

        item.TextColorSpans = MergeAdjacentTextSpans(next);
    }

    private static void PreserveExistingTextSize(AnnotationItem item)
    {
        if (item.Kind is not (AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Text) ||
            string.IsNullOrEmpty(item.Text))
        {
            return;
        }

        var covered = new bool[item.Text.Length];
        foreach (var span in NormalizeTextSizeSpans(item))
        {
            var end = Math.Min(item.Text.Length, span.Start + span.Length);
            for (var i = span.Start; i < end; i++)
            {
                covered[i] = true;
            }
        }

        var next = item.TextSizeSpans.Select(span => span.Clone()).ToList();
        var start = -1;
        for (var i = 0; i < covered.Length; i++)
        {
            if (!covered[i] && start < 0)
            {
                start = i;
            }
            else if (covered[i] && start >= 0)
            {
                next.Add(new TextSizeSpan
                {
                    Start = start,
                    Length = i - start,
                    Size = item.TextSize
                });
                start = -1;
            }
        }

        if (start >= 0)
        {
            next.Add(new TextSizeSpan
            {
                Start = start,
                Length = covered.Length - start,
                Size = item.TextSize
            });
        }

        item.TextSizeSpans = MergeAdjacentTextSizeSpans(next);
    }

    private static IEnumerable<TextColorSpan> NormalizeTextSpans(AnnotationItem item)
    {
        return item.TextColorSpans
            .Where(span => span.Length > 0 && span.Start < item.Text.Length)
            .Select(span => new TextColorSpan
            {
                Start = Math.Clamp(span.Start, 0, item.Text.Length),
                Length = Math.Min(span.Length, item.Text.Length - Math.Clamp(span.Start, 0, item.Text.Length)),
                Color = span.Color
            })
            .Where(span => span.Length > 0)
            .OrderBy(span => span.Start);
    }

    private static IEnumerable<TextSizeSpan> NormalizeTextSizeSpans(AnnotationItem item)
    {
        return item.TextSizeSpans
            .Where(span => span.Length > 0 && span.Start < item.Text.Length)
            .Select(span => new TextSizeSpan
            {
                Start = Math.Clamp(span.Start, 0, item.Text.Length),
                Length = Math.Min(span.Length, item.Text.Length - Math.Clamp(span.Start, 0, item.Text.Length)),
                Size = span.Size
            })
            .Where(span => span.Length > 0)
            .OrderBy(span => span.Start);
    }

    private static void ApplyTextColor(AnnotationItem item, int start, int length, AnnotationColor color)
    {
        start = Math.Clamp(start, 0, item.Text.Length);
        length = Math.Min(length, item.Text.Length - start);
        if (length <= 0)
        {
            item.TextColor = color;
            return;
        }

        var end = start + length;
        var next = new List<TextColorSpan>();
        foreach (var span in NormalizeTextSpans(item))
        {
            var spanEnd = span.Start + span.Length;
            if (spanEnd <= start || span.Start >= end)
            {
                next.Add(span.Clone());
                continue;
            }

            if (span.Start < start)
            {
                next.Add(new TextColorSpan
                {
                    Start = span.Start,
                    Length = start - span.Start,
                    Color = span.Color
                });
            }

            if (spanEnd > end)
            {
                next.Add(new TextColorSpan
                {
                    Start = end,
                    Length = spanEnd - end,
                    Color = span.Color
                });
            }
        }

        next.Add(new TextColorSpan { Start = start, Length = length, Color = color });
        item.TextColorSpans = MergeAdjacentTextSpans(next);
    }

    private static void ApplyTextSize(AnnotationItem item, int start, int length, TextSize size)
    {
        start = Math.Clamp(start, 0, item.Text.Length);
        length = Math.Min(length, item.Text.Length - start);
        if (length <= 0)
        {
            item.TextSize = size;
            return;
        }

        var end = start + length;
        var next = new List<TextSizeSpan>();
        foreach (var span in NormalizeTextSizeSpans(item))
        {
            var spanEnd = span.Start + span.Length;
            if (spanEnd <= start || span.Start >= end)
            {
                next.Add(span.Clone());
                continue;
            }

            if (span.Start < start)
            {
                next.Add(new TextSizeSpan
                {
                    Start = span.Start,
                    Length = start - span.Start,
                    Size = span.Size
                });
            }

            if (spanEnd > end)
            {
                next.Add(new TextSizeSpan
                {
                    Start = end,
                    Length = spanEnd - end,
                    Size = span.Size
                });
            }
        }

        next.Add(new TextSizeSpan { Start = start, Length = length, Size = size });
        item.TextSizeSpans = MergeAdjacentTextSizeSpans(next);
    }

    private static List<TextColorSpan> MergeAdjacentTextSpans(IEnumerable<TextColorSpan> spans)
    {
        var ordered = spans
            .Where(span => span.Length > 0)
            .OrderBy(span => span.Start)
            .ToList();
        var merged = new List<TextColorSpan>();
        foreach (var span in ordered)
        {
            if (merged.LastOrDefault() is { } last &&
                last.Color == span.Color &&
                last.Start + last.Length == span.Start)
            {
                last.Length += span.Length;
            }
            else
            {
                merged.Add(span.Clone());
            }
        }

        return merged;
    }

    private static List<TextSizeSpan> MergeAdjacentTextSizeSpans(IEnumerable<TextSizeSpan> spans)
    {
        var ordered = spans
            .Where(span => span.Length > 0)
            .OrderBy(span => span.Start)
            .ToList();
        var merged = new List<TextSizeSpan>();
        foreach (var span in ordered)
        {
            if (merged.LastOrDefault() is { } last &&
                last.Size == span.Size &&
                last.Start + last.Length == span.Start)
            {
                last.Length += span.Length;
            }
            else
            {
                merged.Add(span.Clone());
            }
        }

        return merged;
    }

    private static void ReplaceSelectedText(AnnotationItem item, string replacement, AnnotationColor replacementColor, TextSize replacementSize)
    {
        if (item.TextSelectionLength > 0)
        {
            var start = Math.Clamp(item.TextSelectionStart, 0, item.Text.Length);
            var length = Math.Min(item.TextSelectionLength, item.Text.Length - start);
            item.Text = item.Text.Remove(start, length).Insert(start, replacement);
            ReplaceTextSpans(item, start, length, replacement.Length);
            ReplaceTextSizeSpans(item, start, length, replacement.Length);
            if (replacement.Length > 0)
            {
                ApplyTextColor(item, start, replacement.Length, replacementColor);
                ApplyTextSize(item, start, replacement.Length, replacementSize);
            }
            item.TextSelectionStart = start + replacement.Length;
            item.TextSelectionLength = 0;
            return;
        }

        var insertAt = Math.Clamp(item.TextSelectionStart, 0, item.Text.Length);
        item.Text = item.Text.Insert(insertAt, replacement);
        ReplaceTextSpans(item, insertAt, 0, replacement.Length);
        ReplaceTextSizeSpans(item, insertAt, 0, replacement.Length);
        if (replacement.Length > 0)
        {
            ApplyTextColor(item, insertAt, replacement.Length, replacementColor);
            ApplyTextSize(item, insertAt, replacement.Length, replacementSize);
        }
        item.TextSelectionStart = insertAt + replacement.Length;
        item.TextSelectionLength = 0;
    }

    private static void DeleteBackspace(AnnotationItem item)
    {
        if (item.TextSelectionLength > 0)
        {
            ReplaceSelectedText(item, string.Empty, item.TextColor, item.TextSize);
            return;
        }

        var caret = Math.Clamp(item.TextSelectionStart, 0, item.Text.Length);
        if (caret == 0 && item.Text.Length > 0)
        {
            caret = item.Text.Length;
        }

        if (caret == 0)
        {
            return;
        }

        var deleteLength = 1;
        var deleteStart = caret - 1;
        if (deleteStart > 0 &&
            item.Text[deleteStart] == '\n' &&
            item.Text[deleteStart - 1] == '\r')
        {
            deleteStart--;
            deleteLength = 2;
        }

        item.Text = item.Text.Remove(deleteStart, deleteLength);
        ReplaceTextSpans(item, deleteStart, deleteLength, 0);
        ReplaceTextSizeSpans(item, deleteStart, deleteLength, 0);
        item.TextSelectionStart = deleteStart;
        item.TextSelectionLength = 0;
    }

    private static void ReplaceTextSpans(AnnotationItem item, int start, int removedLength, int insertedLength)
    {
        if (item.TextColorSpans.Count == 0)
        {
            return;
        }

        var removedEnd = start + removedLength;
        var delta = insertedLength - removedLength;
        var oldTextLength = item.Text.Length - delta;
        var next = new List<TextColorSpan>();
        foreach (var existing in item.TextColorSpans.OrderBy(span => span.Start))
        {
            var spanStart = Math.Clamp(existing.Start, 0, Math.Max(0, oldTextLength));
            var spanLength = Math.Min(existing.Length, Math.Max(0, oldTextLength - spanStart));
            if (spanLength <= 0)
            {
                continue;
            }

            var span = new TextColorSpan
            {
                Start = spanStart,
                Length = spanLength,
                Color = existing.Color
            };
            var spanEnd = span.Start + span.Length;
            if (spanEnd <= start)
            {
                next.Add(span.Clone());
            }
            else if (span.Start >= removedEnd)
            {
                next.Add(new TextColorSpan
                {
                    Start = span.Start + delta,
                    Length = span.Length,
                    Color = span.Color
                });
            }
            else
            {
                if (span.Start < start)
                {
                    next.Add(new TextColorSpan
                    {
                        Start = span.Start,
                        Length = start - span.Start,
                        Color = span.Color
                    });
                }

                if (spanEnd > removedEnd)
                {
                    next.Add(new TextColorSpan
                    {
                        Start = start + insertedLength,
                        Length = spanEnd - removedEnd,
                        Color = span.Color
                    });
                }
            }
        }

        item.TextColorSpans = MergeAdjacentTextSpans(next);
    }

    private static void ReplaceTextSizeSpans(AnnotationItem item, int start, int removedLength, int insertedLength)
    {
        if (item.TextSizeSpans.Count == 0)
        {
            return;
        }

        var removedEnd = start + removedLength;
        var delta = insertedLength - removedLength;
        var oldTextLength = item.Text.Length - delta;
        var next = new List<TextSizeSpan>();
        foreach (var existing in item.TextSizeSpans.OrderBy(span => span.Start))
        {
            var spanStart = Math.Clamp(existing.Start, 0, Math.Max(0, oldTextLength));
            var spanLength = Math.Min(existing.Length, Math.Max(0, oldTextLength - spanStart));
            if (spanLength <= 0)
            {
                continue;
            }

            var span = new TextSizeSpan
            {
                Start = spanStart,
                Length = spanLength,
                Size = existing.Size
            };
            var spanEnd = span.Start + span.Length;
            if (spanEnd <= start)
            {
                next.Add(span.Clone());
            }
            else if (span.Start >= removedEnd)
            {
                next.Add(new TextSizeSpan
                {
                    Start = span.Start + delta,
                    Length = span.Length,
                    Size = span.Size
                });
            }
            else
            {
                if (span.Start < start)
                {
                    next.Add(new TextSizeSpan
                    {
                        Start = span.Start,
                        Length = start - span.Start,
                        Size = span.Size
                    });
                }

                if (spanEnd > removedEnd)
                {
                    next.Add(new TextSizeSpan
                    {
                        Start = start + insertedLength,
                        Length = spanEnd - removedEnd,
                        Size = span.Size
                    });
                }
            }
        }

        item.TextSizeSpans = MergeAdjacentTextSizeSpans(next);
    }

    private bool TryPointToTextIndex(AnnotationItem item, Point point, out int index)
    {
        index = 0;
        if (!TryGetTextContentRect(item, out var contentRect) || !contentRect.Contains(point))
        {
            return false;
        }

        var lines = BuildTextLayout(item, contentRect);
        var line = FindLineForPoint(lines, point);
        var column = 0;
        for (var candidate = 1; candidate <= line.Length; candidate++)
        {
            var width = MeasureTextRange(item, line.Start, candidate);
            if (line.Left + width - point.X > 0)
            {
                break;
            }

            column = candidate;
        }

        index = Math.Clamp(line.Start + column, 0, item.Text.Length);
        return true;
    }

    private static bool TryGetTextContentRect(AnnotationItem item, out Rect contentRect)
    {
        if (item.Kind is not (AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Text))
        {
            contentRect = Rect.Empty;
            return false;
        }

        var rect = item.Bounds;
        var padding = TextPadding(item);
        if (item.Kind == AnnotationKind.Ellipse)
        {
            var innerWidth = Math.Max(1, rect.Width / Math.Sqrt(2));
            var innerHeight = Math.Max(1, rect.Height / Math.Sqrt(2));
            rect = new Rect(
                rect.Left + (rect.Width - innerWidth) / 2,
                rect.Top + (rect.Height - innerHeight) / 2,
                innerWidth,
                innerHeight);
        }

        contentRect = new Rect(
            rect.Left + padding,
            rect.Top + padding,
            Math.Max(1, rect.Width - padding * 2),
            Math.Max(1, rect.Height - padding * 2));
        return true;
    }

    private TextVisualLine FindLineForPoint(IReadOnlyList<TextVisualLine> lines, Point point)
    {
        if (lines.Count == 0)
        {
            return new TextVisualLine(0, 0, point.X, point.Y, 0, 1);
        }

        foreach (var line in lines)
        {
            if (point.Y >= line.Top && point.Y <= line.Top + line.Height)
            {
                return line;
            }
        }

        return point.Y < lines[0].Top ? lines[0] : lines[^1];
    }

    private static TextVisualLine FindLineForTextIndex(IReadOnlyList<TextVisualLine> lines, int index)
    {
        if (lines.Count == 0)
        {
            return new TextVisualLine(0, 0, 0, 0, 0, 1);
        }

        var clamped = Math.Max(0, index);
        foreach (var line in lines)
        {
            if (clamped >= line.Start && clamped <= line.Start + line.Length)
            {
                return line;
            }
        }

        return lines[^1];
    }

    private void AddTextSelectionHighlight(Canvas layer, AnnotationItem item, Rect contentRect, IReadOnlyList<TextVisualLine> lines)
    {
        if (_selectedId != item.Id || item.TextSelectionLength <= 0)
        {
            return;
        }

        var start = Math.Clamp(item.TextSelectionStart, 0, item.Text.Length);
        var end = Math.Clamp(start + item.TextSelectionLength, 0, item.Text.Length);
        foreach (var line in lines)
        {
            var lineStart = line.Start;
            var lineEnd = line.Start + line.Length;
            var overlapStart = Math.Max(start, lineStart);
            var overlapEnd = Math.Min(end, lineEnd);
            if (overlapEnd > overlapStart)
            {
                var x = line.Left + MeasureTextRange(item, lineStart, overlapStart - lineStart);
                var y = Math.Max(line.Top, contentRect.Top);
                var height = Math.Min(line.Height - (y - line.Top), contentRect.Bottom - y);
                if (height <= 0)
                {
                    continue;
                }

                var width = Math.Min(
                    MeasureTextRange(item, overlapStart, overlapEnd - overlapStart),
                    Math.Max(0, contentRect.Right - x));
                if (width <= 0)
                {
                    continue;
                }

                var highlight = new Rectangle
                {
                    Width = Math.Max(4, width),
                    Height = height,
                    Fill = Brushes.DeepSkyBlue,
                    Opacity = 0.35,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(highlight, x - contentRect.Left);
                Canvas.SetTop(highlight, y - contentRect.Top);
                layer.Children.Add(highlight);
            }
        }
    }

    private void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        _redo.Push(CloneItems(_items));
        ReplaceItems(_undo.Pop());
    }

    private void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _undo.Push(CloneItems(_items));
        ReplaceItems(_redo.Pop());
    }

    private void PushUndo()
    {
        _undo.Push(CloneItems(_items));
        _redo.Clear();
    }

    private void ReplaceItems(List<AnnotationItem> items)
    {
        _items.Clear();
        _items.AddRange(CloneItems(items));
        _selectedIds.Clear();
        if (_items.LastOrDefault() is { } last)
        {
            SelectOnly(last.Id);
        }
        else
        {
            _selectedId = null;
        }
        RenderAnnotations();
    }

    private static List<AnnotationItem> CloneItems(IEnumerable<AnnotationItem> items) =>
        items.Select(item => item.Clone()).ToList();

    private BitmapSource RenderFinalImage()
    {
        ClearSelection();
        RenderAnnotations();
        _surface.Measure(new Size(_canvasWidth, _canvasHeight));
        _surface.Arrange(new Rect(0, 0, _canvasWidth, _canvasHeight));
        _surface.UpdateLayout();

        var render = new RenderTargetBitmap(
            (int)Math.Ceiling(_canvasWidth),
            (int)Math.Ceiling(_canvasHeight),
            _screenshot.DpiX,
            _screenshot.DpiY,
            PixelFormats.Pbgra32);
        render.Render(_surface);
        render.Freeze();
        return render;
    }

    private AnnotationColor SelectedColor() =>
        _activeColor;

    private ModelThickness SelectedThickness() =>
        _activeThickness;

    private static AnnotationKind ToolToKind(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Pencil => AnnotationKind.Pencil,
        AnnotationTool.Rectangle => AnnotationKind.Rectangle,
        AnnotationTool.Ellipse => AnnotationKind.Ellipse,
        AnnotationTool.Text => AnnotationKind.Text,
        _ => AnnotationKind.Arrow
    };

    private Point ClampToCanvas(Point point)
    {
        return new Point(
            Math.Clamp(point.X, 0, _annotationCanvas.Width),
            Math.Clamp(point.Y, 0, _annotationCanvas.Height));
    }

    private static (double Width, double Height) CalculateCanvasSize(BitmapSource screenshot)
    {
        var desktopWidth = Math.Max(SystemParameters.VirtualScreenWidth, SystemParameters.WorkArea.Width);
        var desktopHeight = Math.Max(SystemParameters.VirtualScreenHeight, SystemParameters.WorkArea.Height);
        var width = Math.Max(screenshot.PixelWidth, Math.Ceiling(desktopWidth));
        var height = Math.Max(screenshot.PixelHeight, Math.Ceiling(desktopHeight));
        return (width, height);
    }

    private void CenterCanvasViewportOnScreenshot()
    {
        if (_surfaceScrollViewer is null)
        {
            return;
        }

        _surfaceScrollViewer.UpdateLayout();
        var horizontalOffset = Math.Max(0, (_canvasWidth - _surfaceScrollViewer.ViewportWidth) / 2);
        var verticalOffset = Math.Max(0, (_canvasHeight - _surfaceScrollViewer.ViewportHeight) / 2);
        _surfaceScrollViewer.ScrollToHorizontalOffset(horizontalOffset);
        _surfaceScrollViewer.ScrollToVerticalOffset(verticalOffset);
    }

    private void OnSurfacePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control ||
            _surfaceScrollViewer is null)
        {
            return;
        }

        var oldZoom = _zoom;
        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        var newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.001)
        {
            e.Handled = true;
            return;
        }

        var mouse = e.GetPosition(_surfaceScrollViewer);
        var contentX = (_surfaceScrollViewer.HorizontalOffset + mouse.X) / oldZoom;
        var contentY = (_surfaceScrollViewer.VerticalOffset + mouse.Y) / oldZoom;

        _zoom = newZoom;
        _surfaceHost.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        _surfaceScrollViewer.UpdateLayout();
        _surfaceScrollViewer.ScrollToHorizontalOffset(Math.Max(0, contentX * _zoom - mouse.X));
        _surfaceScrollViewer.ScrollToVerticalOffset(Math.Max(0, contentY * _zoom - mouse.Y));
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _caretTimer.Stop();
        base.OnClosed(e);
    }

    private enum ResizeHandle
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    private sealed record HandleTag(Guid ItemId, ResizeHandle Handle);

    private sealed record RawTextLine(int Start, int Length);

    private sealed record TextVisualLine(
        int Start,
        int Length,
        double Left,
        double Top,
        double Width,
        double Height);

    private readonly record struct TextMeasureKey(string Text, TextSize Size, double PixelsPerDip);

    private sealed record ToolbarGroupInfo(
        string Name,
        int CollapseOrder,
        int CompactColumns,
        int ChildCount,
        WrapPanel? Panel,
        double? FullWidthOverride = null,
        double? CompactWidthOverride = null,
        Action<bool>? SetCompact = null)
    {
        public double FullWidth => FullWidthOverride ?? ChildCount * 44;
        public double CompactWidth => CompactWidthOverride ?? CompactColumns * 44;
        public double CurrentWidth => Panel?.Width ?? FullWidth;
    }
}
