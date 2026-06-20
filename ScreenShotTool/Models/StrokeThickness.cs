namespace ScreenShotTool.Models;

public enum StrokeThickness
{
    Thin,
    Medium,
    Thick,
    ExtraThick
}

public static class StrokeThicknessExtensions
{
    public static double ToPixels(this StrokeThickness thickness) => thickness switch
    {
        StrokeThickness.Thin => 2,
        StrokeThickness.Thick => 8,
        StrokeThickness.ExtraThick => 13,
        _ => 4
    };
}
