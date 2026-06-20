using System.Windows.Media;

namespace ScreenShotTool.Models;

public enum AnnotationColor
{
    Red,
    Blue,
    Green,
    Yellow
}

public static class AnnotationColorExtensions
{
    public static Brush ToBrush(this AnnotationColor color) => color switch
    {
        AnnotationColor.Blue => Brushes.DodgerBlue,
        AnnotationColor.Green => Brushes.LimeGreen,
        AnnotationColor.Yellow => Brushes.Gold,
        _ => Brushes.Red
    };
}
