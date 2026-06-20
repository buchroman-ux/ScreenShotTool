using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace ScreenShotTool.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayService(Action capture, Action settings, Action help, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Capture Screenshot", null, (_, _) => capture());
        menu.Items.Add("Open Settings", null, (_, _) => settings());
        menu.Items.Add("Help / Shortcuts", null, (_, _) => help());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "ScreenShotTool",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => capture();
    }

    public void ShowInfo(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
        {
            return Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
        }

        return SystemIcons.Application;
    }
}
