using System.Windows;

namespace ScreenShotTool.Models;

public sealed class AnnotationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AnnotationKind Kind { get; set; }
    public AnnotationColor Color { get; set; } = AnnotationColor.Red;
    public StrokeThickness Thickness { get; set; } = StrokeThickness.Medium;
    public TextSize TextSize { get; set; } = TextSize.Medium;
    public TextHorizontalPlacement TextHorizontalPlacement { get; set; } = TextHorizontalPlacement.Center;
    public TextVerticalPlacement TextVerticalPlacement { get; set; } = TextVerticalPlacement.Middle;
    public AnnotationColor TextColor { get; set; } = AnnotationColor.Red;
    public int TextSelectionStart { get; set; }
    public int TextSelectionLength { get; set; }
    public Point Start { get; set; }
    public Point End { get; set; }
    public List<Point> Points { get; set; } = [];
    public string Text { get; set; } = string.Empty;
    public List<TextColorSpan> TextColorSpans { get; set; } = [];
    public List<TextSizeSpan> TextSizeSpans { get; set; } = [];

    public AnnotationItem Clone(bool newId = false)
    {
        return new AnnotationItem
        {
            Id = newId ? Guid.NewGuid() : Id,
            Kind = Kind,
            Color = Color,
            Thickness = Thickness,
            TextSize = TextSize,
            TextHorizontalPlacement = TextHorizontalPlacement,
            TextVerticalPlacement = TextVerticalPlacement,
            TextColor = TextColor,
            TextSelectionStart = TextSelectionStart,
            TextSelectionLength = TextSelectionLength,
            Start = Start,
            End = End,
            Points = Points.ToList(),
            Text = Text,
            TextColorSpans = TextColorSpans.Select(span => span.Clone()).ToList(),
            TextSizeSpans = TextSizeSpans.Select(span => span.Clone()).ToList()
        };
    }

    public Rect Bounds
    {
        get
        {
            var left = Math.Min(Start.X, End.X);
            var top = Math.Min(Start.Y, End.Y);
            var width = Math.Abs(Start.X - End.X);
            var height = Math.Abs(Start.Y - End.Y);

            if (Kind == AnnotationKind.Pencil && Points.Count > 0)
            {
                left = Points.Min(point => point.X);
                top = Points.Min(point => point.Y);
                var right = Points.Max(point => point.X);
                var bottom = Points.Max(point => point.Y);
                var padding = Math.Max(4, Thickness.ToPixels() / 2 + 4);
                return new Rect(
                    left - padding,
                    top - padding,
                    Math.Max(1, right - left + padding * 2),
                    Math.Max(1, bottom - top + padding * 2));
            }

            if (Kind == AnnotationKind.Text)
            {
                width = Math.Max(width, 140);
                height = Math.Max(height, 36);
            }

            return new Rect(left, top, Math.Max(1, width), Math.Max(1, height));
        }
    }

    public void Offset(Vector delta)
    {
        Start += delta;
        End += delta;
        for (var i = 0; i < Points.Count; i++)
        {
            Points[i] += delta;
        }
    }
}
