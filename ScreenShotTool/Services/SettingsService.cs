using System.IO;
using System.Text.Json;
using ScreenShotTool.Models;

namespace ScreenShotTool.Services;

public sealed class SettingsService
{
    private readonly string _settingsFile;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "ScreenShotTool");
        Directory.CreateDirectory(folder);
        _settingsFile = Path.Combine(folder, "settings.json");
    }

    public AppSettings Settings { get; private set; } = new();

    public string DefaultScreenshotFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");

    public void Load()
    {
        if (!File.Exists(_settingsFile))
        {
            Settings = new AppSettings { ScreenshotFolderPath = DefaultScreenshotFolder };
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsFile);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }

        if (string.IsNullOrWhiteSpace(Settings.ScreenshotFolderPath))
        {
            Settings.ScreenshotFolderPath = DefaultScreenshotFolder;
        }

        if (Settings.Hotkey is null || string.IsNullOrWhiteSpace(Settings.Hotkey.Key))
        {
            Settings.Hotkey = ScreenshotHotkey.Default;
        }
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_settingsFile, JsonSerializer.Serialize(Settings, options));
    }

    public bool TryValidateFolder(string folderPath, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            error = "Choose a screenshot folder before saving.";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            Directory.CreateDirectory(fullPath);
            var probe = Path.Combine(fullPath, $".roman_screenshot_tool_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex)
        {
            error = $"The selected folder cannot be used: {ex.Message}";
            return false;
        }
    }

    public string EnsureScreenshotFolder()
    {
        var configured = Settings.ScreenshotFolderPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            try
            {
                Directory.CreateDirectory(configured);
                return configured;
            }
            catch
            {
                // Fall back below.
            }
        }

        Directory.CreateDirectory(DefaultScreenshotFolder);
        Settings.ScreenshotFolderPath = DefaultScreenshotFolder;
        Save();
        return DefaultScreenshotFolder;
    }
}
