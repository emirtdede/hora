using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hora.Core.Localization;

namespace Hora.UI.Views;

/// <summary>Product identity, version, credits, and the external project links.</summary>
public sealed class AboutView : PageBase
{
    private readonly Action<string, ToastKind> _toast;

    public AboutView(AppContext context, Action<string, ToastKind> toast)
        : base(context)
    {
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        var root = new StackPanel();
        root.Children.Add(Header("aboutTitle", "aboutSubtitle"));

        var identity = Ui.Card(BuildIdentity());
        root.Children.Add(identity);

        var details = Ui.Card(BuildDetails());
        details.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(details);

        var links = Ui.Card(BuildLinks());
        links.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(links);

        var credits = Ui.Card(BuildCredits());
        credits.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(credits);

        Content = root;
    }

    public override void RefreshData()
    {
        // Identity content is static and localization is bound on each element.
    }

    private FrameworkElement BuildIdentity()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var mark = new Border
        {
            Width = 64,
            Height = 64,
            CornerRadius = new CornerRadius(16),
            Margin = new Thickness(0, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        mark.SetResourceReference(Border.BackgroundProperty, "BrushAccent");
        var glyph = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 32 32 A 12 12 0 1 1 20 20 M 32 32 L 32 22 M 32 32 L 40 37"),
            Stroke = Brushes.White,
            StrokeThickness = 2.6,
            Stretch = Stretch.Uniform,
            Width = 32,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        mark.Child = glyph;
        grid.Children.Add(mark);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Ui.Text("appName", "TitleText"));
        var tagline = Ui.Text("appTagline", "MutedText");
        tagline.Margin = new Thickness(0, 4, 0, 0);
        text.Children.Add(tagline);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private FrameworkElement BuildDetails()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("aboutBuiltWith", "HeadingText"));
        var table = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        table.Children.Add(DetailRow("aboutVersion",
            typeof(AboutView).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
        table.Children.Add(DetailRow("aboutPublisher", "Vellium"));
        table.Children.Add(DetailRow("aboutDeveloper", "Emir Tede"));
        table.Children.Add(DetailRow("aboutLicense", Context.T("aboutLicenseValue")));
        table.Children.Add(DetailRow("aboutArchitecture", ".NET 8 / WPF"));
        table.Children.Add(DetailRow("aboutLanguages",
            Context.T("aboutLanguagesValue", LanguageCatalog.All.Count.ToString(CultureInfo.CurrentCulture))));
        table.Children.Add(DetailRow("aboutData", Context.T("aboutDataValue")));
        stack.Children.Add(table);
        return stack;
    }

    private FrameworkElement BuildLinks()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("aboutWebsite", "HeadingText"));
        var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(Ui.Secondary("aboutWebsite", () => Open(AboutLinks.Website)));
        var github = Ui.Secondary("aboutGitHub", () => Open(AboutLinks.GitHub));
        github.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(github);
        var sponsor = Ui.Secondary("aboutSponsor", () => Open(AboutLinks.Sponsor));
        sponsor.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(sponsor);
        stack.Children.Add(actions);
        return stack;
    }

    private FrameworkElement BuildCredits()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("aboutCredits", "HeadingText"));
        var body = Ui.Text("aboutCreditsBody", "MutedText");
        body.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(body);
        var license = Ui.Text("legalLicense", "MicroText");
        license.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(license);
        return stack;
    }

    private static FrameworkElement DetailRow(string labelKey, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var label = Ui.Text(labelKey, "MutedText");
        label.Margin = new Thickness(0, 0, 20, 0);
        grid.Children.Add(label);
        var text = Ui.Literal(value, "BodyStrong");
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
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
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _toast(Context.T("unknownError"), ToastKind.Error);
        }
    }
}
