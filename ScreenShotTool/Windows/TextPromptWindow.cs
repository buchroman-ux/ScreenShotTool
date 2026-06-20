using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenShotTool.Windows;

public sealed class TextPromptWindow : Window
{
    private readonly TextBox _textBox = new();

    public string EnteredText => _textBox.Text;

    public TextPromptWindow(string title, string initialText = "")
    {
        Title = title;
        Width = 360;
        Height = 160;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock { Text = "Text", Margin = new Thickness(0, 0, 0, 8) });
        _textBox.Text = initialText;
        _textBox.MinHeight = 34;
        _textBox.VerticalContentAlignment = VerticalAlignment.Center;
        Grid.SetRow(_textBox, 1);
        root.Children.Add(_textBox);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
        ok.Click += (_, _) => DialogResult = true;
        actions.Children.Add(cancel);
        actions.Children.Add(ok);
        Grid.SetRow(actions, 2);
        root.Children.Add(actions);

        Content = root;
        Loaded += (_, _) =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
    }
}
