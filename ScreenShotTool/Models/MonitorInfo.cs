using System.Drawing;

namespace ScreenShotTool.Models;

public sealed record MonitorInfo(string DeviceName, Rectangle Bounds, Rectangle WorkingArea, bool IsPrimary);
