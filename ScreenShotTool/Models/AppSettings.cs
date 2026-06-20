namespace ScreenShotTool.Models;

public sealed class AppSettings
{
    public string? ScreenshotFolderPath { get; set; }
    public bool StartWithWindows { get; set; }
    public ThemeMode ThemeMode { get; set; } = ThemeMode.WindowsDefault;
    public ScreenshotHotkey Hotkey { get; set; } = ScreenshotHotkey.Default;
}

public sealed class ScreenshotHotkey
{
    public bool Control { get; set; } = true;
    public bool Shift { get; set; } = true;
    public bool Alt { get; set; }
    public bool Windows { get; set; }
    public string Key { get; set; } = "S";

    public static ScreenshotHotkey Default => new();

    public ScreenshotHotkey Clone() => new()
    {
        Control = Control,
        Shift = Shift,
        Alt = Alt,
        Windows = Windows,
        Key = Key
    };

    public override string ToString()
    {
        var parts = new List<string>();
        if (Control)
        {
            parts.Add("Ctrl");
        }
        if (Shift)
        {
            parts.Add("Shift");
        }
        if (Alt)
        {
            parts.Add("Alt");
        }
        if (Windows)
        {
            parts.Add("Win");
        }
        parts.Add(Key);
        return string.Join(" + ", parts);
    }
}
