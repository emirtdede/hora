using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Hora.UI.Platform;
using Path = System.Windows.Shapes.Path;

namespace Hora.UI.Views;

/// <summary>Small fluent factory used by every view so spacing and styles stay uniform.</summary>
public static class Ui
{
    public static T Styled<T>(this T element, string styleKey)
        where T : FrameworkElement
    {
        element.Style = (Style)Application.Current.FindResource(styleKey);
        return element;
    }

    public static TextBlock Text(string key, string styleKey = "MutedText")
    {
        var text = new TextBlock();
        text.Style = (Style)Application.Current.FindResource(styleKey);
        Bind(text, key);
        return text;
    }

    public static TextBlock Literal(string value, string styleKey = "MutedText")
    {
        var text = new TextBlock();
        text.Style = (Style)Application.Current.FindResource(styleKey);
        text.Text = value;
        return text;
    }

    public static TextBlock Bind(TextBlock text, string key)
    {
        text.SetBinding(TextBlock.TextProperty, new Binding($"[{key}]")
        {
            Source = LocalizationProxy.Instance,
            Mode = BindingMode.OneWay,
        });
        return text;
    }

    public static Path Icon(string geometryKey, double size = 18, string styleKey = "Icon")
    {
        var icon = new Path
        {
            Style = (Style)Application.Current.FindResource(styleKey),
            Data = (Geometry)Application.Current.FindResource(geometryKey),
            Width = size,
            Height = size,
        };
        return icon;
    }

    public static StackPanel Stack(bool horizontal = false, double gap = 0)
    {
        var panel = new StackPanel { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
        if (gap > 0)
        {
            panel.Margin = new Thickness(0);
            panel.Tag = gap;
            panel.Loaded += (_, _) => ApplyGap(panel, horizontal, gap);
        }

        return panel;
    }

    private static void ApplyGap(StackPanel panel, bool horizontal, double gap)
    {
        if (horizontal)
        {
            panel.Margin = new Thickness(0);
            for (var index = 1; index < panel.Children.Count; index += 2)
            {
                if (panel.Children[index] is FrameworkElement child)
                {
                    child.Margin = new Thickness(gap, 0, 0, 0);
                }
            }

            return;
        }

        for (var index = 1; index < panel.Children.Count; index += 2)
        {
            if (panel.Children[index] is FrameworkElement child)
            {
                child.Margin = new Thickness(0, gap, 0, 0);
            }
        }
    }

    public static StackPanel Add(this StackPanel panel, params UIElement[] children)
    {
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    public static Border Card(UIElement child, double padding = 20)
    {
        var border = new Border { Style = (Style)Application.Current.FindResource("Card"), Padding = new Thickness(padding) };
        border.Child = child;
        return border;
    }

    public static Border Subtle(UIElement child, double padding = 14)
    {
        var border = new Border
        {
            Style = (Style)Application.Current.FindResource("SubtleCard"),
            Padding = new Thickness(padding),
        };
        border.Child = child;
        return border;
    }

    public static Border Divider()
    {
        var border = new Border { Height = 1, Margin = new Thickness(0, 14, 0, 14) };
        border.SetResourceReference(Border.BackgroundProperty, "BrushBorder");
        return border;
    }

    public static Button Primary(string key, Action click)
    {
        var button = new Button { Style = (Style)Application.Current.FindResource("PrimaryButton") };
        BindContent(button, key);
        button.Click += (_, _) => click();
        return button;
    }

    public static Button Secondary(string key, Action click)
    {
        var button = new Button { Style = (Style)Application.Current.FindResource("SecondaryButton") };
        BindContent(button, key);
        button.Click += (_, _) => click();
        return button;
    }

    public static Button Link(string key, Action click)
    {
        var button = new Button { Style = (Style)Application.Current.FindResource("LinkButton") };
        BindContent(button, key);
        button.Click += (_, _) => click();
        return button;
    }

    public static T BindContent<T>(T control, string key)
        where T : ContentControl
    {
        control.SetBinding(ContentControl.ContentProperty, new Binding($"[{key}]")
        {
            Source = LocalizationProxy.Instance,
            Mode = BindingMode.OneWay,
        });
        return control;
    }

    /// <summary>Rebinds an existing button label to a different catalog key.</summary>
    public static void SetContentKey(ContentControl control, string key)
    {
        control.SetBinding(ContentControl.ContentProperty, new Binding($"[{key}]")
        {
            Source = LocalizationProxy.Instance,
            Mode = BindingMode.OneWay,
        });
    }

    public static Button IconAction(string geometryKey, string tooltipKey, Action click)
    {
        var button = new Button { Style = (Style)Application.Current.FindResource("IconButton") };
        button.Content = Icon(geometryKey);
        button.ToolTip = Text(tooltipKey, "MutedText");
        button.Click += (_, _) => click();
        return button;
    }

    public static Border Pill(string key, string brushKey)
    {
        var text = Text(key, "MicroText");
        text.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3, 8, 3),
            Child = text,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        border.SetResourceReference(Border.BackgroundProperty, "BrushAccentFaint");
        return border;
    }

    /// <summary>Two-column track used for percentage bars.</summary>
    public static Grid Bar(double percent, string brushKey = "BrushChart1")
    {
        var grid = new Grid { Height = 6, Margin = new Thickness(0, 6, 0, 0) };
        var filled = Math.Clamp(percent, 0, 100);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(filled, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, 100 - filled), GridUnitType.Star) });
        var track = new Border { CornerRadius = new CornerRadius(3) };
        track.SetResourceReference(Border.BackgroundProperty, "BrushSurfaceAlt");
        Grid.SetColumnSpan(track, 2);
        var fill = new Border { CornerRadius = new CornerRadius(3) };
        fill.SetResourceReference(Border.BackgroundProperty, brushKey);
        Grid.SetColumn(fill, 0);
        grid.Children.Add(track);
        grid.Children.Add(fill);
        return grid;
    }

    public static CheckBox Toggle(bool value, Action<bool> changed)
    {
        var toggle = new CheckBox
        {
            Style = (Style)Application.Current.FindResource("Switch"),
            IsChecked = value,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toggle.Checked += (_, _) => changed(true);
        toggle.Unchecked += (_, _) => changed(false);
        return toggle;
    }

    /// <summary>Row with a label and description on the left and a control on the right.</summary>
    public static Grid SettingRow(string titleKey, string? descriptionKey, UIElement control)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labels = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        labels.Children.Add(Text(titleKey, "BodyStrong"));
        if (!string.IsNullOrEmpty(descriptionKey))
        {
            labels.Children.Add(Text(descriptionKey, "MutedText"));
        }

        grid.Children.Add(labels);
        var container = new Grid { VerticalAlignment = VerticalAlignment.Center, MinWidth = 44 };
        container.Children.Add(control);
        Grid.SetColumn(container, 1);
        grid.Children.Add(container);
        return grid;
    }

    public static FrameworkElement SectionHeader(string key)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        stack.Children.Add(Text(key, "HeadingText"));
        return stack;
    }

    public static FrameworkElement Kpi(string labelKey, TextBlock value)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
        stack.Children.Add(Text(labelKey, "LabelText"));
        value.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(value);
        return stack;
    }

    public static TextBlock ValueText(string text, string styleKey = "KpiValue")
    {
        var block = new TextBlock { Text = text, Style = (Style)Application.Current.FindResource(styleKey) };
        return block;
    }
}
