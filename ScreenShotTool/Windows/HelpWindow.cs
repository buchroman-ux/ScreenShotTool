using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenShotTool.Windows;

public sealed class HelpWindow : Window
{
    public HelpWindow()
    {
        Title = "Help / Shortcuts";
        Width = 760;
        Height = 620;
        MinWidth = 640;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildContent()
        };
    }

    private static UIElement BuildContent()
    {
        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(Heading("ScreenShotTool", 27, new Thickness(0, 0, 0, 5)));
        panel.Children.Add(Subtle("Shortcut-first reference for capture, annotation, and save."));

        panel.Children.Add(Card("Shortcuts",
            Shortcut("Ctrl", "Shift", "S", "Default capture shortcut; can be changed in Settings"),
            Shortcut("Esc", null, null, "Switch to select mode and clear active editing state"),
            Shortcut("Esc", "Esc", null, "Ask before closing the editor when pressed twice quickly"),
            Shortcut("Ctrl", "Z", null, "Undo last editor action"),
            Shortcut("Ctrl", "Y", null, "Redo last undone action"),
            Shortcut("Ctrl", "C", null, "Copy selected text, or selected annotation objects when no text is selected"),
            Shortcut("Ctrl", "V", null, "Paste clipboard text into active text, or paste copied annotation objects"),
            Shortcut("Ctrl", "A", null, "Select all text inside the active text shape"),
            Shortcut("Ctrl", "Mouse wheel", null, "Zoom the editor canvas in or out"),
            Shortcut("Delete", null, null, "Delete selected annotation object"),
            Shortcut("Backspace", null, null, "Delete text inside the selected shape")));

        panel.Children.Add(Card("Workflow",
            TextLine("Capture freezes the virtual desktop first, so temporary popups and messages stay visible while you drag the selection."),
            TextLine("The default capture shortcut is Ctrl + Shift + S. You can change it from Settings."),
            TextLine("Annotate on the editor canvas, then press Ctrl + Shift + S to save the PNG and copy it to the clipboard."),
            TextLine("After drawing a rectangle, ellipse, or text box, the editor returns to select mode. After an arrow, rectangle stays active for quick callouts."),
            TextLine("Drag left to right to select objects fully inside the box; drag right to left to select touched objects.")));

        panel.Children.Add(Card("Tools",
            TextLine("Use Arrow, Pencil, Rectangle, Ellipse, and Text from the main tool group."),
            TextLine("Pencil draws freehand strokes with the active color and thickness.")));

        panel.Children.Add(Card("Text",
            TextLine("Select a rectangle, ellipse, or dashed text box and type directly into it."),
            TextLine("Drag across text to select characters. Color and text-size changes apply to the selected text, or to the next typed text when nothing is selected."),
            TextLine("When characters are selected, Ctrl + C and Ctrl + V work on the text only; select the whole annotation object to copy the shape with its text."),
            TextLine("Dashed text boxes grow as needed; shape text stays clipped inside the shape.")));

        panel.Children.Add(Card("Settings",
            TextLine("Choose the screenshot folder, startup behavior, and theme from Settings.")));
        return panel;
    }

    private static TextBlock Heading(string text, double size, Thickness margin)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            Margin = margin
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return block;
    }

    private static TextBlock Subtle(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 15,
            Margin = new Thickness(0, 0, 0, 22)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "SubtleTextBrush");
        return block;
    }

    private static Border Card(string title, params UIElement[] children)
    {
        var panel = new StackPanel();
        panel.Children.Add(Heading(title, 18, new Thickness(0, 0, 0, 12)));
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        var border = new Border
        {
            Child = panel,
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1)
        };
        border.SetResourceReference(Border.BackgroundProperty, "PanelBackgroundBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return border;
    }

    private static TextBlock TextLine(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15.5,
            Margin = new Thickness(0, 0, 0, 10)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return block;
    }

    private static UIElement Shortcut(string first, string? second, string? third, string description)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 11) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(245) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var keys = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        keys.Children.Add(KeyBadge(first));
        if (second is not null)
        {
            keys.Children.Add(Plus());
            keys.Children.Add(KeyBadge(second));
        }
        if (third is not null)
        {
            keys.Children.Add(Plus());
            keys.Children.Add(KeyBadge(third));
        }
        Grid.SetColumn(keys, 0);
        row.Children.Add(keys);

        var text = TextLine(description);
        text.Margin = new Thickness(0);
        text.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private static TextBlock Plus()
    {
        var plus = new TextBlock
        {
            Text = "+",
            Margin = new Thickness(6, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15
        };
        plus.SetResourceReference(TextBlock.ForegroundProperty, "SubtleTextBrush");
        return plus;
    }

    private static Border KeyBadge(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            MinWidth = 42,
            TextAlignment = TextAlignment.Center
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        var badge = new Border
        {
            Child = label,
            Padding = new Thickness(10, 5, 10, 5),
            MinHeight = 32,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1)
        };
        badge.SetResourceReference(Border.BackgroundProperty, "ButtonBackgroundBrush");
        badge.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return badge;
    }
}
