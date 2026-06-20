using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using ScreenShotTool.Models;

namespace ScreenShotTool.Services;

public sealed class ThemeService
{
    public void Apply(ThemeMode mode)
    {
        var isDark = mode == ThemeMode.Dark || mode == ThemeMode.WindowsDefault && IsWindowsDarkMode();
        var resources = Application.Current.Resources;

        resources["WindowBackgroundBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(35, 38, 43) : Color.FromRgb(247, 248, 250));
        resources["PanelBackgroundBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(45, 49, 55) : Colors.White);
        resources["TextBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(240, 243, 247) : Color.FromRgb(32, 36, 42));
        resources["SubtleTextBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(178, 186, 196) : Color.FromRgb(102, 112, 122));
        resources["BorderBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(78, 85, 94) : Color.FromRgb(217, 222, 230));
        resources["AccentBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(138, 180, 248) : Color.FromRgb(127, 166, 216));
        resources["ButtonBackgroundBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(55, 60, 67) : Colors.White);
        resources["ButtonHoverBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(68, 74, 83) : Color.FromRgb(240, 244, 250));
        resources["SelectedButtonBackgroundBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(70, 76, 86) : Color.FromRgb(248, 250, 252));
        resources["ComboBoxHighlightBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(51, 84, 122) : Color.FromRgb(238, 244, 255));
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
