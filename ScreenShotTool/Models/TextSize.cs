namespace ScreenShotTool.Models;

public enum TextSize
{
    Small,
    Medium,
    Large,
    XLarge,
    XXLarge,
    Huge
}

public static class TextSizeExtensions
{
    public static double ToPixels(this TextSize size) => size switch
    {
        TextSize.Small => 16,
        TextSize.Medium => 24,
        TextSize.Large => 32,
        TextSize.XLarge => 56,
        TextSize.XXLarge => 84,
        TextSize.Huge => 132,
        _ => 24
    };
}
