using System.Drawing;
using Microsoft.Win32;
using ScreenShotTool.Models;
using Forms = System.Windows.Forms;

namespace ScreenShotTool.Services;

public sealed class MonitorService : IDisposable
{
    private readonly object _lock = new();
    private List<MonitorInfo> _monitors = [];

    public MonitorService()
    {
        Refresh();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public IReadOnlyList<MonitorInfo> Monitors
    {
        get
        {
            lock (_lock)
            {
                return _monitors.ToList();
            }
        }
    }

    public Rectangle VirtualBounds
    {
        get
        {
            lock (_lock)
            {
                if (_monitors.Count == 0)
                {
                    return Rectangle.Empty;
                }

                return _monitors.Select(m => m.Bounds)
                    .Aggregate(Rectangle.Union);
            }
        }
    }

    public void Refresh()
    {
        lock (_lock)
        {
            _monitors = Forms.Screen.AllScreens
                .Select(screen => new MonitorInfo(screen.DeviceName, screen.Bounds, screen.WorkingArea, screen.Primary))
                .ToList();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Refresh();

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
