using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ScreenShotTool.Installer;

internal static class Program
{
    private const string AppName = "ScreenShotTool";
    private const string ExeName = "ScreenShotTool.exe";
    private const string UninstallerName = "Uninstall.exe";
    private const string PayloadName = "ScreenShotToolPayload.zip";
    private const string Publisher = "Roman";
    private const string Version = "1.0.0";
    private const string DefaultHotkeyText = "Ctrl + Shift + S";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenShotTool";
    private const string StartupRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "ScreenShotTool";

    [STAThread]
    private static int Main(string[] args)
    {
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        var isSilent = HasArg(args, "/silent");

        try
        {
            if (ShouldUninstall(args))
            {
                return Uninstall(DefaultInstallDir(), DefaultStartMenuDir(), DefaultDesktopShortcut(), args);
            }

            var options = GetInstallOptions(args);
            if (options is null)
            {
                return 0;
            }

            Install(options);
            if (!options.Silent)
            {
                ShowInfo("Installation complete", $"{AppName} was installed successfully.");
            }

            if (options.LaunchAfterInstall)
            {
                Process.Start(new ProcessStartInfo(Path.Combine(options.InstallDir, ExeName)) { UseShellExecute = true });
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (!isSilent)
            {
                ShowError("Installation failed", ex.Message);
            }

            return 1;
        }
    }

    private static InstallOptions? GetInstallOptions(string[] args)
    {
        if (HasArg(args, "/silent"))
        {
            return new InstallOptions
            {
                InstallDir = GetArgValue(args, "/dir=") ?? DefaultInstallDir(),
                StartWithWindows = HasArg(args, "/startup"),
                CreateDesktopShortcut = HasArg(args, "/desktop"),
                LaunchAfterInstall = !HasArg(args, "/no-launch"),
                Silent = true
            };
        }

        using var wizard = new InstallWizardForm(DefaultInstallDir(), DefaultHotkeyText);
        return wizard.ShowDialog() == Forms.DialogResult.OK ? wizard.Options : null;
    }

    private static void Install(InstallOptions options)
    {
        var installDir = Path.GetFullPath(options.InstallDir);
        var startMenuDir = DefaultStartMenuDir();
        var desktopShortcut = DefaultDesktopShortcut();
        var startMenuShortcut = Path.Combine(startMenuDir, $"{AppName}.lnk");
        var targetExe = Path.Combine(installDir, ExeName);
        var uninstallerExe = Path.Combine(installDir, UninstallerName);

        Directory.CreateDirectory(installDir);
        ExtractPayload(installDir);
        CopyUninstaller(uninstallerExe);

        Directory.CreateDirectory(startMenuDir);
        CreateShortcut(startMenuShortcut, targetExe, installDir);

        if (options.CreateDesktopShortcut)
        {
            CreateShortcut(desktopShortcut, targetExe, installDir);
        }
        else
        {
            DeleteIfExists(desktopShortcut);
        }

        SetStartupEntry(options.StartWithWindows, targetExe);
        RegisterUninstallEntry(installDir, targetExe, uninstallerExe);
    }

    private static bool ShouldUninstall(string[] args) =>
        HasArg(args, "/uninstall");

    private static void ExtractPayload(string installDir)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadName)
            ?? throw new InvalidOperationException("Installer payload is missing.");
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var installRoot = EnsureTrailingSeparator(Path.GetFullPath(installDir));
        foreach (var entry in archive.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(installDir, entry.FullName));
            if (!destination.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Installer payload contains an invalid path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static void CopyUninstaller(string uninstallerExe)
    {
        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve installer path.");
        File.Copy(currentExe, uninstallerExe, overwrite: true);
    }

    private static void RegisterUninstallEntry(string installDir, string targetExe, string uninstallerExe)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, true)
            ?? throw new InvalidOperationException("Could not create uninstall registry entry.");

        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", Version);
        key.SetValue("Publisher", Publisher);
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", targetExe);
        key.SetValue("UninstallString", $"\"{uninstallerExe}\" /uninstall");
        key.SetValue("QuietUninstallString", $"\"{uninstallerExe}\" /uninstall /silent");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", EstimateInstallSizeKb(installDir), RegistryValueKind.DWord);
    }

    private static int EstimateInstallSizeKb(string installDir)
    {
        if (!Directory.Exists(installDir))
        {
            return 0;
        }

        var bytes = Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
        return Math.Max(1, (int)Math.Ceiling(bytes / 1024d));
    }

    private static int Uninstall(string defaultInstallDir, string startMenuDir, string desktopShortcut, string[] args)
    {
        var installDir = GetArgValue(args, "/dir=") ?? TryGetInstalledLocation() ?? defaultInstallDir;
        var uninstallerExe = Path.Combine(installDir, UninstallerName);

        if (!HasArg(args, "/silent"))
        {
            var result = Forms.MessageBox.Show(
                $"Remove {AppName} from this PC?",
                $"{AppName} Uninstaller",
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Question,
                Forms.MessageBoxDefaultButton.Button1);
            if (result != Forms.DialogResult.Yes)
            {
                return 0;
            }
        }

        StopRunningApplication();
        SetStartupEntry(false, string.Empty);
        DeleteIfExists(desktopShortcut);
        DeleteDirectoryIfExists(startMenuDir);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);

        DeleteInstalledFiles(installDir, uninstallerExe);
        ScheduleSelfDelete(installDir, uninstallerExe);

        if (!HasArg(args, "/silent"))
        {
            ShowInfo("Uninstall complete", $"{AppName} was removed.");
        }

        return 0;
    }

    private static string? TryGetInstalledLocation()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, false);
        return key?.GetValue("InstallLocation") as string;
    }

    private static void StopRunningApplication()
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(1500))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort: uninstall should continue even if a process already exited.
            }
        }
    }

    private static void SetStartupEntry(bool enabled, string targetExe)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(StartupRunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (enabled)
        {
            key.SetValue(StartupValueName, $"\"{targetExe}\"");
        }
        else
        {
            key.DeleteValue(StartupValueName, throwOnMissingValue: false);
        }
    }

    private static void DeleteInstalledFiles(string installDir, string uninstallerExe)
    {
        if (!Directory.Exists(installDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories))
        {
            if (file.Equals(uninstallerExe, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DeleteIfExists(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(installDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Keep uninstall resilient; leftover files can be removed manually.
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Keep uninstall resilient; leftover folders can be removed manually.
        }
    }

    private static void ScheduleSelfDelete(string installDir, string uninstallerExe)
    {
        var command = $"/C timeout /t 2 /nobreak >NUL & del /F /Q \"{uninstallerExe}\" >NUL 2>NUL & rmdir \"{installDir}\" >NUL 2>NUL";
        Process.Start(new ProcessStartInfo("cmd.exe", command)
        {
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false
        });
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is unavailable; shortcut creation failed.");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Could not create WScript.Shell.");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = targetPath;
        shortcut.Description = "Capture and annotate screenshots";
        shortcut.Save();
    }

    private static string DefaultInstallDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppName);

    private static string DefaultStartMenuDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            AppName);

    private static string DefaultDesktopShortcut() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{AppName}.lnk");

    private static bool HasArg(string[] args, string value) =>
        args.Any(arg => arg.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static string? GetArgValue(string[] args, string prefix) =>
        args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];

    private static void ShowInfo(string title, string message) =>
        Forms.MessageBox.Show(message, title, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);

    private static void ShowError(string title, string message) =>
        Forms.MessageBox.Show(message, title, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
}

internal sealed class InstallOptions
{
    public string InstallDir { get; set; } = string.Empty;
    public bool StartWithWindows { get; set; }
    public bool CreateDesktopShortcut { get; set; }
    public bool LaunchAfterInstall { get; set; }
    public bool Silent { get; set; }
}

internal sealed class InstallWizardForm : Forms.Form
{
    private readonly Forms.TextBox _installDirTextBox = new();
    private readonly Forms.CheckBox _startupCheckBox = new();
    private readonly Forms.CheckBox _desktopShortcutCheckBox = new();
    private readonly Forms.CheckBox _launchCheckBox = new();

    public InstallWizardForm(string defaultInstallDir, string defaultHotkey)
    {
        Text = "ScreenShotTool Setup";
        AutoScaleMode = Forms.AutoScaleMode.Dpi;
        ClientSize = new System.Drawing.Size(760, 520);
        MinimumSize = new System.Drawing.Size(720, 500);
        StartPosition = Forms.FormStartPosition.CenterScreen;
        FormBorderStyle = Forms.FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScroll = true;

        _installDirTextBox.Text = defaultInstallDir;
        _startupCheckBox.Text = "Start ScreenShotTool when Windows starts";
        _desktopShortcutCheckBox.Text = "Create a Desktop shortcut";
        _launchCheckBox.Text = "Launch ScreenShotTool after installation";
        _launchCheckBox.Checked = true;

        Controls.Add(BuildContent(defaultHotkey));
    }

    public InstallOptions Options => new()
    {
        InstallDir = _installDirTextBox.Text,
        StartWithWindows = _startupCheckBox.Checked,
        CreateDesktopShortcut = _desktopShortcutCheckBox.Checked,
        LaunchAfterInstall = _launchCheckBox.Checked
    };

    private Forms.Control BuildContent(string defaultHotkey)
    {
        var root = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Forms.Padding(28)
        };
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));

        var title = new Forms.Label
        {
            Text = "Install ScreenShotTool",
            AutoSize = true,
            Font = new System.Drawing.Font(Font.FontFamily, 16, System.Drawing.FontStyle.Bold),
            Margin = new Forms.Padding(0, 0, 0, 6)
        };
        root.Controls.Add(title, 0, 0);

        var subtitle = new Forms.Label
        {
            Text = "Choose where to install the app and whether it should start with Windows.",
            AutoSize = false,
            Dock = Forms.DockStyle.Fill,
            Height = 46,
            Margin = new Forms.Padding(0, 0, 0, 18)
        };
        root.Controls.Add(subtitle, 0, 1);

        root.Controls.Add(BuildInstallLocationRow(), 0, 2);

        var optionsPanel = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 7,
            Margin = new Forms.Padding(0, 12, 0, 0)
        };

        AddOption(optionsPanel, _startupCheckBox, "Open ScreenShotTool automatically after you sign in to Windows.");
        AddOption(optionsPanel, _desktopShortcutCheckBox, "Add a shortcut on your Desktop so you can open it quickly.");
        AddOption(optionsPanel, _launchCheckBox, "Start ScreenShotTool immediately when this setup finishes.");
        optionsPanel.Controls.Add(new Forms.Label
        {
            Text = $"Default screenshot shortcut: {defaultHotkey}",
            AutoSize = true,
            Margin = new Forms.Padding(0, 18, 0, 0)
        });
        root.Controls.Add(optionsPanel, 0, 3);

        root.Controls.Add(BuildActions(), 0, 4);
        return root;
    }

    private static void AddOption(Forms.TableLayoutPanel panel, Forms.CheckBox checkBox, string description)
    {
        checkBox.AutoSize = true;
        checkBox.Margin = new Forms.Padding(0, 4, 0, 0);
        panel.Controls.Add(checkBox);

        panel.Controls.Add(new Forms.Label
        {
            Text = description,
            AutoSize = false,
            Dock = Forms.DockStyle.Top,
            Height = 24,
            Margin = new Forms.Padding(24, 0, 0, 6),
            ForeColor = System.Drawing.SystemColors.GrayText
        });
    }

    private Forms.Control BuildInstallLocationRow()
    {
        var panel = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Top,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            Margin = new Forms.Padding(0)
        };
        panel.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 100));
        panel.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute, 118));
        panel.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        panel.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));

        var label = new Forms.Label
        {
            Text = "Install location",
            AutoSize = true,
            Font = new System.Drawing.Font(Font.FontFamily, 9, System.Drawing.FontStyle.Bold),
            Margin = new Forms.Padding(0, 0, 0, 6)
        };
        panel.Controls.Add(label, 0, 0);
        panel.SetColumnSpan(label, 2);

        _installDirTextBox.Dock = Forms.DockStyle.Fill;
        _installDirTextBox.Margin = new Forms.Padding(0, 0, 8, 0);
        panel.Controls.Add(_installDirTextBox, 0, 1);

        var browse = new Forms.Button
        {
            Text = "Browse...",
            Width = 110,
            Height = 30,
            Margin = new Forms.Padding(0)
        };
        browse.Click += (_, _) => BrowseInstallFolder();
        panel.Controls.Add(browse, 1, 1);

        return panel;
    }

    private Forms.Control BuildActions()
    {
        var panel = new Forms.FlowLayoutPanel
        {
            FlowDirection = Forms.FlowDirection.RightToLeft,
            Dock = Forms.DockStyle.Top,
            AutoSize = true,
            Margin = new Forms.Padding(0, 16, 0, 0)
        };

        var install = new Forms.Button
        {
            Text = "Install",
            Width = 104,
            Height = 32
        };
        install.Click += (_, _) =>
        {
            if (ValidateInstallDir())
            {
                DialogResult = Forms.DialogResult.OK;
                Close();
            }
        };

        var cancel = new Forms.Button
        {
            Text = "Cancel",
            Width = 104,
            Height = 32
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = Forms.DialogResult.Cancel;
            Close();
        };

        AcceptButton = install;
        CancelButton = cancel;
        panel.Controls.Add(install);
        panel.Controls.Add(cancel);
        return panel;
    }

    private void BrowseInstallFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose where ScreenShotTool will be installed",
            SelectedPath = _installDirTextBox.Text,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == Forms.DialogResult.OK)
        {
            _installDirTextBox.Text = dialog.SelectedPath;
        }
    }

    private bool ValidateInstallDir()
    {
        if (string.IsNullOrWhiteSpace(_installDirTextBox.Text))
        {
            Forms.MessageBox.Show(this, "Choose an install location.", "Install location", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            _installDirTextBox.Text = Path.GetFullPath(_installDirTextBox.Text);
            return true;
        }
        catch (Exception ex)
        {
            Forms.MessageBox.Show(this, ex.Message, "Install location", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            return false;
        }
    }
}
