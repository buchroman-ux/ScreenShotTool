using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using ScreenShotTool.Models;

namespace ScreenShotTool.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 101;
    private HwndSource? _source;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public void Register(ScreenshotHotkey hotkey)
    {
        Unregister();
        _source?.Dispose();

        var parameters = new HwndSourceParameters("ScreenShotToolHotkey")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        var modifiers = ToNativeModifiers(hotkey);
        var virtualKey = ToVirtualKey(hotkey);
        _registered = NativeMethods.RegisterHotKey(
            _source.Handle,
            HotkeyId,
            modifiers,
            virtualKey);

        if (!_registered)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not register {hotkey}. Another app may already use this hotkey.");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source?.Dispose();
    }

    private void Unregister()
    {
        if (_registered && _source is not null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
    }

    private static uint ToNativeModifiers(ScreenshotHotkey hotkey)
    {
        var modifiers = 0u;
        if (hotkey.Control)
        {
            modifiers |= NativeMethods.ModControl;
        }
        if (hotkey.Shift)
        {
            modifiers |= NativeMethods.ModShift;
        }
        if (hotkey.Alt)
        {
            modifiers |= NativeMethods.ModAlt;
        }
        if (hotkey.Windows)
        {
            modifiers |= NativeMethods.ModWindows;
        }

        if (modifiers == 0)
        {
            throw new InvalidOperationException("Choose at least one modifier key for the screenshot shortcut.");
        }

        return modifiers;
    }

    private static uint ToVirtualKey(ScreenshotHotkey hotkey)
    {
        var key = KeyFromName(hotkey.Key);
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
        {
            throw new InvalidOperationException("Choose a valid key for the screenshot shortcut.");
        }

        return (uint)virtualKey;
    }

    private static Key KeyFromName(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return Key.None;
        }

        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
        {
            return Enum.Parse<Key>(keyName.ToUpperInvariant());
        }

        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            return Enum.Parse<Key>($"D{keyName}");
        }

        return Enum.TryParse<Key>(keyName, ignoreCase: true, out var key) ? key : Key.None;
    }
}
