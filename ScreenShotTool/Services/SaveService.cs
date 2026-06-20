using System.IO;
using System.Windows.Media.Imaging;

namespace ScreenShotTool.Services;

public sealed class SaveService(SettingsService settingsService)
{
    public string SavePng(BitmapSource image)
    {
        var folder = settingsService.EnsureScreenshotFolder();
        var fileName = $"screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        var path = Path.Combine(folder, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
