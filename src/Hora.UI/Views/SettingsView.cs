using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Hora.Core.Input;
using Hora.Core.Localization;
using Hora.Core.Theming;
using Hora.UI.Platform;
using Hora.UI.Services;

namespace Hora.UI.Views;

/// <summary>
/// Appearance, language, behavior, notifications, updates, and data management.
/// Each section exposes a scroll anchor so the second rail can jump to it.
/// </summary>
public sealed class SettingsView : PageBase
{
    private readonly Action<string> _navigate;
    private readonly Action<string, ToastKind> _toast;
    private readonly Dictionary<string, FrameworkElement> _sections = new(StringComparer.Ordinal);
    private readonly UniformGrid _themeGrid = new() { Columns = 7, Margin = new Thickness(0, 0, 0, 10) };
    private readonly TextBox _customHex;
    private readonly TextBlock _customHint = Ui.Text("settingsThemeCustomHelp", "MutedText");
    private readonly Button _applyCustom;
    private readonly Button[] _savedSwatches = new Button[12];
    private readonly Slider _zoom;
    private readonly Slider _fontScale;
    private readonly TextBlock _zoomValue = Ui.ValueText("100%", "BodyStrong");
    private readonly TextBlock _fontValue = Ui.ValueText("100%", "BodyStrong");
    private readonly ComboBox _languageCombo = new() { Style = (Style)Application.Current.FindResource("FlatCombo") };
    private readonly ComboBox _closeCombo = new() { Style = (Style)Application.Current.FindResource("FlatCombo") };
    private readonly ComboBox _hotkeyCombo = new() { Style = (Style)Application.Current.FindResource("FlatCombo") };
    private readonly ComboBox _iconCombo = new() { Style = (Style)Application.Current.FindResource("FlatCombo") };
    private readonly ComboBox _themeModeCombo = new() { Style = (Style)Application.Current.FindResource("FlatCombo") };
    private readonly TextBlock _hotkeyStatus = Ui.Text("settingsHotkeyHelp", "MutedText");
    private readonly TextBlock _updateStatus = Ui.Text("settingsUpdatesCurrent", "MutedText");
    private readonly TextBlock _dataPath = Ui.Text("settingsDataLocation", "MutedText");
    private readonly TextBlock _themeInvalid = Ui.Text("settingsThemeInvalid", "MutedText");
    private bool _suppress;
    public SettingsView(AppContext context, Action<string> navigate, Action<string, ToastKind> toast)
        : base(context)
    {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));

        _customHex = new TextBox
        {
            Style = (Style)Application.Current.FindResource("Input"),
            Width = 132,
            MaxLength = 7,
            Text = context.Settings.CustomAccent,
        };
        _applyCustom = Ui.Secondary("settingsThemeApply", ApplyCustomTheme);

        _zoom = new Slider
        {
            Style = (Style)Application.Current.FindResource("FlatSlider"),
            Minimum = 80,
            Maximum = 150,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            MinWidth = 150,
            MaxWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _fontScale = new Slider
        {
            Style = (Style)Application.Current.FindResource("FlatSlider"),
            Minimum = 90,
            Maximum = 140,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            MinWidth = 150,
            MaxWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _zoom.ValueChanged += (_, _) => OnZoomChanged();
        _fontScale.ValueChanged += (_, _) => OnFontChanged();

        var root = new StackPanel();
        root.Children.Add(Header("settingsTitle", "settingsSubtitle"));
        root.Children.Add(Section("appearance", BuildAppearance()));
        root.Children.Add(Section("language", BuildLanguage()));
        root.Children.Add(Section("behavior", BuildBehavior()));
        root.Children.Add(Section("notifications", BuildNotifications()));
        root.Children.Add(Section("updates", BuildUpdates()));
        root.Children.Add(Section("data", BuildData()));
        Content = root;

        RefreshData();
    }

    private FrameworkElement Section(string id, UIElement content)
    {
        var card = Ui.Card(content);
        card.Margin = new Thickness(0, 0, 0, 14);
        _sections[id] = card;
        return card;
    }

    /// <summary>Scrolls the hosting scroll viewer to a section header.</summary>
    public void ScrollToSection(string id)
    {
        if (!_sections.TryGetValue(id, out var section))
        {
            return;
        }

        var viewer = FindAncestor<ScrollViewer>(this);
        if (viewer is null)
        {
            return;
        }

        var point = section.TransformToAncestor(viewer).Transform(new Point(0, 0));
        viewer.ScrollToVerticalOffset(viewer.VerticalOffset + point.Y - 12);
    }

    private static T? FindAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private FrameworkElement BuildAppearance()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("settingsSectionAppearance", "HeadingText"));
        var help = Ui.Text("settingsThemeHelp", "MutedText");
        help.Margin = new Thickness(0, 3, 0, 14);
        stack.Children.Add(help);

        stack.Children.Add(Ui.SettingRow("settingsTheme", null, _themeModeCombo));
        stack.Children.Add(Ui.Divider());

        var grid = _themeGrid;
        grid.Children.Clear();
        foreach (var theme in ThemeCatalog.All)
        {
            grid.Children.Add(BuildSwatch(theme));
        }

        grid.Children.Add(BuildCustomSwatch());
        stack.Children.Add(grid);

        var customRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        customRow.Children.Add(_customHex);
        _applyCustom.Margin = new Thickness(8, 0, 0, 0);
        customRow.Children.Add(_applyCustom);
        stack.Children.Add(customRow);

        _customHint.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(_customHint);
        _themeInvalid.Margin = new Thickness(0, 4, 0, 0);
        _themeInvalid.Foreground = (Brush)FindResource("BrushDanger");
        _themeInvalid.Visibility = Visibility.Collapsed;
        stack.Children.Add(_themeInvalid);

        stack.Children.Add(Ui.Divider());

        var savedLabel = Ui.Text("settingsAccent", "LabelText");
        savedLabel.Margin = new Thickness(0, 0, 0, 8);
        stack.Children.Add(savedLabel);
        var savedRow = new StackPanel { Orientation = Orientation.Horizontal };
        for (var index = 0; index < _savedSwatches.Length; index += 1)
        {
            var slot = index;
            var button = new Button
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = string.Empty,
                ToolTip = string.Empty,
                Visibility = Visibility.Collapsed,
            };
            button.Template = SwatchTemplate();
            button.Click += (_, _) => ApplySavedTheme(slot);
            _savedSwatches[index] = button;
            savedRow.Children.Add(button);
        }

        stack.Children.Add(savedRow);

        stack.Children.Add(Ui.Divider());
        var zoomRow = new Grid();
        zoomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        zoomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var zoomLabels = new StackPanel();
        zoomLabels.Children.Add(Ui.Text("settingsZoom", "BodyStrong"));
        zoomLabels.Children.Add(Ui.Text("settingsZoomHelp", "MutedText"));
        zoomRow.Children.Add(zoomLabels);
        var zoomControl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        zoomControl.Children.Add(_zoom);
        _zoomValue.Margin = new Thickness(12, 0, 0, 0);
        zoomControl.Children.Add(_zoomValue);
        Grid.SetColumn(zoomControl, 1);
        zoomRow.Children.Add(zoomControl);
        stack.Children.Add(zoomRow);

        stack.Children.Add(Ui.Divider());

        var fontRow = new Grid();
        fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var fontLabels = new StackPanel();
        fontLabels.Children.Add(Ui.Text("settingsFontScale", "BodyStrong"));
        fontLabels.Children.Add(Ui.Text("settingsFontScaleHelp", "MutedText"));
        fontRow.Children.Add(fontLabels);
        var fontControl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        fontControl.Children.Add(_fontScale);
        _fontValue.Margin = new Thickness(12, 0, 0, 0);
        fontControl.Children.Add(_fontValue);
        Grid.SetColumn(fontControl, 1);
        fontRow.Children.Add(fontControl);
        stack.Children.Add(fontRow);

        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.SettingRow("settingsReducedMotion", null, Toggle(Context.Settings.ReducedMotion, value =>
        {
            if (_suppress)
            {
                return;
            }

            Context.Settings.ReducedMotion = value;
            Context.SaveSettings();
        })));
        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.SettingRow("settingsHighContrast", null, Toggle(Context.Settings.HighContrast, value =>
        {
            if (_suppress)
            {
                return;
            }

            Context.Settings.HighContrast = value;
            Context.SaveSettings();
        })));

        stack.Children.Add(Ui.Divider());
        var iconRow = new Grid();
        iconRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        iconRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170, MaxWidth = 240 });
        var iconLabels = new StackPanel();
        iconLabels.Children.Add(Ui.Text("settingsTrayIcon", "BodyStrong"));
        iconLabels.Children.Add(Ui.Text("settingsTrayIconHelp", "MutedText"));
        iconRow.Children.Add(iconLabels);
        _iconCombo.Margin = new Thickness(0);
        Grid.SetColumn(_iconCombo, 1);
        iconRow.Children.Add(_iconCombo);
        stack.Children.Add(iconRow);

        return stack;
    }

    private FrameworkElement BuildSwatch(ThemePalette theme)
    {
        var label = Context.T("theme" + Capitalize(theme.Id));
        var button = new Button
        {
            Height = 46,
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = System.Windows.Input.Cursors.Hand,
            Uid = theme.Id,
            ToolTip = label,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Accent)),
        };
        button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, label);
        button.Template = SwatchTemplate();
        button.Click += (_, _) => ApplyTheme(theme.Id);
        return button;
    }

    private FrameworkElement BuildCustomSwatch()
    {
        var label = Context.T("settingsThemeCustom");
        var button = new Button
        {
            Height = 46,
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = System.Windows.Input.Cursors.Hand,
            Uid = ThemeCatalog.CustomId,
            ToolTip = label,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Context.Settings.CustomAccent)),
        };
        button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, label);
        button.Template = SwatchTemplate();
        button.Click += (_, _) => ApplyTheme(ThemeCatalog.CustomId);
        return button;
    }

    private static ControlTemplate SwatchTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Swatch";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(
                System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82));
        template.Triggers.Add(hover);
        var active = new Trigger { Property = FrameworkElement.TagProperty, Value = "active" };
        active.Setters.Add(new Setter(
            Border.BorderBrushProperty,
            new DynamicResourceExtension("BrushForeground"),
            "Swatch"));
        template.Triggers.Add(active);
        return template;
    }

    private void ApplyTheme(string themeId)
    {
        var settings = Context.Settings;
        settings.Theme = themeId;
        if (themeId == ThemeCatalog.CustomId)
        {
            settings.CustomAccent = ThemeCatalog.NormalizeHex(_customHex.Text);
        }

        Context.SaveSettings();
        Context.ApplyTheme();
        _toast(Context.T("settingsThemeApply"), ToastKind.Success);
        RefreshData();
    }

    private void ApplyCustomTheme()
    {
        if (!ThemeCatalog.IsValidHex(_customHex.Text))
        {
            _themeInvalid.Visibility = Visibility.Visible;
            return;
        }

        _themeInvalid.Visibility = Visibility.Collapsed;
        Context.Settings.Theme = ThemeCatalog.CustomId;
        Context.Settings.CustomAccent = ThemeCatalog.NormalizeHex(_customHex.Text);
        Context.SaveSettings();
        Context.ApplyTheme();
        _toast(Context.T("settingsThemeApply"), ToastKind.Success);
        RefreshData();
    }

    private void ApplySavedTheme(int slot)
    {
        var saved = Context.Settings.SavedThemes;
        if (slot >= saved.Count)
        {
            return;
        }

        Context.Settings.Theme = ThemeCatalog.CustomId;
        Context.Settings.CustomAccent = saved[slot];
        _customHex.Text = saved[slot];
        Context.SaveSettings();
        Context.ApplyTheme();
        RefreshData();
    }

    private FrameworkElement BuildLanguage()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("settingsSectionLanguage", "HeadingText"));
        var help = Ui.Text("settingsLanguageHelp", "MutedText");
        help.Margin = new Thickness(0, 3, 0, 14);
        stack.Children.Add(help);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 190, MaxWidth = 280 });
        var labels = new StackPanel();
        labels.Children.Add(Ui.Text("settingsSectionLanguage", "BodyStrong"));
        labels.Children.Add(Ui.Literal(
            Context.T("aboutLanguagesValue", LanguageCatalog.All.Count.ToString(CultureInfo.CurrentCulture)),
            "MicroText"));
        row.Children.Add(labels);
        Grid.SetColumn(_languageCombo, 1);
        row.Children.Add(_languageCombo);
        stack.Children.Add(row);

        _languageCombo.DisplayMemberPath = nameof(LanguageOption.Label);
        _languageCombo.SelectedValuePath = nameof(LanguageOption.Id);
        _languageCombo.SelectionChanged += (_, _) =>
        {
            if (_suppress || _languageCombo.SelectedValue is not string id || id == Context.Settings.Language)
            {
                return;
            }

            Context.ApplyLanguage(id);
            _toast(Context.T("settingsLanguageChanged"), ToastKind.Success);
        };

        return stack;
    }

    private FrameworkElement BuildBehavior()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("settingsSectionBehavior", "HeadingText"));
        var help = Ui.Text("settingsBehaviorHelp", "MutedText");
        help.Margin = new Thickness(0, 3, 0, 14);
        stack.Children.Add(help);

        stack.Children.Add(Ui.SettingRow("settingsStartWithWindows", "settingsStartWithWindowsHelp",
            Toggle(Context.Settings.LaunchOnStartup, value =>
            {
                if (_suppress)
                {
                    return;
                }

                Context.Settings.LaunchOnStartup = value;
                Context.SaveSettings();
            })));
        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.SettingRow("settingsSilentStart", "settingsSilentStartHelp",
            Toggle(Context.Settings.SilentStart, value =>
            {
                if (_suppress)
                {
                    return;
                }

                Context.Settings.SilentStart = value;
                Context.SaveSettings();
            })));
        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.SettingRow("settingsJumpList", "settingsJumpListHelp",
            Toggle(Context.Settings.JumpList, value =>
            {
                if (_suppress)
                {
                    return;
                }

                Context.Settings.JumpList = value;
                Context.SaveSettings();
            })));
        stack.Children.Add(Ui.Divider());

        var closeRow = new Grid();
        closeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        closeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170, MaxWidth = 240 });
        var closeLabels = new StackPanel();
        closeLabels.Children.Add(Ui.Text("settingsCloseBehavior", "BodyStrong"));
        closeRow.Children.Add(closeLabels);
        Grid.SetColumn(_closeCombo, 1);
        closeRow.Children.Add(_closeCombo);
        stack.Children.Add(closeRow);

        _closeCombo.DisplayMemberPath = nameof(LanguageOption.Label);
        _closeCombo.SelectedValuePath = nameof(LanguageOption.Id);
        _closeCombo.SelectionChanged += (_, _) =>
        {
            if (_suppress || _closeCombo.SelectedValue is not string value)
            {
                return;
            }

            Context.Settings.CloseBehavior = value;
            Context.SaveSettings();
        };

        stack.Children.Add(Ui.Divider());

        var hotkeyRow = new Grid();
        hotkeyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hotkeyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170, MaxWidth = 240 });
        var hotkeyLabels = new StackPanel();
        hotkeyLabels.Children.Add(Ui.Text("settingsHotkey", "BodyStrong"));
        _hotkeyStatus.Margin = new Thickness(0, 2, 0, 0);
        hotkeyLabels.Children.Add(_hotkeyStatus);
        hotkeyRow.Children.Add(hotkeyLabels);
        Grid.SetColumn(_hotkeyCombo, 1);
        hotkeyRow.Children.Add(_hotkeyCombo);
        stack.Children.Add(hotkeyRow);

        _hotkeyCombo.DisplayMemberPath = nameof(LanguageOption.Label);
        _hotkeyCombo.SelectedValuePath = nameof(LanguageOption.Id);
        _hotkeyCombo.SelectionChanged += (_, _) =>
        {
            if (_suppress || _hotkeyCombo.SelectedValue is not string gesture)
            {
                return;
            }

            Context.Settings.Hotkey = gesture;
            Context.SaveSettings();
            _hotkeyStatus.Text = Context.T(Context.HotkeyAvailable
                ? "settingsHotkeyRegistered"
                : "settingsHotkeyInvalid");
        };

        return stack;
    }

    private FrameworkElement BuildNotifications()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("settingsSectionNotifications", "HeadingText"));
        var help = Ui.Text("settingsNotificationsHelp", "MutedText");
        help.Margin = new Thickness(0, 3, 0, 14);
        stack.Children.Add(help);

        stack.Children.Add(Ui.SettingRow("settingsNotifications", "settingsNotificationsHelp",
            Toggle(Context.Settings.Notifications, value =>
            {
                if (_suppress)
                {
                    return;
                }

                Context.Settings.Notifications = value;
                Context.SaveSettings();
            })));
        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.SettingRow("settingsSound", "settingsSoundHelp",
            Toggle(Context.Settings.NotificationSound, value =>
            {
                if (_suppress)
                {
                    return;
                }

                Context.Settings.NotificationSound = value;
                Context.SaveSettings();
            })));
        stack.Children.Add(Ui.Divider());
        var testRow = new StackPanel { Orientation = Orientation.Horizontal };
        testRow.Children.Add(Ui.Secondary("settingsSoundTest", () =>
        {
            SoundHelper.PlayNotification();
            _toast(Context.T("settingsSound"), ToastKind.Success);
        }));
        stack.Children.Add(testRow);
        return stack;
    }

    private FrameworkElement BuildUpdates()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("settingsSectionUpdates", "HeadingText"));
        var version = Ui.Literal(
            Context.T("commonVersion") + " " + (typeof(SettingsView).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"),
            "MutedText");
        version.Margin = new Thickness(0, 3, 0, 10);
        stack.Children.Add(version);
        _updateStatus.Margin = new Thickness(0, 0, 0, 12);
        stack.Children.Add(_updateStatus);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Ui.Secondary("settingsUpdatesCheck", () =>
        {
            _updateStatus.Text = Context.T("settingsUpdatesChecking");
            _ = CheckUpdatesAsync();
        }));
        stack.Children.Add(row);
        return stack;
    }

    private async Task CheckUpdatesAsync()
    {
        var result = await UpdateService.CheckAsync(Context.Logger).ConfigureAwait(true);
        _updateStatus.Text = result switch
        {
            UpdateCheckResult.UpdateAvailable => Context.T("settingsUpdatesAvailable"),
            UpdateCheckResult.Latest => Context.T("settingsUpdatesLatest"),
            _ => Context.T("settingsUpdatesError"),
        };
    }

    private FrameworkElement BuildData()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("settingsSectionData", "HeadingText"));

        _dataPath.Text = Context.T("settingsDataLocation") + ": " + Hora.Storage.Paths.HoraPaths.AppDir;
        _dataPath.Margin = new Thickness(0, 6, 0, 12);
        stack.Children.Add(_dataPath);

        var actions = new WrapPanel();
        actions.Children.Add(Ui.Secondary("settingsOpenDataFolder", OpenDataFolder));
        actions.Children.Add(new Border { Width = 8 });
        actions.Children.Add(Ui.Secondary("settingsCopySystemInfo", CopySystemInfo));
        actions.Children.Add(new Border { Width = 8 });
        actions.Children.Add(Ui.Secondary("settingsDiagnosticsLink", () => _navigate("diagnostics")));
        actions.Children.Add(new Border { Width = 8 });
        actions.Children.Add(Ui.Secondary("backupTitle", () => _navigate("backup")));
        stack.Children.Add(actions);

        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.Text("settingsResetTitle", "BodyStrong"));
        var help = Ui.Text("settingsResetHelp", "MutedText");
        help.Margin = new Thickness(0, 2, 0, 10);
        stack.Children.Add(help);
        var reset = Ui.Secondary("settingsResetConfirm", ResetData);
        reset.Foreground = (Brush)FindResource("BrushDanger");
        stack.Children.Add(reset);
        return stack;
    }

    private void OpenDataFolder()
    {
        try
        {
            Hora.Storage.Paths.HoraPaths.EnsureCreated();
            Hora.Storage.Backup.BackupLocationDetector.OpenFolder(Hora.Storage.Paths.HoraPaths.AppDir);
        }
        catch (DirectoryNotFoundException)
        {
            _toast(Context.T("openFolderFailed"), ToastKind.Error);
        }
    }

    private void CopySystemInfo()
    {
        try
        {
            Clipboard.SetText(SystemInfoReport.Build(Context));
            _toast(Context.T("systemInfoCopied"), ToastKind.Success);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _toast(Context.T("unknownError"), ToastKind.Error);
        }
    }

    private void ResetData()
    {
        var dialog = new Dialogs.PromptWindow(
            Context.T("settingsResetTitle"),
            Context.T("settingsResetConfirm"),
            Context.T("commonYes"),
            Context.T("commonCancel"))
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _ = ResetAsync();
    }

    private async Task ResetAsync()
    {
        try
        {
            await Context.Repository.DeleteAllAsync().ConfigureAwait(true);
            Context.NotifyDataChanged();
            _toast(Context.T("commonReset"), ToastKind.Success);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            _toast(Context.T("unknownError"), ToastKind.Error);
        }
    }

    private void OnZoomChanged()
    {
        if (_suppress)
        {
            return;
        }

        Context.Settings.UiScale = Math.Round(_zoom.Value / 100.0, 2);
        _zoomValue.Text = Math.Round(_zoom.Value).ToString("0", CultureInfo.CurrentCulture) + "%";
        Context.SaveSettingsDeferred();
    }

    private void OnFontChanged()
    {
        if (_suppress)
        {
            return;
        }

        Context.Settings.FontScale = Math.Round(_fontScale.Value / 100.0, 2);
        _fontValue.Text = Math.Round(_fontScale.Value).ToString("0", CultureInfo.CurrentCulture) + "%";
        Context.SaveSettingsDeferred();
    }

    public override void RefreshData()
    {
        _suppress = true;
        try
        {
            _zoom.Value = Math.Round(Context.Settings.UiScale * 100);
            _fontScale.Value = Math.Round(Context.Settings.FontScale * 100);
            _zoomValue.Text = Math.Round(_zoom.Value).ToString("0", CultureInfo.CurrentCulture) + "%";
            _fontValue.Text = Math.Round(_fontScale.Value).ToString("0", CultureInfo.CurrentCulture) + "%";
            _customHex.Text = Context.Settings.CustomAccent;

            FillThemeMode();
            FillLanguages();
            FillCloseBehaviors();
            FillHotkeys();
            FillIcons();
            RefreshSavedThemes();
            RefreshSwatchSelection();
        }
        finally
        {
            _suppress = false;
        }

    }

    public override void ApplyLocalization()
    {
        RefreshData();
    }

    private void FillThemeMode()
    {
        _themeModeCombo.Items.Clear();
        foreach (var (id, key) in new[]
                 {
                     ("System", "settingsThemeSystem"),
                     ("Light", "settingsThemeLight"),
                     ("Dark", "settingsThemeDark"),
                 })
        {
            _themeModeCombo.Items.Add(new LanguageOption(id, Context.T(key)));
        }

        _themeModeCombo.SelectedValue = Context.Settings.ThemeMode;
        if (_themeModeCombo.SelectedIndex < 0)
        {
            _themeModeCombo.SelectedIndex = 0;
        }

        _themeModeCombo.SelectionChanged -= OnThemeModeChanged;
        _themeModeCombo.SelectionChanged += OnThemeModeChanged;
    }

    private void OnThemeModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _themeModeCombo.SelectedValue is not string mode)
        {
            return;
        }

        Context.Settings.ThemeMode = mode;
        Context.SaveSettings();
        Context.ApplyTheme();
    }

    private void FillLanguages()
    {
        _languageCombo.Items.Clear();
        var options = new ObservableCollection<LanguageOption>(
            LanguageCatalog.All.Select(language => new LanguageOption(language.Id, language.NativeName)));
        foreach (var option in options)
        {
            _languageCombo.Items.Add(option);
        }

        _languageCombo.SelectedValue = Context.Settings.Language;
        if (_languageCombo.SelectedIndex < 0)
        {
            _languageCombo.SelectedIndex = 0;
        }
    }

    private void FillCloseBehaviors()
    {
        _closeCombo.Items.Clear();
        foreach (var (id, key) in new[]
                 {
                     ("ask", "settingsCloseAsk"),
                     ("tray", "settingsCloseTray"),
                     ("exit", "settingsCloseExit"),
                 })
        {
            _closeCombo.Items.Add(new LanguageOption(id, Context.T(key)));
        }

        _closeCombo.SelectedValue = Context.Settings.CloseBehavior;
        if (_closeCombo.SelectedIndex < 0)
        {
            _closeCombo.SelectedIndex = 0;
        }
    }

    private void FillHotkeys()
    {
        _hotkeyCombo.Items.Clear();
        foreach (var gesture in HotkeyCatalog.Presets)
        {
            _hotkeyCombo.Items.Add(new LanguageOption(gesture, gesture));
        }

        _hotkeyCombo.SelectedValue = Context.Settings.Hotkey;
        if (_hotkeyCombo.SelectedIndex < 0)
        {
            _hotkeyCombo.SelectedIndex = 0;
        }
    }

    private void FillIcons()
    {
        _iconCombo.Items.Clear();
        foreach (var option in IconGenerator.Options)
        {
            _iconCombo.Items.Add(new LanguageOption(option.Id, Context.T("icon" + Capitalize(option.Id))));
        }

        _iconCombo.SelectedValue = Context.Settings.IconId;
        if (_iconCombo.SelectedIndex < 0)
        {
            _iconCombo.SelectedIndex = 0;
        }

        _iconCombo.SelectionChanged -= OnIconChanged;
        _iconCombo.SelectionChanged += OnIconChanged;
    }

    private void OnIconChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _iconCombo.SelectedValue is not string id)
        {
            return;
        }

        Context.Settings.IconId = id;
        Context.SaveSettings();
        ShortcutHelper.TryUpdateIcon(id, out _);
    }

    private void RefreshSavedThemes()
    {
        var saved = Context.Settings.SavedThemes;
        for (var index = 0; index < _savedSwatches.Length; index += 1)
        {
            var button = _savedSwatches[index];
            if (index < saved.Count)
            {
                var hex = saved[index];
                button.Visibility = Visibility.Visible;
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                button.Uid = hex;
                button.ToolTip = hex;
                button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, hex);
            }
            else
            {
                button.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RefreshSwatchSelection()
    {
        foreach (var child in FindSwatches())
        {
            child.Tag = string.Equals(child.Uid, Context.Settings.Theme, StringComparison.OrdinalIgnoreCase)
                ? "active"
                : null;
        }
    }

    private IEnumerable<Button> FindSwatches()
    {
        foreach (var child in _themeGrid.Children)
        {
            if (child is Button button)
            {
                yield return button;
            }
        }

        // Saved custom accents live outside the theme grid, so they were never
        // marked as selected even when their hex matched the active theme.
        foreach (var saved in _savedSwatches)
        {
            if (saved.Visibility == Visibility.Visible)
            {
                yield return saved;
            }
        }
    }

    private CheckBox Toggle(bool value, Action<bool> changed) => Ui.Toggle(value, changed);

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private sealed record LanguageOption(string Id, string Label)
    {
        // The flat combo template renders the selection box through ToString
        // when no item template is present, so the label must be the text.
        public override string ToString() => Label;
    }
}
