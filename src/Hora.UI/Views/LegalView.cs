using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Hora.UI.Views;

/// <summary>Privacy, terms, and license text bundled with the application.</summary>
public sealed class LegalView : PageBase
{
    /// <summary>Date the bundled privacy, terms, and license text last changed.</summary>
    private static readonly DateOnly LastRevised = new(2026, 11, 22);

    private readonly TextBlock _updated;

    public LegalView(AppContext context)
        : base(context)
    {
        var root = new StackPanel();
        root.Children.Add(Header("legalTitle", "legalSubtitle"));

        // The catalog string carries a {0} placeholder; without an argument the
        // page printed the raw placeholder instead of a date.
        _updated = Ui.Literal(RevisedText(), "MicroText");
        _updated.Margin = new Thickness(0, -12, 0, 14);
        root.Children.Add(_updated);

        root.Children.Add(Section("legalPrivacy", "legalPrivacyBody", "IconShield"));
        root.Children.Add(Section("legalTerms", "legalTermsBody", "IconLegal"));
        root.Children.Add(Section("legalLicense", "legalLicenseBody", "IconCode"));

        Content = root;
    }

    public override void RefreshData() => _updated.Text = RevisedText();

    private string RevisedText() =>
        Context.T("legalUpdated",
            LastRevised.ToString("d", System.Globalization.CultureInfo.CurrentCulture));

    private FrameworkElement Section(string titleKey, string bodyKey, string iconKey)
    {
        var stack = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var icon = new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource("Icon"),
            Data = (Geometry)FindResource(iconKey),
            Margin = new Thickness(0, 0, 10, 0),
        };
        icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "BrushAccent");
        head.Children.Add(icon);
        var title = Ui.Text(titleKey, "HeadingText");
        title.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(title);
        stack.Children.Add(head);
        stack.Children.Add(Ui.Text(bodyKey, "MutedText"));
        var card = Ui.Card(stack);
        card.Margin = new Thickness(0, 0, 0, 14);
        return card;
    }
}
