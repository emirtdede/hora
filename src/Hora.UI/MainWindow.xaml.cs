using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Hora.Core.Localization;
using Hora.Core.Presentation;
using Hora.Core.Theming;
using Hora.Storage.Backup;
using Hora.Storage.Paths;
using Hora.UI.Dialogs;
using Hora.UI.Platform;
using Hora.UI.Services;
using Hora.UI.Views;
using Forms = System.Windows.Forms;
using Path = System.Windows.Shapes.Path;

namespace Hora.UI;

public partial class MainWindow : Window
{
    private sealed record Destination(string Id, string LabelKey, string IconKey);

    private static readonly Destination[] Destinations =
    {
        new("dashboard", "navDashboard", "IconDashboard"),
        new("history", "navHistory", "IconHistory"),
        new("settings", "navSettings", "IconSettings"),
        new("backup", "navBackup", "IconBackup"),
        new("diagnostics", "navDiagnostics", "IconDiagnostics"),
        new("docs", "navDocs", "IconDocs"),
        new("about", "navAbout", "IconAbout"),
        new("legal", "navLegal", "IconLegal"),
    };

    private readonly AppContext _context;
    private readonly Dictionary<string, PageBase> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _railButtons = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _tick = new();
    private readonly DispatcherTimer _geometrySave = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly Forms.NotifyIcon _tray = new();
    private readonly HotkeyService _hotkey;
    private bool _trayHintShown;
    private bool _exiting;
    private bool _totalBusy;
    private string _currentPage = string.Empty;

    public MainWindow(AppContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        InitializeComponent();

        _hotkey = new HotkeyService(context.Logger);
        _hotkey.Pressed += (_, _) => ToggleWindowVisibility();

        Left = context.Settings.Left;
        Top = context.Settings.Top;
        // A 150% scaled 1080p screen offers less than 780 DIU of work area, so the
        // window shrinks to fit instead of hiding its status bar under the taskbar.
        var work = SystemParameters.WorkArea;
        MaxWidth = work.Width;
        MaxHeight = work.Height;
        Width = Math.Min(context.Settings.Width, work.Width - 24);
        Height = Math.Min(context.Settings.Height, work.Height - 24);
        Opacity = context.Settings.Opacity;
        EnsureVisibleOnScreen();

        TitleVersion.Text = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        ToastService.Attach(ToastHost, ToastText);

        _context.SettingsChanged += OnContextSettingsChanged;
        _context.LanguageChanged += OnContextLanguageChanged;
        _context.DataChanged += OnContextDataChanged;
        // The backup service raises status from the thread pool; WPF controls
        // may only be touched on the dispatcher.
        _context.Backup.StatusChanged += OnBackupStatusChanged;

        BuildRail();
        BuildPages();
        NavigateTo("dashboard");
        SetWindowIcon();
        SetupTray();
        ApplyWindowChromeSettings();
        ApplyScale();

        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        // Geometry is written after the move or resize settles, not on every
        // intermediate LocationChanged event during a drag.
        _geometrySave.Tick += (_, _) =>
        {
            _geometrySave.Stop();
            SaveGeometry();
        };
        LocationChanged += (_, _) => ScheduleGeometrySave();
        SizeChanged += (_, _) => ScheduleGeometrySave();

        _tick.Interval = TimeSpan.FromSeconds(1);
        _tick.Tick += OnTick;
    }

    public void StartTracking()
    {
        ApplyScale();
        RefreshCurrentPage();
        SyncStatusBar();
    }

    /// <summary>Brings the window to the front, restoring it from the tray or minimized state.</summary>
    public void ShowFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void NavigateTo(string pageId)
    {
        if (!_pages.TryGetValue(pageId, out var page))
        {
            return;
        }

        _currentPage = pageId;
        PageHost.Content = page;
        foreach (var (id, button) in _railButtons)
        {
            button.Tag = string.Equals(id, pageId, StringComparison.Ordinal) ? "active" : null;
        }

        BuildRailContent(pageId);
        page.RefreshData();
        UpdateRailTitle();
        PageScroll.ScrollToTop();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // F5 refreshes the visible page without leaving it, as the docs promise.
        if (e.Key == Key.F5 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
        {
            RefreshCurrentPage();
            SyncStatusBar();
            e.Handled = true;
        }

        if (e.Key == Key.W && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            HideToTray();
            e.Handled = true;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The window handle exists now, so the global shortcut can be claimed.
        // Without this call it stays dead until the user changes a setting.
        _context.HotkeyAvailable = _hotkey.TryRegister(this, _context.Settings.Hotkey);
        _tick.Start();
        SyncStatusBar();
        _ = RefreshTotalAsync();
        if (_context.Settings.Notifications && _context.Settings.CompletedSetup)
        {
            _context.Logger.Info("Main window ready.");
        }

        if (_context.Settings.BackupConnected && _context.Settings.AutoSync)
        {
            _ = _context.Backup.SyncNowAsync();
        }
        else if (!_context.Settings.RestorePrompted)
        {
            _context.Settings.RestorePrompted = true;
            _context.SaveSettings(notify: false);
            // The prompt is owned by this window, and Owner is only legal after
            // Show() has completed, not inside the Loaded handler.
            Dispatcher.BeginInvoke(new Action(() => _ = OfferRestoreIfNeededAsync()),
                DispatcherPriority.ApplicationIdle);
        }
    }

    private async Task OfferRestoreIfNeededAsync()
    {
        try
        {
            var empty = await _context.Repository.CountRowsAsync() == 0;
            if (!empty || !await _context.Backup.HasRestorableBackupAsync())
            {
                return;
            }

            var folder = _context.BackupFolder;
            var prompt = new PromptWindow(
                _context.T("backupRecoveryTitle"),
                _context.T("backupRecoveryBody", folder),
                _context.T("backupRecoveryRestore"),
                _context.T("backupRecoveryFresh"));
            if (IsVisible)
            {
                prompt.Owner = this;
            }

            if (prompt.ShowDialog() == true)
            {
                var restored = await _context.Backup.RestoreAsync();
                ToastService.Show(
                    _context.T(restored ? "backupRestoreDone" : "backupRestoreFailed"),
                    restored ? ToastKind.Success : ToastKind.Error);
                ApplyScale();
                _context.ApplyTheme();
                RebuildChrome();
                NavigateTo("settings");
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            _context.Logger.Warn($"Restore prompt failed: {exception.GetType().Name}");
        }
    }

    private void BuildRail()
    {
        DestinationList.Children.Clear();
        _railButtons.Clear();
        foreach (var destination in Destinations)
        {
            var icon = new Path
            {
                Style = (Style)FindResource("Icon"),
                Data = (Geometry)FindResource(destination.IconKey),
            };

            var button = new Button
            {
                Style = (Style)FindResource("RailButton"),
                Content = icon,
                Tag = null,
            };
            button.Click += (_, _) => NavigateTo(destination.Id);
            button.SetBinding(ToolTipProperty, new System.Windows.Data.Binding($"[{destination.LabelKey}]")
            {
                Source = LocalizationProxy.Instance,
            });
            button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, _context.T(destination.LabelKey));
            button.SetValue(
                System.Windows.Automation.AutomationProperties.AutomationIdProperty,
                $"Nav{destination.Id}");
            _railButtons[destination.Id] = button;
            DestinationList.Children.Add(button);
        }

        if (_railButtons.TryGetValue(_currentPage, out var active))
        {
            active.Tag = "active";
        }
    }

    private void BuildPages()
    {
        _pages["dashboard"] = new DashboardView(_context, NavigateTo);
        _pages["history"] = new HistoryView(_context);
        _pages["settings"] = new SettingsView(_context, NavigateTo, ShowToast);
        _pages["backup"] = new BackupView(_context, ShowToast);
        _pages["diagnostics"] = new DiagnosticsView(_context, ShowToast);
        _pages["docs"] = new DocsView(_context, NavigateTo);
        _pages["about"] = new AboutView(_context, ShowToast);
        _pages["legal"] = new LegalView(_context);
        foreach (var page in _pages.Values)
        {
            page.HorizontalAlignment = HorizontalAlignment.Stretch;
            page.VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    private void BuildRailContent(string pageId)
    {
        RailContent.Children.Clear();
        switch (pageId)
        {
            case "dashboard":
                AddRailAction("railPause", "IconPause", TogglePause_Click);
                AddRailAction("railHide", "IconTray", (_, _) => HideToTray());
                AddRailAction("navBackup", "IconBackup", (_, _) => NavigateTo("backup"));
                AddRailAction("navDiagnostics", "IconDiagnostics", (_, _) => NavigateTo("diagnostics"));
                AddRailNote("backupProtected");
                break;

            case "history":
                AddRailNote("historySubtitle");
                AddRailNote("dashboardTrackingNote");
                break;

            case "settings":
                foreach (var (key, tag) in new[]
                         {
                             ("settingsSectionAppearance", "appearance"),
                             ("settingsSectionLanguage", "language"),
                             ("settingsSectionBehavior", "behavior"),
                             ("settingsSectionNotifications", "notifications"),
                             ("settingsSectionUpdates", "updates"),
                             ("settingsSectionData", "data"),
                         })
                {
                    AddRailActionForTag(key, tag);
                }

                AddRailAction("settingsDiagnosticsLink", "IconDiagnostics", (_, _) => NavigateTo("diagnostics"));
                break;

            case "backup":
                AddRailNote("backupProviderHelp");
                AddRailNote("backupProtected");
                break;

            case "diagnostics":
                AddRailNote("diagnosticsRedact");
                AddRailNote("settingsCopySystemInfo");
                break;

            case "docs":
                AddRailNote("docsSubtitle");
                break;

            case "about":
                AddRailAction("aboutGitHub", "IconCode", (_, _) => OpenLink(AboutLinks.GitHub));
                AddRailAction("aboutSponsor", "IconHeart", (_, _) => OpenLink(AboutLinks.Sponsor));
                break;

            case "legal":
                AddRailNote("legalSubtitle");
                break;
        }

        RefreshRailFooter();
    }

    private void AddRailAction(string labelKey, string iconKey, RoutedEventHandler handler)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new Path
        {
            Style = (Style)FindResource("Icon"),
            Data = (Geometry)FindResource(iconKey),
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 10, 0),
        });
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding($"[{labelKey}]")
        {
            Source = LocalizationProxy.Instance,
        });
        content.Children.Add(text);

        var button = new Button
        {
            Style = (Style)FindResource("RailRow"),
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 4),
        };
        button.Click += handler;
        RailContent.Children.Add(button);
    }

    private void AddRailActionForTag(string labelKey, string tag)
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding($"[{labelKey}]")
        {
            Source = LocalizationProxy.Instance,
        });
        var button = new Button
        {
            Style = (Style)FindResource("RailRow"),
            Content = text,
            Tag = tag,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 2),
        };
        button.Click += (_, _) =>
        {
            if (_pages.TryGetValue("settings", out var page) && page is SettingsView settings)
            {
                settings.ScrollToSection(tag);
            }
        };
        RailContent.Children.Add(button);
    }

    private void AddRailNote(string labelKey)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 8, 12, 4),
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "BrushMuted");
        text.SetResourceReference(TextBlock.FontSizeProperty, "FontSmall");
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding($"[{labelKey}]")
        {
            Source = LocalizationProxy.Instance,
        });
        RailContent.Children.Add(text);
    }

    private void UpdateRailTitle()
    {
        var destination = Destinations.FirstOrDefault(d => d.Id == _currentPage);
        if (destination is null)
        {
            return;
        }

        RailTitle.Text = _context.T(destination.LabelKey);
        RailSubtitle.Text = _currentPage switch
        {
            "dashboard" => _context.T("dashboardSubtitle"),
            "history" => _context.T("historySubtitle"),
            "settings" => _context.T("settingsSubtitle"),
            "backup" => _context.T("backupSubtitle"),
            "diagnostics" => _context.T("diagnosticsSubtitle"),
            "docs" => _context.T("docsSubtitle"),
            "about" => _context.T("aboutSubtitle"),
            _ => _context.T("legalSubtitle"),
        };
    }

    private void RefreshRailFooter()
    {
        var connected = _context.Settings.BackupConnected;
        RailFooterTitle.Text = connected ? _context.T("backupConnected") : _context.T("backupDisconnected");
        var providerKey = AppContext.ProviderLabelKey(_context.Settings.BackupProvider);
        var lastSync = _context.Settings.LastSyncUtc;
        RailFooterBody.Text = lastSync is null
            ? _context.T(providerKey)
            : _context.T("backupLastSync") + ": " + lastSync.Value.ToLocalTime().ToString("g",
                System.Globalization.CultureInfo.CurrentCulture);
    }

    private void RebuildChrome()
    {
        BuildRail();
        UpdateRailTitle();
        BuildRailContent(_currentPage);
        foreach (var page in _pages.Values)
        {
            page.ApplyLocalization();
        }

        SyncStatusBar();
        SetupTrayMenu();
    }

    private void OnContextSettingsChanged(object? sender, EventArgs e) => OnSettingsChanged();

    private void OnContextLanguageChanged(object? sender, EventArgs e) => RebuildChrome();

    private void OnContextDataChanged(object? sender, EventArgs e) => RefreshCurrentPage();

    private void OnBackupStatusChanged(object? sender, BackupStatus status)
    {
        if (!Dispatcher.HasShutdownStarted)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRailFooter));
        }
    }

    private void DetachContextEvents()
    {
        _context.SettingsChanged -= OnContextSettingsChanged;
        _context.LanguageChanged -= OnContextLanguageChanged;
        _context.DataChanged -= OnContextDataChanged;
        _context.Backup.StatusChanged -= OnBackupStatusChanged;
    }

    private void OnSettingsChanged()
    {
        ApplyScale();
        _context.ApplyTheme();
        SetWindowIcon();
        ApplyWindowChromeSettings();
        AutostartHelper.SetEnabled(_context.Settings.LaunchOnStartup, _context.Settings.SilentStart);
        _context.HotkeyAvailable = _hotkey.TryRegister(this, _context.Settings.Hotkey);
        JumpListHelper.Apply(
            _context.Settings.JumpList,
            Environment.ProcessPath ?? string.Empty,
            IconGenerator.EnsureIco(_context.Settings.IconId));
        RefreshRailFooter();
        SyncStatusBar();
    }

    private void ApplyWindowChromeSettings()
    {
        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
        if (chrome is null)
        {
            return;
        }

        chrome.ResizeBorderThickness = _context.Settings.View == "Mini"
            ? new Thickness(4)
            : new Thickness(6);
    }

    /// <summary>Applies the accessibility zoom to the page host without breaking layout.</summary>
    private void ApplyScale()
    {
        var scale = Math.Clamp(_context.Settings.UiScale * _context.Settings.FontScale, 0.8, 2.1);
        PageHost.LayoutTransform = new ScaleTransform(scale, scale);
        PageHost.Margin = scale > 1.001
            ? new Thickness(0, 0, 0, 40 * (scale - 1))
            : new Thickness(0);
    }

    private void ShowToast(string message, ToastKind kind) => ToastService.Show(message, kind);

    private void SetWindowIcon()
    {
        try
        {
            var png = IconGenerator.EnsurePng(_context.Settings.IconId, 256);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(png, UriKind.Absolute);
            image.EndInit();
            Icon = image;
            TitleMark.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(ThemeCatalog.Get(_context.Settings.Theme).Accent));
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or UriFormatException)
        {
            _context.Logger.Warn("Window icon could not be loaded.");
        }
    }

    private void SetupTray()
    {
        _tray.Visible = true;
        _tray.Text = _context.T("trayTooltip");
        try
        {
            _tray.Icon = IconGenerator.LoadIcon(_context.Settings.IconId);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            _tray.Icon = System.Drawing.SystemIcons.Application;
        }

        _tray.DoubleClick += (_, _) => ToggleWindowVisibility();
        SetupTrayMenu();
    }

    private void SetupTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_context.T("trayShow"), null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_context.T("trayPause"), null, (_, _) => SetPaused(true));
        menu.Items.Add(_context.T("trayResume"), null, (_, _) => SetPaused(false));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_context.T("traySettings"), null, (_, _) =>
        {
            ShowWindow();
            NavigateTo("settings");
        });
        menu.Items.Add(_context.T("trayQuit"), null, (_, _) => ExitApplication());
        _tray.ContextMenuStrip?.Dispose();
        _tray.ContextMenuStrip = menu;
        _tray.Text = _context.T("trayTooltip");
    }

    private void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            HideToTray();
        }
        else
        {
            ShowWindow();
        }
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void HideToTray()
    {
        Hide();
        if (_trayHintShown || !_context.Settings.Notifications)
        {
            return;
        }

        _trayHintShown = true;
        try
        {
            _tray.BalloonTipTitle = _context.T("trayHintTitle");
            _tray.BalloonTipText = _context.T("trayHintBody");
            _tray.ShowBalloonTip(3000);
            if (_context.Settings.NotificationSound)
            {
                SoundHelper.PlayNotification();
            }
        }
        catch (InvalidOperationException)
        {
            // Balloon tips can fail when notifications are disabled by policy.
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_context.Engine.IsPaused)
        {
            _context.Engine.Tick();
        }

        if (_tick.Tag is int seconds && seconds >= 5)
        {
            _tick.Tag = 0;
            _ = FlushAndRefreshAsync();
        }
        else
        {
            _tick.Tag = _tick.Tag is int current ? current + 1 : 1;
        }

        SyncStatusBar();
    }

    private async Task FlushAndRefreshAsync()
    {
        try
        {
            await _context.Engine.FlushAsync();
            await RefreshTotalAsync();
            RefreshCurrentPage();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException
                                              or Microsoft.Data.Sqlite.SqliteException)
        {
            _context.Logger.Warn("Refresh cycle failed.");
        }
    }

    private void RefreshCurrentPage()
    {
        if (_pages.TryGetValue(_currentPage, out var page))
        {
            page.RefreshData();
        }
    }

    private void SyncStatusBar()
    {
        var paused = _context.Engine.IsPaused;
        StatusText.Text = paused ? _context.T("railStatusPaused") : _context.T("railStatusActive");
        StatusDot.Fill = (Brush)(TryFindResource(paused ? "BrushMuted" : "BrushSuccess") ?? Brushes.Gray);
        PauseIcon.Data = (Geometry)FindResource(paused ? "IconPlay" : "IconPause");
        BtnRailPause.ToolTip = _context.T(paused ? "railResume" : "railPause");

        var meta = $"{_context.T("commonVersion")} {TitleVersion.Text} - {LanguageCatalog.Get(_context.Settings.Language).NativeName}";
        if (!string.Equals(StatusMeta.Text, meta, StringComparison.Ordinal))
        {
            StatusMeta.Text = meta;
        }
    }

    /// <summary>
    /// Refreshes the today total after a flush. A slow query never stacks another
    /// one on top of it, and shutdown does not produce an unobserved exception.
    /// </summary>
    private async Task RefreshTotalAsync()
    {
        if (_totalBusy)
        {
            return;
        }

        _totalBusy = true;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var rows = await _context.Repository.GetDayAsync(today);
            // The status bar must agree with the dashboard, so it uses the same
            // active total and excludes idle, lock, sleep, and meeting buckets.
            var total = UsageSummary.Total(rows) + _context.Engine.PendingActiveSeconds;
            StatusTotal.Text = $"{_context.T("commonToday")}: {DurationFormatter.Format(total)}";
        }
        catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException
                                              or IOException
                                              or ObjectDisposedException)
        {
            StatusTotal.Text = string.Empty;
        }
        finally
        {
            _totalBusy = false;
        }
    }

    private void TogglePause_Click(object sender, RoutedEventArgs e) =>
        SetPaused(!_context.Engine.IsPaused);

    private void SetPaused(bool paused)
    {
        _context.Engine.IsPaused = paused;
        if (!paused)
        {
            _context.Engine.Tick();
        }

        SyncStatusBar();
        RefreshCurrentPage();
        if (_currentPage == "dashboard")
        {
            BuildRailContent("dashboard");
        }
    }

    private void GoSettings_Click(object sender, RoutedEventArgs e) => NavigateTo("settings");

    private void Minimize_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Dragging can fail if the mouse button was released between events.
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ScheduleGeometrySave()
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        _geometrySave.Stop();
        _geometrySave.Start();
    }

    /// <summary>
    /// A saved position can point at a monitor that is no longer connected. The
    /// window is centered on the primary work area instead of opening off screen.
    /// </summary>
    private void EnsureVisibleOnScreen()
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        var visible = Left < virtualRight - 100
                      && Left + Width > virtualLeft + 100
                      && Top < virtualBottom - 60
                      && Top + Height > virtualTop + 60;
        if (visible)
        {
            return;
        }

        var work = SystemParameters.WorkArea;
        Width = Math.Min(Width, work.Width);
        Height = Math.Min(Height, work.Height);
        Left = work.Left + Math.Max(0, (work.Width - Width) / 2);
        Top = work.Top + Math.Max(0, (work.Height - Height) / 2);
    }

    private void SaveGeometry()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _context.Settings.Left = Left;
        _context.Settings.Top = Top;
        _context.Settings.Width = Width;
        _context.Settings.Height = Height;
        _context.SettingsStore.Save(_context.Settings);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        _geometrySave.Stop();
        SaveGeometry();

        var behavior = _context.Settings.CloseBehavior;
        if (behavior == "ask")
        {
            var dialog = new CloseDialog(_context.T("closeAskTitle"), _context.T("closeAskBody"),
                _context.T("closeAskTray"), _context.T("closeAskExit"), _context.T("closeAskRemember"));
            if (IsVisible)
            {
                dialog.Owner = this;
            }

            var result = dialog.ShowDialog();
            if (result is null)
            {
                e.Cancel = true;
                return;
            }

            if (dialog.Remember)
            {
                _context.Settings.CloseBehavior = result == true ? "tray" : "exit";
                _context.SettingsStore.Save(_context.Settings);
            }

            if (result == true)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
        }
        else if (behavior == "tray")
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        ShutdownCleanly();
        Application.Current.Shutdown();
    }

    private void ExitApplication()
    {
        _exiting = true;
        ShutdownCleanly();
        Application.Current.Shutdown();
    }

    /// <summary>Called after an unexpected UI failure: persist what is safe, then exit.</summary>
    public void ExitForFailure() => ExitApplication();

    private void ShutdownCleanly()
    {
        _exiting = true;
        _tick.Stop();
        _geometrySave.Stop();
        try
        {
            // A stalled disk or a paused cloud sync must not freeze the exit path.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var flush = _context.Engine.FlushAsync(timeout.Token);
            var backup = _context.Settings.BackupConnected
                ? _context.Backup.FlushAsync(timeout.Token)
                : Task.CompletedTask;
            Task.WhenAll(flush, backup).Wait(TimeSpan.FromSeconds(4));

            SaveGeometry();
            _context.SaveSettings(notify: false);
            _context.Logger.Info("Hora stopped.");
        }
        catch (Exception exception) when (exception is IOException
                                              or AggregateException
                                              or InvalidOperationException
                                              or OperationCanceledException)
        {
            _context.Logger.Warn("Shutdown flush failed.");
        }
        finally
        {
            DetachContextEvents();
            ToastService.Detach(ToastHost);
            _tray.Visible = false;
            _tray.Dispose();
            _hotkey.Dispose();
        }
    }

    private static void OpenLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
    }
}

/// <summary>Canonical external links used by About, support, and documentation.</summary>
public static class AboutLinks
{
    public const string Website = "https://vellium.dev";
    public const string GitHub = "https://github.com/emirtdede/hora";
    public const string Sponsor = "https://github.com/sponsors/emirtdede";
    public const string Issues = "https://github.com/emirtdede/hora/issues";
    public const string Releases = "https://github.com/emirtdede/hora/releases";
    public const string License = "https://github.com/emirtdede/hora/blob/main/LICENSE";
}
