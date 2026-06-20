using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenShotTool.Services;
using DrawingRectangle = System.Drawing.Rectangle;
using ShapeRectangle = System.Windows.Shapes.Rectangle;

namespace ScreenShotTool.Windows;

public sealed class CaptureOverlayWindow : Window
{
    private readonly MonitorService _monitorService;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly Canvas _canvas = new();
    private readonly BitmapSource _frozenDesktop;
    private readonly Image _frozenImage = new()
    {
        Stretch = Stretch.Fill,
        IsHitTestVisible = false
    };
    private readonly ShapeRectangle _shade = new()
    {
        Fill = new SolidColorBrush(Color.FromArgb(95, 0, 0, 0)),
        IsHitTestVisible = false
    };
    private readonly ShapeRectangle _selectionRectangle = new()
    {
        Stroke = Brushes.White,
        StrokeThickness = 2,
        StrokeDashArray = [4, 2],
        Fill = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
        Visibility = Visibility.Collapsed
    };

    private Point _dragStart;
    private bool _isDragging;

    public event EventHandler<BitmapSource>? CaptureCompleted;
    public event EventHandler? CaptureCanceled;

    public CaptureOverlayWindow(MonitorService monitorService, ScreenCaptureService screenCaptureService)
    {
        _monitorService = monitorService;
        _screenCaptureService = screenCaptureService;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        _frozenDesktop = _screenCaptureService.Capture(_monitorService.VirtualBounds);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        Focusable = true;
        Content = _canvas;

        _frozenImage.Source = _frozenDesktop;
        _frozenImage.Width = Width;
        _frozenImage.Height = Height;
        _shade.Width = Width;
        _shade.Height = Height;
        _canvas.Children.Add(_frozenImage);
        _canvas.Children.Add(_shade);
        _canvas.Children.Add(_selectionRectangle);
        Loaded += (_, _) => Focus();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(_canvas);
        _isDragging = true;
        CaptureMouse();
        UpdateSelection(_dragStart, _dragStart);
        _selectionRectangle.Visibility = Visibility.Visible;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        UpdateSelection(_dragStart, e.GetPosition(_canvas));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();
        var end = e.GetPosition(_canvas);
        var rect = CreateRect(_dragStart, end);
        if (rect.Width < 3 || rect.Height < 3)
        {
            Cancel();
            return;
        }

        var captureBounds = ToPixelBounds(rect);
        try
        {
            var image = CropFrozenDesktop(captureBounds);
            CaptureCompleted?.Invoke(this, image);
        }
        finally
        {
            Close();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancel();
        }
    }

    private void Cancel()
    {
        CaptureCanceled?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void UpdateSelection(Point start, Point end)
    {
        var rect = CreateRect(start, end);
        Canvas.SetLeft(_selectionRectangle, rect.Left);
        Canvas.SetTop(_selectionRectangle, rect.Top);
        _selectionRectangle.Width = rect.Width;
        _selectionRectangle.Height = rect.Height;
    }

    private static Rect CreateRect(Point start, Point end)
    {
        return new Rect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(start.X - end.X),
            Math.Abs(start.Y - end.Y));
    }

    private DrawingRectangle ToPixelBounds(Rect dipRect)
    {
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var virtualBounds = _monitorService.VirtualBounds;

        return new DrawingRectangle(
            virtualBounds.Left + (int)Math.Round(dipRect.Left * transform.M11),
            virtualBounds.Top + (int)Math.Round(dipRect.Top * transform.M22),
            Math.Max(1, (int)Math.Round(dipRect.Width * transform.M11)),
            Math.Max(1, (int)Math.Round(dipRect.Height * transform.M22)));
    }

    private BitmapSource CropFrozenDesktop(DrawingRectangle bounds)
    {
        var virtualBounds = _monitorService.VirtualBounds;
        var x = Math.Clamp(bounds.Left - virtualBounds.Left, 0, Math.Max(0, _frozenDesktop.PixelWidth - 1));
        var y = Math.Clamp(bounds.Top - virtualBounds.Top, 0, Math.Max(0, _frozenDesktop.PixelHeight - 1));
        var right = Math.Clamp(bounds.Right - virtualBounds.Left, x + 1, _frozenDesktop.PixelWidth);
        var bottom = Math.Clamp(bounds.Bottom - virtualBounds.Top, y + 1, _frozenDesktop.PixelHeight);
        var cropped = new CroppedBitmap(_frozenDesktop, new Int32Rect(x, y, right - x, bottom - y));
        cropped.Freeze();
        return cropped;
    }
}
