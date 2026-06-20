namespace ScreenShotTool.Models;

public sealed class TextSizeSpan
{
    public int Start { get; set; }
    public int Length { get; set; }
    public TextSize Size { get; set; } = TextSize.Medium;

    public TextSizeSpan Clone() => new()
    {
        Start = Start,
        Length = Length,
        Size = Size
    };
}
