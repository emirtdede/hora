using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hora.Core.Localization;

namespace Hora.UI.Dialogs;

/// <summary>Shared shell for the small modal dialogs: card layout, window chrome, and buttons.</summary>
public abstract class DialogBase : Window
{
    protected DialogBase(string title, double width = 470)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        Background = (System.Windows.Media.Brush)Application.Current.FindResource("BrushBackground");
        ShowInTaskbar = false;
        FontFamily = (System.Windows.Media.FontFamily)Application.Current.FindResource("UiFont");
        // A long translated body must not push the action buttons off screen.
        MaxHeight = Math.Max(320, Math.Min(640, SystemParameters.WorkArea.Height - 48));
        SetValue(System.Windows.Shell.WindowChrome.WindowChromeProperty, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            try
            {
                DialogResult = EscapeResult;
                Close();
            }
            catch (InvalidOperationException)
            {
                // The dialog is not modal in this context; closing is enough.
                Close();
            }
        };
    }

    /// <summary>Result assigned when the user dismisses the dialog with Escape.</summary>
    protected virtual bool? EscapeResult => false;

    protected void BindTitle()
    {
        Title = Translator.Instance.Get(Title);
        SetValue(System.Windows.Automation.AutomationProperties.NameProperty, Title);
    }

    /// <summary>Hosts the body in a scroll viewer so tall content stays reachable.</summary>
    protected void SetDialogContent(FrameworkElement content) => Content = new ScrollViewer
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Focusable = false,
        Content = content,
    };
}

/// <summary>Two-choice confirmation dialog with an optional remember switch.</summary>
public sealed class PromptWindow : DialogBase
{
    public PromptWindow(string title, string body, string primaryKey, string secondaryKey)
        : base(title)
    {
        BindTitle();

        var root = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.FindResource("BrushSurface"),
            BorderBrush = (System.Windows.Media.Brush)Application.Current.FindResource("BrushBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24),
        };

        var stack = new System.Windows.Controls.StackPanel();
        var heading = new System.Windows.Controls.TextBlock
        {
            Text = Translator.Instance.Get(title),
            Style = (System.Windows.Style)Application.Current.FindResource("TitleText"),
        };
        stack.Children.Add(heading);

        var text = new System.Windows.Controls.TextBlock
        {
            Text = body,
            Margin = new Thickness(0, 10, 0, 20),
            Style = (System.Windows.Style)Application.Current.FindResource("MutedText"),
        };
        text.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 13d);
        stack.Children.Add(text);

        var buttons = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        };

        var cancel = new System.Windows.Controls.Button
        {
            Content = Translator.Instance.Get(secondaryKey),
            Style = (System.Windows.Style)Application.Current.FindResource("SecondaryButton"),
            MinWidth = 92,
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        buttons.Children.Add(cancel);

        var accept = new System.Windows.Controls.Button
        {
            Content = Translator.Instance.Get(primaryKey),
            Style = (System.Windows.Style)Application.Current.FindResource("PrimaryButton"),
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 92,
            IsDefault = true,
        };
        accept.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        buttons.Children.Add(accept);

        stack.Children.Add(buttons);
        root.Child = stack;
        SetDialogContent(root);
    }
}

/// <summary>Close-behavior dialog: keep running in the tray, exit, and optionally remember.</summary>
public sealed class CloseDialog : DialogBase
{
    private readonly System.Windows.Controls.CheckBox _remember;

    protected override bool? EscapeResult => null;

    public CloseDialog(string title, string body, string trayKey, string exitKey, string rememberKey)
        : base(title)
    {
        BindTitle();

        var root = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.FindResource("BrushSurface"),
            BorderBrush = (System.Windows.Media.Brush)Application.Current.FindResource("BrushBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24),
        };

        var stack = new System.Windows.Controls.StackPanel();
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = Translator.Instance.Get(title),
            Style = (System.Windows.Style)Application.Current.FindResource("TitleText"),
        });
        var text = new System.Windows.Controls.TextBlock
        {
            Text = body,
            Margin = new Thickness(0, 10, 0, 16),
            Style = (System.Windows.Style)Application.Current.FindResource("MutedText"),
        };
        text.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 13d);
        stack.Children.Add(text);

        _remember = new System.Windows.Controls.CheckBox
        {
            Content = Translator.Instance.Get(rememberKey),
            Style = (System.Windows.Style)Application.Current.FindResource("Switch"),
            Margin = new Thickness(0, 0, 0, 18),
        };
        stack.Children.Add(_remember);

        var buttons = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        };

        var cancel = new System.Windows.Controls.Button
        {
            Content = Translator.Instance.Get("commonCancel"),
            Style = (System.Windows.Style)Application.Current.FindResource("LinkButton"),
            Margin = new Thickness(0, 0, 12, 0),
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = null;
            Close();
        };
        buttons.Children.Add(cancel);

        var exit = new System.Windows.Controls.Button
        {
            Content = Translator.Instance.Get(exitKey),
            Style = (System.Windows.Style)Application.Current.FindResource("SecondaryButton"),
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 0),
        };
        exit.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        buttons.Children.Add(exit);

        var tray = new System.Windows.Controls.Button
        {
            Content = Translator.Instance.Get(trayKey),
            Style = (System.Windows.Style)Application.Current.FindResource("PrimaryButton"),
            MinWidth = 96,
            IsDefault = true,
        };
        tray.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        buttons.Children.Add(tray);

        stack.Children.Add(buttons);
        root.Child = stack;
        SetDialogContent(root);
    }

    public bool Remember => _remember.IsChecked == true;
}
