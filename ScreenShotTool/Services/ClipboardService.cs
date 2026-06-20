using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenShotTool.Services;

public sealed class ClipboardService
{
    public void SetImage(BitmapSource image)
    {
        Clipboard.SetImage(image);
    }

    public void SetText(string text)
    {
        Clipboard.SetText(text);
    }

    public bool TryGetText(out string text)
    {
        text = string.Empty;
        if (!Clipboard.ContainsText())
        {
            return false;
        }

        text = Clipboard.GetText();
        return true;
    }
}
