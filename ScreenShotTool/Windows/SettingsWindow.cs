using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenShotTool.Models;
using ScreenShotTool.Services;
using Forms = System.Windows.Forms;

namespace ScreenShotTool.Windows;

public sealed class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly ThemeService _themeService;
    private readonly Action _applyHotkeySettings;
    private readonly TextBox _folderTextBox = new();
    private readonly CheckBox _startupCheckBox = new();
    private readonly CheckBox _hotkeyControlCheckBox = new();
    private readonly CheckBox _hotkeyShiftCheckBox = new();
    private readonly CheckBox _hotkeyAltCheckBox = new();
    private readonly CheckBox _hotkeyWindowsCheckBox = new();
    private readonly ComboBox _hotkeyKeyComboBox = new();
    private readonly ComboBox _themeComboBox = new();

    public SettingsWindow(
        SettingsService settingsService,
        StartupService startupService,
        ThemeService themeService,
        Action applyHotkeySettings)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _themeService = themeService;
        _applyHotkeySettings = applyHotkeySettings;

        Title = "Settings";
        Width = 720;
        Height = 620;
        MinWidth = 620;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");

        Content = BuildContent();
        LoadValues();
        Loaded += (_, _) => RefreshStartupState();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddHeader(root);
        AddFolderSection(root);
        AddOptionsSection(root);
        AddHotkeySection(root);
        AddActions(root);
        return root;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private void AddHeader(Grid root)
    {
        var title = new TextBlock
        {
            Text = "ScreenShotTool",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        var subtitle = new TextBlock
        {
            Text = "Screenshot, startup, and theme preferences",
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 18)
        };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "SubtleTextBrush");

        var header = new StackPanel();
        header.Children.Add(title);
        header.Children.Add(subtitle);
        Grid.SetRow(header, 0);
        root.Children.Add(header);
    }

    private void AddFolderSection(Grid root)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Add(Label("Screenshot folder"));

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _folderTextBox.MinHeight = 34;
        _folderTextBox.VerticalContentAlignment = VerticalAlignment.Center;
        _folderTextBox.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_folderTextBox, 0);
        row.Children.Add(_folderTextBox);

        var browse = Button("Browse");
        browse.Margin = new Thickness(0, 0, 8, 0);
        browse.Click += (_, _) => BrowseFolder();
        Grid.SetColumn(browse, 1);
        row.Children.Add(browse);

        var reset = Button("Reset to Default");
        reset.Click += (_, _) => _folderTextBox.Text = _settingsService.DefaultScreenshotFolder;
        Grid.SetColumn(reset, 2);
        row.Children.Add(reset);

        panel.Children.Add(row);
        var card = Card(panel);
        Grid.SetRow(card, 1);
        root.Children.Add(card);
    }

    private void AddOptionsSection(Grid root)
    {
        var panel = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var startupPanel = new StackPanel();
        startupPanel.Children.Add(Label("Launch"));
        _startupCheckBox.Content = "Start with Windows";
        _startupCheckBox.VerticalContentAlignment = VerticalAlignment.Center;
        startupPanel.Children.Add(_startupCheckBox);
        Grid.SetColumn(startupPanel, 0);
        panel.Children.Add(startupPanel);

        var themePanel = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        themePanel.Children.Add(Label("Theme"));
        _themeComboBox.MinHeight = 34;
        _themeComboBox.Width = 220;
        _themeComboBox.HorizontalAlignment = HorizontalAlignment.Left;
        _themeComboBox.Items.Add("Use Windows default");
        _themeComboBox.Items.Add("Light");
        _themeComboBox.Items.Add("Dark");
        themePanel.Children.Add(_themeComboBox);
        Grid.SetColumn(themePanel, 1);
        panel.Children.Add(themePanel);

        var card = Card(panel);
        Grid.SetRow(card, 2);
        root.Children.Add(card);
    }

    private void AddHotkeySection(Grid root)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Add(Label("Screenshot shortcut"));

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        ConfigureHotkeyModifier(_hotkeyControlCheckBox, "Ctrl");
        ConfigureHotkeyModifier(_hotkeyShiftCheckBox, "Shift");
        ConfigureHotkeyModifier(_hotkeyAltCheckBox, "Alt");
        ConfigureHotkeyModifier(_hotkeyWindowsCheckBox, "Win");
        row.Children.Add(_hotkeyControlCheckBox);
        row.Children.Add(_hotkeyShiftCheckBox);
        row.Children.Add(_hotkeyAltCheckBox);
        row.Children.Add(_hotkeyWindowsCheckBox);

        _hotkeyKeyComboBox.MinHeight = 34;
        _hotkeyKeyComboBox.Width = 120;
        _hotkeyKeyComboBox.HorizontalAlignment = HorizontalAlignment.Left;
        foreach (var key in HotkeyKeys())
        {
            _hotkeyKeyComboBox.Items.Add(key);
        }
        row.Children.Add(_hotkeyKeyComboBox);
        panel.Children.Add(row);

        var note = new TextBlock
        {
            Text = "Default: Ctrl + Shift + S",
            FontSize = 13,
            Margin = new Thickness(0, 8, 0, 0)
        };
        note.SetResourceReference(TextBlock.ForegroundProperty, "SubtleTextBrush");
        panel.Children.Add(note);

        var card = Card(panel);
        Grid.SetRow(card, 3);
        root.Children.Add(card);
    }

    private static void ConfigureHotkeyModifier(CheckBox checkBox, string text)
    {
        checkBox.Content = text;
        checkBox.Margin = new Thickness(0, 0, 16, 0);
        checkBox.VerticalContentAlignment = VerticalAlignment.Center;
    }

    private static IEnumerable<string> HotkeyKeys()
    {
        for (var c = 'A'; c <= 'Z'; c++)
        {
            yield return c.ToString();
        }

        for (var i = 0; i <= 9; i++)
        {
            yield return i.ToString();
        }

        for (var i = 1; i <= 12; i++)
        {
            yield return $"F{i}";
        }
    }

    private void AddActions(Grid root)
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancel = Button("Cancel");
        cancel.Margin = new Thickness(0, 0, 8, 0);
        cancel.Click += (_, _) => Close();
        actions.Children.Add(cancel);

        var save = Button("Save");
        save.Click += (_, _) => Save();
        actions.Children.Add(save);

        Grid.SetRow(actions, 5);
        root.Children.Add(actions);
    }

    private static Button Button(string text) => new()
    {
        Content = text,
        MinWidth = 96,
        MinHeight = 34,
        Padding = new Thickness(12, 4, 12, 4)
    };

    private static Border Card(UIElement child)
    {
        var border = new Border
        {
            Child = child,
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1)
        };
        border.SetResourceReference(Border.BackgroundProperty, "PanelBackgroundBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return border;
    }

    private void LoadValues()
    {
        _folderTextBox.Text = _settingsService.Settings.ScreenshotFolderPath ?? _settingsService.DefaultScreenshotFolder;
        _startupCheckBox.IsChecked = _settingsService.Settings.StartWithWindows;
        var hotkey = _settingsService.Settings.Hotkey ?? ScreenshotHotkey.Default;
        _hotkeyControlCheckBox.IsChecked = hotkey.Control;
        _hotkeyShiftCheckBox.IsChecked = hotkey.Shift;
        _hotkeyAltCheckBox.IsChecked = hotkey.Alt;
        _hotkeyWindowsCheckBox.IsChecked = hotkey.Windows;
        _hotkeyKeyComboBox.SelectedItem = HotkeyKeys().Contains(hotkey.Key, StringComparer.OrdinalIgnoreCase)
            ? HotkeyKeys().First(key => key.Equals(hotkey.Key, StringComparison.OrdinalIgnoreCase))
            : ScreenshotHotkey.Default.Key;
        _themeComboBox.SelectedIndex = _settingsService.Settings.ThemeMode switch
        {
            ThemeMode.Light => 1,
            ThemeMode.Dark => 2,
            _ => 0
        };
    }

    private void RefreshStartupState()
    {
        try
        {
            _startupCheckBox.IsChecked = _startupService.IsEnabled();
        }
        catch
        {
            _startupCheckBox.IsChecked = _settingsService.Settings.StartWithWindows;
        }
    }

    private void BrowseFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose where screenshots are saved",
            SelectedPath = _folderTextBox.Text,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _folderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Save()
    {
        if (!_settingsService.TryValidateFolder(_folderTextBox.Text, out var error))
        {
            MessageBox.Show(this, error, "Invalid folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryBuildHotkey(out var hotkey, out var hotkeyError))
        {
            MessageBox.Show(this, hotkeyError, "Invalid shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var startWithWindows = _startupCheckBox.IsChecked == true;
            var previousHotkey = (_settingsService.Settings.Hotkey ?? ScreenshotHotkey.Default).Clone();
            _startupService.SetEnabled(startWithWindows);
            _settingsService.Settings.ScreenshotFolderPath = _folderTextBox.Text;
            _settingsService.Settings.StartWithWindows = startWithWindows;
            _settingsService.Settings.Hotkey = hotkey;
            _settingsService.Settings.ThemeMode = _themeComboBox.SelectedIndex switch
            {
                1 => ThemeMode.Light,
                2 => ThemeMode.Dark,
                _ => ThemeMode.WindowsDefault
            };
            try
            {
                _applyHotkeySettings();
            }
            catch
            {
                _settingsService.Settings.Hotkey = previousHotkey;
                _applyHotkeySettings();
                throw;
            }

            _settingsService.Save();
            _themeService.Apply(_settingsService.Settings.ThemeMode);

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Settings could not be saved", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool TryBuildHotkey(out ScreenshotHotkey hotkey, out string error)
    {
        hotkey = new ScreenshotHotkey
        {
            Control = _hotkeyControlCheckBox.IsChecked == true,
            Shift = _hotkeyShiftCheckBox.IsChecked == true,
            Alt = _hotkeyAltCheckBox.IsChecked == true,
            Windows = _hotkeyWindowsCheckBox.IsChecked == true,
            Key = _hotkeyKeyComboBox.SelectedItem as string ?? string.Empty
        };
        error = string.Empty;

        if (!hotkey.Control && !hotkey.Shift && !hotkey.Alt && !hotkey.Windows)
        {
            error = "Choose at least one modifier key.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(hotkey.Key))
        {
            error = "Choose the main key for the shortcut.";
            return false;
        }

        return true;
    }
}
