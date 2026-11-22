using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hora.Core.Presentation;

namespace Hora.UI.Views;

/// <summary>In-app documentation: quick start, tracking rules, shortcuts, backup, privacy.</summary>
public sealed class DocsView : PageBase
{
    private readonly Action<string> _navigate;

    public DocsView(AppContext context, Action<string> navigate)
        : base(context)
    {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));

        var root = new StackPanel();
        root.Children.Add(Header("docsTitle", "docsSubtitle"));

        var cards = Columns(
            (1, BuildTopic("docsGettingStarted", "docsGettingStartedBody", "IconStar")),
            (1, BuildTopic("docsTracking", "docsTrackingBody", "IconClock")));
        root.Children.Add(cards);

        var second = Columns(
            (1, BuildTopic("docsBackup", "docsBackupBody", "IconBackup")),
            (1, BuildTopic("docsPrivacy", "docsPrivacyBody", "IconShield")));
        second.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(second);

        var support = Ui.Card(BuildSupport());
        support.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(support);

        var shortcuts = Ui.Card(BuildShortcuts());
        shortcuts.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(shortcuts);

        Content = root;
    }

    public override void RefreshData()
    {
        // Documentation is static; localization is handled by element bindings.
    }

    private FrameworkElement BuildTopic(string titleKey, string bodyKey, string iconKey)
    {
        var stack = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var icon = new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource("Icon"),
            Data = (Geometry)FindResource(iconKey),
            Width = 20,
            Height = 20,
            Margin = new Thickness(0, 0, 10, 0),
        };
        icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "BrushAccent");
        head.Children.Add(icon);
        var title = Ui.Text(titleKey, "HeadingText");
        title.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(title);
        stack.Children.Add(head);
        stack.Children.Add(Ui.Text(bodyKey, "MutedText"));
        return Ui.Card(stack);
    }

    private FrameworkElement BuildSupport()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("docsSupport", "HeadingText"));
        var body = Ui.Text("docsSupportBody", "MutedText");
        body.Margin = new Thickness(0, 4, 0, 12);
        stack.Children.Add(body);

        var actions = new WrapPanel();
        actions.Children.Add(Ui.Secondary("docsOpenDiagnostics", () => _navigate("diagnostics")));
        var github = Ui.Secondary("aboutGitHub", () => Open(AboutLinks.GitHub + "/issues"));
        github.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(github);
        var about = Ui.Secondary("navAbout", () => _navigate("about"));
        about.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(about);
        stack.Children.Add(actions);
        return stack;
    }

    private FrameworkElement BuildShortcuts()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("docsShortcuts", "HeadingText"));
        var table = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        table.Children.Add(ShortcutRow("docsShortcutToggle", Context.Settings.Hotkey));
        table.Children.Add(ShortcutRow("docsShortcutClose", "Ctrl+W"));
        table.Children.Add(ShortcutRow("docsShortcutExit", "Alt+F4"));
        table.Children.Add(ShortcutRow("docsShortcutRefresh", "F5"));
        stack.Children.Add(table);
        return stack;
    }

    private FrameworkElement ShortcutRow(string labelKey, string gesture)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Ui.Text(labelKey, "MutedText");
        label.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(label);

        var key = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3, 8, 3),
            Child = Ui.Literal(gesture, "MicroText"),
        };
        key.SetResourceReference(Border.BackgroundProperty, "BrushSurfaceAlt");
        key.SetResourceReference(Border.BorderBrushProperty, "BrushBorder");
        key.BorderThickness = new Thickness(1);
        Grid.SetColumn(key, 1);
        grid.Children.Add(key);
        return grid;
    }

    private void Open(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                              or System.ComponentModel.Win32Exception)
        {
            Toast(Context.T("openFolderFailed"), ToastKind.Error);
        }
    }
}
