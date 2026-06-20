using Microsoft.Win32;

namespace ScreenShotTool.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenShotTool";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(ValueName) is string value && value.Contains(GetExecutablePath(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{GetExecutablePath()}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve the application executable path.");
    }
}
