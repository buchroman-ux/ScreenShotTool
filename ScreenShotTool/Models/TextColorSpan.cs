namespace ScreenShotTool.Models;

public sealed class TextColorSpan
{
    public int Start { get; set; }
    public int Length { get; set; }
    public AnnotationColor Color { get; set; } = AnnotationColor.Red;

    public TextColorSpan Clone() => new()
    {
        Start = Start,
        Length = Length,
        Color = Color
    };
}
