using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Hora.Core.Localization;
using Hora.Core.Theming;
using Hora.UI.Platform;
using Hora.UI.Views;

namespace Hora.UI.Wizard;

/// <summary>
/// First-run setup. Each step is its own screen: language, theme, welcome, and tour.
/// Choices are written immediately so a skipped tour keeps the selected look.
/// </summary>
public sealed class SetupWindow : Window
{
    private const int StepCount = 4;

    private readonly AppContext _context;
    private readonly Grid _host = new();
    private readonly TextBlock _stepText;
    private readonly Button _back;
    private readonly Button _next;
    private readonly Button _skip;
    private int _step;

    public SetupWindow(AppContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        Title = "Hora";
        // 1366x768 at 150% scaling leaves about 900x500 DIU of work area, so the
        // wizard starts inside that and shrinks instead of hiding its footer.
        var work = SystemParameters.WorkArea;
        Width = Math.Max(600, Math.Min(820, work.Width - 80));
        Height = Math.Max(460, Math.Min(600, work.Height - 80));
        MinWidth = 600;
        MinHeight = 460;
        MaxWidth = work.Width;
        MaxHeight = work.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = (Brush)FindResource("BrushBackground");
        FontFamily = (FontFamily)FindResource("UiFont");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        SetValue(System.Windows.Shell.WindowChrome.WindowChromeProperty, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 44,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        _stepText = new TextBlock { Style = (Style)FindResource("MicroText") };
        _back = Ui.Secondary("commonBack", () => GoTo(_step - 1));
        _next = Ui.Primary("commonNext", Advance);
        _skip = Ui.Link("wizardSkip", Complete);

        // Stable automation ids let the screenshot and end-to-end scripts find the
        // footer buttons even after the language switch renames their labels.
        System.Windows.Automation.AutomationProperties.SetAutomationId(_back, "WizardBack");
        System.Windows.Automation.AutomationProperties.SetAutomationId(_next, "WizardNext");
        System.Windows.Automation.AutomationProperties.SetAutomationId(_skip, "WizardSkip");

        Content = BuildShell();
        Loaded += (_, _) => RenderStep();
        PreviewKeyDown += (_, e) =>
        {
            // Esc leaves setup without marking it complete, so the next launch
            // offers the wizard again instead of starting half configured.
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
            }
        };
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState != MouseButtonState.Pressed || e.OriginalSource is Button)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The drag can end before it starts when the mouse is released early.
        }
    }

    private FrameworkElement BuildShell()
    {
        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)FindResource("BrushBorder"),
            Background = (Brush)FindResource("BrushBackground"),
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });

        var header = new Grid { Margin = new Thickness(24, 0, 16, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var mark = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)FindResource("BrushAccent"),
            Margin = new Thickness(0, 0, 10, 0),
        };
        mark.Child = BuildClockGlyph("BrushOnAccent", 15, 1.6);
        brand.Children.Add(mark);
        brand.Children.Add(Ui.Text("appName", "BodyStrong"));
        header.Children.Add(brand);

        _stepText.VerticalAlignment = VerticalAlignment.Center;
        _stepText.HorizontalAlignment = HorizontalAlignment.Right;
        _stepText.Margin = new Thickness(0, 0, 18, 0);
        Grid.SetColumn(_stepText, 1);
        header.Children.Add(_stepText);

        var close = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Content = new System.Windows.Shapes.Path
            {
                Style = (Style)FindResource("Icon"),
                Data = (Geometry)FindResource("IconClose"),
                Width = 14,
                Height = 14,
            },
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = _context.T("commonClose"),
        };
        close.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        Grid.SetColumn(close, 2);
        header.Children.Add(close);
        root.Children.Add(header);

        _host.Margin = new Thickness(52, 10, 52, 10);
        // Small or heavily scaled displays cannot show a full step at 600 DIU,
        // so the body scrolls instead of clipping the footer.
        var body = new ScrollViewer
        {
            Content = _host,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Padding = new Thickness(0),
        };
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var footer = new Grid { Margin = new Thickness(52, 0, 52, 24) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var privacy = Ui.Text("wizardPrivacyNote", "MicroText");
        privacy.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(privacy);

        _skip.Visibility = Visibility.Collapsed;
        Grid.SetColumn(_skip, 1);
        footer.Children.Add(_skip);

        _back.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(_back, 2);
        footer.Children.Add(_back);

        _next.Margin = new Thickness(8, 0, 0, 0);
        _next.MinWidth = 108;
        Grid.SetColumn(_next, 3);
        footer.Children.Add(_next);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        frame.Child = root;
        return frame;
    }

    private static System.Windows.Shapes.Path BuildClockGlyph(string brushKey, double size, double thickness)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 13 13 A 5 5 0 1 1 8 8 M 13 13 L 13 9 M 13 13 L 17 16"),
            StrokeThickness = thickness,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
        return path;
    }

    private void Advance()
    {
        if (_step >= StepCount - 1)
        {
            Complete();
            return;
        }

        GoTo(_step + 1);
    }

    private void GoTo(int step)
    {
        _step = Math.Clamp(step, 0, StepCount - 1);
        RenderStep();
    }

    private void RenderStep()
    {
        _host.Children.Clear();
        _host.Children.Add(_step switch
        {
            0 => BuildLanguageStep(),
            1 => BuildThemeStep(),
            2 => BuildWelcomeStep(),
            _ => BuildTourStep(),
        });

        _stepText.Text = _context.T("wizardStep", _step + 1, StepCount);
        _back.Visibility = _step == 0 ? Visibility.Collapsed : Visibility.Visible;
        _skip.Visibility = _step == StepCount - 1 ? Visibility.Visible : Visibility.Collapsed;
        Ui.SetContentKey(_next, _step == StepCount - 1 ? "wizardStart" : "commonNext");
        _next.IsDefault = true;
    }

    private FrameworkElement BuildLanguageStep()
    {
        var stack = BuildStepText("wizardLanguageTitle", "wizardLanguageBody");
        var combo = new ComboBox
        {
            Style = (Style)FindResource("FlatCombo"),
            Width = 340,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 18, 0, 0),
        };

        var languages = LanguageCatalog.All;
        foreach (var language in languages)
        {
            combo.Items.Add(language.NativeName);
        }

        var selected = 0;
        for (var index = 0; index < languages.Count; index += 1)
        {
            if (string.Equals(languages[index].Id, _context.Settings.Language, StringComparison.OrdinalIgnoreCase))
            {
                selected = index;
                break;
            }
        }

        combo.SelectedIndex = selected;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < languages.Count)
            {
                _context.ApplyLanguage(languages[combo.SelectedIndex].Id);
            }
        };
        stack.Children.Add(combo);

        var count = new TextBlock
        {
            Style = (Style)FindResource("MicroText"),
            Margin = new Thickness(0, 12, 0, 0),
            // A bare number told the user nothing.
            Text = _context.T("aboutLanguagesValue", languages.Count.ToString("0", CultureInfo.CurrentCulture)),
        };
        stack.Children.Add(count);
        return stack;
    }

    private FrameworkElement BuildThemeStep()
    {
        var stack = BuildStepText("wizardThemeTitle", "wizardThemeBody");

        var modes = new[]
        {
            ("System", _context.T("settingsThemeSystem")),
            ("Light", _context.T("settingsThemeLight")),
            ("Dark", _context.T("settingsThemeDark")),
        };

        var modeCombo = new ComboBox
        {
            Style = (Style)FindResource("FlatCombo"),
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 16, 0, 16),
        };

        foreach (var (_, label) in modes)
        {
            modeCombo.Items.Add(label);
        }

        var selectedMode = 0;
        for (var index = 0; index < modes.Length; index += 1)
        {
            if (string.Equals(modes[index].Item1, _context.Settings.ThemeMode, StringComparison.Ordinal))
            {
                selectedMode = index;
                break;
            }
        }

        modeCombo.SelectedIndex = selectedMode;
        modeCombo.SelectionChanged += (_, _) =>
        {
            if (modeCombo.SelectedIndex >= 0 && modeCombo.SelectedIndex < modes.Length)
            {
                _context.Settings.ThemeMode = modes[modeCombo.SelectedIndex].Item1;
                _context.SaveSettings();
                _context.ApplyTheme();
            }
        };
        stack.Children.Add(modeCombo);

        var grid = new UniformGrid { Columns = 7 };
        foreach (var theme in ThemeCatalog.All)
        {
            grid.Children.Add(BuildSwatch(theme));
        }

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 260,
            Content = grid,
        };
        stack.Children.Add(scroll);
        return stack;
    }

    private FrameworkElement BuildSwatch(ThemePalette theme)
    {
        var label = _context.T("theme" + Capitalize(theme.Id));
        var selected = string.Equals(_context.Settings.Theme, theme.Id, StringComparison.OrdinalIgnoreCase);
        var swatch = new Border
        {
            Height = 44,
            Margin = new Thickness(0, 0, 8, 8),
            CornerRadius = new CornerRadius(9),
            Cursor = Cursors.Hand,
            ToolTip = label,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Accent)),
            BorderThickness = new Thickness(2),
            BorderBrush = selected ? (Brush)FindResource("BrushForeground") : Brushes.Transparent,
        };
        swatch.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, label);
        swatch.MouseLeftButtonUp += (_, _) =>
        {
            _context.Settings.Theme = theme.Id;
            _context.SaveSettings();
            _context.ApplyTheme();
            RenderStep();
        };
        return swatch;
    }

    private FrameworkElement BuildWelcomeStep()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var mark = new Border
        {
            Width = 84,
            Height = 84,
            CornerRadius = new CornerRadius(22),
            Background = (Brush)FindResource("BrushAccent"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 22),
        };
        mark.Child = BuildClockGlyph("BrushOnAccent", 46, 3.2);
        stack.Children.Add(mark);
        stack.Children.Add(Ui.Text("wizardWelcomeTitle", "DisplayText"));
        var body = Ui.Text("wizardWelcomeBody", "MutedText");
        body.MaxWidth = 620;
        body.HorizontalAlignment = HorizontalAlignment.Left;
        body.Margin = new Thickness(0, 12, 0, 0);
        body.FontSize = 15;
        stack.Children.Add(body);

        var facts = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };
        facts.Children.Add(Fact("IconShield", "docsPrivacyBody"));
        facts.Children.Add(Fact("IconClock", "docsTrackingBody"));
        stack.Children.Add(facts);
        return stack;
    }

    private FrameworkElement BuildTourStep()
    {
        var stack = BuildStepText("wizardTourTitle", "wizardTourBody");
        var list = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        list.Children.Add(TourRow("navDashboard", "dashboardSubtitle"));
        list.Children.Add(TourRow("navHistory", "historySubtitle"));
        list.Children.Add(TourRow("navSettings", "settingsSubtitle"));
        list.Children.Add(TourRow("navBackup", "backupSubtitle"));
        stack.Children.Add(list);
        return stack;
    }

    private FrameworkElement Fact(string iconKey, string bodyKey)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource("Icon"),
            Data = (Geometry)FindResource(iconKey),
            Margin = new Thickness(0, 2, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "BrushAccent");
        row.Children.Add(icon);
        var text = Ui.Text(bodyKey, "MutedText");
        text.MaxWidth = 560;
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private FrameworkElement TourRow(string titleKey, string bodyKey)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Ui.Text(titleKey, "BodyStrong"));
        var body = Ui.Text(bodyKey, "MutedText");
        body.MaxWidth = 440;
        Grid.SetColumn(body, 1);
        row.Children.Add(body);
        return row;
    }

    private StackPanel BuildStepText(string titleKey, string bodyKey)
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text(titleKey, "TitleText"));
        var body = Ui.Text(bodyKey, "MutedText");
        body.MaxWidth = 640;
        body.HorizontalAlignment = HorizontalAlignment.Left;
        body.Margin = new Thickness(0, 8, 0, 0);
        body.FontSize = 14;
        stack.Children.Add(body);
        return stack;
    }

    private void Complete()
    {
        _context.Settings.CompletedSetup = true;
        _context.SaveSettings();
        AutostartHelper.SetEnabled(_context.Settings.LaunchOnStartup, _context.Settings.SilentStart);
        DialogResult = true;
        Close();
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
