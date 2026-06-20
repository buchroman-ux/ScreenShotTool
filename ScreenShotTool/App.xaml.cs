using System.Windows;
using ScreenShotTool.Services;
using ScreenShotTool.Windows;

namespace ScreenShotTool;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\ScreenShotTool.SingleInstance";

    private System.Threading.Mutex? _singleInstanceMutex;
    private SettingsService _settingsService = null!;
    private StartupService _startupService = null!;
    private ThemeService _themeService = null!;
    private MonitorService? _monitorService;
    private ScreenCaptureService? _screenCaptureService;
    private SaveService? _saveService;
    private ClipboardService? _clipboardService;
    private HotkeyService _hotkeyService = null!;
    private TrayService _trayService = null!;
    private EditorWindow? _activeEditor;
    private CaptureOverlayWindow? _captureOverlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new System.Threading.Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "ScreenShotTool is already running.",
                "ScreenShotTool",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);

        _settingsService = new SettingsService();
        _settingsService.Load();
        _startupService = new StartupService();
        _themeService = new ThemeService();
        _themeService.Apply(_settingsService.Settings.ThemeMode);

        _trayService = new TrayService(BeginCapture, OpenSettings, OpenHelp, ExitApplication);

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += (_, _) => HandleScreenshotHotkey();
        try
        {
            _hotkeyService.Register(_settingsService.Settings.Hotkey);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ScreenShotTool", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void HandleScreenshotHotkey()
    {
        Dispatcher.Invoke(() =>
        {
            if (_activeEditor is not null)
            {
                FinalizeEditor();
                return;
            }

            BeginCapture();
        });
    }

    private void BeginCapture()
    {
        if (_activeEditor is not null || _captureOverlay is not null)
        {
            return;
        }

        _captureOverlay = new CaptureOverlayWindow(MonitorService, ScreenCaptureService);
        _captureOverlay.CaptureCompleted += (_, image) =>
        {
            _captureOverlay = null;
            OpenEditor(image);
        };
        _captureOverlay.CaptureCanceled += (_, _) => _captureOverlay = null;
        _captureOverlay.Show();
        _captureOverlay.Activate();
    }

    private void OpenEditor(System.Windows.Media.Imaging.BitmapSource screenshot)
    {
        _activeEditor = new EditorWindow(screenshot, SaveService, ClipboardService, OpenSettings, OpenHelp);
        _activeEditor.Closed += (_, _) => _activeEditor = null;
        _activeEditor.Show();
        _activeEditor.Activate();
    }

    private void FinalizeEditor()
    {
        if (_activeEditor is null)
        {
            return;
        }

        try
        {
            var savedPath = _activeEditor.FinalizeImage();
            _trayService.ShowInfo("Screenshot saved", savedPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not save screenshot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenSettings()
    {
        try
        {
            var window = new SettingsWindow(_settingsService, _startupService, _themeService, ApplyHotkeySettings);
            window.Owner = _activeEditor;
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not open Settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyHotkeySettings()
    {
        _hotkeyService.Register(_settingsService.Settings.Hotkey);
    }

    private static void OpenHelp()
    {
        var window = new HelpWindow();
        window.Show();
        window.Activate();
    }

    private void ExitApplication()
    {
        _activeEditor?.Close();
        _captureOverlay?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        _hotkeyService?.Dispose();
        _monitorService?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private MonitorService MonitorService => _monitorService ??= new MonitorService();

    private ScreenCaptureService ScreenCaptureService => _screenCaptureService ??= new ScreenCaptureService();

    private SaveService SaveService => _saveService ??= new SaveService(_settingsService);

    private ClipboardService ClipboardService => _clipboardService ??= new ClipboardService();
}
