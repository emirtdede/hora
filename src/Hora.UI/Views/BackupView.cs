using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hora.Core.Presentation;
using Hora.Storage.Backup;
using Hora.Storage.Paths;
using Hora.UI.Services;

namespace Hora.UI.Views;

/// <summary>Backup card: provider discovery, connect, sync, restore, and history.</summary>
public sealed class BackupView : PageBase
{
    private readonly Action<string, ToastKind> _toast;
    private readonly StackPanel _providerList = new();
    private readonly TextBlock _badge = Ui.Text("backupNotDetected", "MicroText");
    private readonly Border _badgeHost;
    private readonly TextBlock _folder = Ui.Text("backupFolder", "MutedText");
    private readonly TextBlock _lastSync = Ui.Text("backupLastSync", "MutedText");
    private readonly TextBlock _summary = Ui.ValueText("0", "KpiValue");
    private readonly TextBlock _summaryText = Ui.Text("backupSummary", "MutedText");
    private readonly TextBlock _fallback = Ui.Text("backupFolderFallback", "MutedText");
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _history = new();
    private readonly CheckBox _autoSync;
    private readonly Button _connect;
    private readonly Button _sync;
    private readonly Button _restore;
    private readonly Button _open;
    private readonly Button _disconnect;
    private bool _busy;
    private bool _suppress;

    public BackupView(AppContext context, Action<string, ToastKind> toast)
        : base(context)
    {
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));

        _badgeHost = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _badge,
        };
        _badgeHost.SetResourceReference(Border.BackgroundProperty, "BrushAccentFaint");

        _connect = Ui.Primary("backupConnect", () => _ = ConnectAsync());
        _sync = Ui.Secondary("backupSyncNow", () => _ = SyncAsync());
        _restore = Ui.Secondary("backupRestore", () => _ = RestoreAsync());
        _open = Ui.Secondary("backupOpenFolder", OpenFolder);
        _disconnect = Ui.Secondary("backupDisconnect", () => _ = DisconnectAsync());
        _autoSync = Ui.Toggle(context.Settings.AutoSync, value =>
        {
            if (_suppress)
            {
                return;
            }

            Context.Settings.AutoSync = value;
            Context.SaveSettings();
        });

        var root = new StackPanel();
        root.Children.Add(Header("backupTitle", "backupSubtitle"));

        var providerCard = Ui.Card(BuildProviderSection());
        root.Children.Add(providerCard);

        var statusCard = Ui.Card(BuildStatusSection());
        statusCard.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(statusCard);

        var historyCard = Ui.Card(BuildHistorySection());
        historyCard.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(historyCard);

        Content = root;
        Context.Backup.StatusChanged += OnStatusChanged;
        Unloaded += (_, _) => Context.Backup.StatusChanged -= OnStatusChanged;
        RefreshData();
    }

    private FrameworkElement BuildProviderSection()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("backupProvider", "HeadingText"));
        var help = Ui.Text("backupProviderHelp", "MutedText");
        help.Margin = new Thickness(0, 3, 0, 12);
        stack.Children.Add(help);
        stack.Children.Add(_providerList);
        return stack;
    }

    private FrameworkElement BuildStatusSection()
    {
        var stack = new StackPanel();
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(Ui.Text("backupSummary", "LabelText"));
        _summary.Margin = new Thickness(0, 6, 0, 0);
        left.Children.Add(_summary);
        _summaryText.Margin = new Thickness(0, 2, 0, 0);
        left.Children.Add(_summaryText);
        top.Children.Add(left);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _badgeHost.HorizontalAlignment = HorizontalAlignment.Right;
        right.Children.Add(_badgeHost);
        _lastSync.HorizontalAlignment = HorizontalAlignment.Right;
        _lastSync.Margin = new Thickness(0, 8, 0, 0);
        right.Children.Add(_lastSync);
        Grid.SetColumn(right, 1);
        top.Children.Add(right);
        stack.Children.Add(top);

        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.Text("backupFolder", "LabelText"));
        _folder.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(_folder);
        _fallback.Visibility = Visibility.Collapsed;
        _fallback.Margin = new Thickness(0, 6, 0, 0);
        _fallback.Foreground = (Brush)FindResource("BrushDanger");
        stack.Children.Add(_fallback);

        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.SettingRow("backupAutoSync", "backupAutoSyncHelp", _autoSync));
        stack.Children.Add(Ui.Divider());

        _actions.Margin = new Thickness(0, 2, 0, 0);
        stack.Children.Add(_actions);
        return stack;
    }

    private FrameworkElement BuildHistorySection()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("backupHistory", "HeadingText"));
        var note = Ui.Text("backupProtected", "MutedText");
        note.Margin = new Thickness(0, 3, 0, 12);
        stack.Children.Add(note);
        stack.Children.Add(_history);
        return stack;
    }

    private void OnStatusChanged(object? sender, BackupStatus status)
    {
        // The backup service raises this from the thread pool.
        if (Dispatcher.HasShutdownStarted)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(RefreshData));
    }

    public override void RefreshData()
    {
        RunSafe(LoadAsync, "load");
    }

    private async Task LoadAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            var status = await Context.Backup.GetStatusAsync().ConfigureAwait(true);
            var detected = BackupLocationDetector.DetectAll(Context.Settings.BackupCustomRoot, HoraPaths.LocalBackupDir);
            RenderProviders(detected, status);

            _badge.Text = Context.T(status.Connected ? "backupConnected" : "backupDisconnected");
            _badgeHost.SetResourceReference(Border.BackgroundProperty,
                status.Connected ? "BrushAccentFaint" : "BrushSurfaceAlt");
            _badge.SetResourceReference(TextBlock.ForegroundProperty,
                status.Connected ? "BrushAccent" : "BrushMuted");

            _folder.Text = status.Folder;
            _lastSync.Text = status.LastSync is null
                ? Context.T("commonNever")
                : Context.T("backupLastSync") + ": " + status.LastSync.Value.ToLocalTime()
                    .ToString("g", CultureInfo.CurrentCulture);
            _summary.Text = status.LocalRecordCount.ToString(CultureInfo.CurrentCulture);
            _summaryText.Text = Context.T("backupSummaryValue", status.LocalRecordCount, status.BackupCount);
            _fallback.Visibility = status.UsingFallback ? Visibility.Visible : Visibility.Collapsed;
            _fallback.Text = Context.T("backupFolderFallback") + ": " + status.Folder;

            _suppress = true;
            _autoSync.IsChecked = status.AutoSync;
            _suppress = false;

            RenderActions(status);
            await RenderHistoryAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or InvalidOperationException
                                              or System.Text.Json.JsonException
                                              or Microsoft.Data.Sqlite.SqliteException)
        {
            Context.Logger.Warn("Backup status could not be read.");
        }
        finally
        {
            _busy = false;
        }
    }

    private void RenderProviders(IReadOnlyList<BackupLocation> locations, BackupStatus status)
    {
        _providerList.Children.Clear();
        foreach (var location in locations)
        {
            var selected = string.Equals(location.Kind.ToString(), status.Provider, StringComparison.Ordinal);
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,
                location.Detected ? "BrushSuccess" : "BrushMuted");
            row.Children.Add(dot);

            var labels = new StackPanel();
            var name = Ui.Literal(ProviderLabel(location.Kind), "BodyStrong");
            labels.Children.Add(name);
            var detail = Ui.Literal(
                location.Detected
                    ? (string.IsNullOrEmpty(location.Root) ? Context.T("backupDetected") : location.Root)
                    : Context.T("backupNotDetected"),
                "MicroText");
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            labels.Children.Add(detail);
            Grid.SetColumn(labels, 1);
            row.Children.Add(labels);

            var pick = new Button
            {
                Style = (Style)FindResource("SecondaryButton"),
                Padding = new Thickness(12, 5, 12, 5),
                MinHeight = 30,
                Content = selected
                    ? (object)new System.Windows.Shapes.Path
                    {
                        Style = (Style)FindResource("Icon"),
                        Data = (Geometry)FindResource("IconCheck"),
                        Width = 14,
                        Height = 14,
                    }
                    : Context.T("commonApply"),
            };
            pick.IsEnabled = location.Kind == BackupProviderKind.Custom || location.Detected;
            pick.Click += (_, _) => SelectProvider(location.Kind);
            Grid.SetColumn(pick, 2);
            row.Children.Add(pick);

            var host = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 8, 12, 8),
                Child = row,
            };
            host.SetResourceReference(Border.BackgroundProperty, selected ? "BrushAccentFaint" : "BrushSurfaceAlt");
            _providerList.Children.Add(host);
        }

        if (locations.All(location => location.Kind != BackupProviderKind.Custom || !location.Detected))
        {
            var choose = Ui.Link("backupChooseFolder", ChooseCustomFolder);
            choose.Margin = new Thickness(0, 4, 0, 0);
            _providerList.Children.Add(choose);
        }
    }

    private void RenderActions(BackupStatus status)
    {
        _actions.Children.Clear();
        if (!status.Connected)
        {
            AddAction(_connect);
            AddAction(_open);
            return;
        }

        AddAction(_sync);
        AddAction(_restore);
        AddAction(_open);
        AddAction(_disconnect);
    }

    private void AddAction(Button button)
    {
        button.Margin = new Thickness(0, 0, 8, 0);
        _actions.Children.Add(button);
    }

    private async Task RenderHistoryAsync()
    {
        _history.Children.Clear();
        var engine = Context.Backup.Engine;
        if (engine is null)
        {
            return;
        }

        IReadOnlyList<BackupFileInfo> files;
        try
        {
            files = await engine.ListBackupsAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or InvalidOperationException)
        {
            _history.Children.Add(Ui.Text("backupNoHistory", "MutedText"));
            Context.Logger.Warn("Backup history could not be listed.");
            return;
        }

        if (files.Count == 0)
        {
            _history.Children.Add(Ui.Text("backupNoHistory", "MutedText"));
            return;
        }

        foreach (var file in files.Take(8))
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = Ui.Literal(file.Date.ToString("d", CultureInfo.CurrentCulture), "BodyStrong");
            row.Children.Add(name);

            var size = Ui.Literal(FormatSize(file.Length), "MicroText");
            Grid.SetColumn(size, 1);
            row.Children.Add(size);
            _history.Children.Add(row);
        }
    }

    private string ProviderLabel(BackupProviderKind kind) =>
        Context.T(AppContext.ProviderLabelKey(kind.ToString()));

    // File size units are standard symbols and stay invariant, like h and m.
    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_048_576 => (bytes / 1_048_576.0).ToString("0.0", CultureInfo.CurrentCulture) + " MB",
        >= 1024 => (bytes / 1024.0).ToString("0.0", CultureInfo.CurrentCulture) + " KB",
        _ => bytes.ToString(CultureInfo.CurrentCulture) + " B",
    };

    private void SelectProvider(BackupProviderKind kind)
    {
        Context.Settings.BackupProvider = kind.ToString();
        Context.SaveSettings();
        Context.Backup.ResolveEngine();
        RunSafe(LoadAsync, "load");
    }

    private void ChooseCustomFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Context.T("backupChooseFolder"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        if (BackupLocationDetector.IsUnsafePath(dialog.SelectedPath))
        {
            _toast(Context.T("openFolderFailed"), ToastKind.Error);
            return;
        }

        Context.Settings.BackupProvider = BackupProviderKind.Custom.ToString();
        Context.Settings.BackupCustomRoot = dialog.SelectedPath;
        Context.SaveSettings();
        Context.Backup.ResolveEngine();
        RunSafe(LoadAsync, "load");
    }

    private async Task ConnectAsync()
    {
        if (Context.Settings.BackupProvider == BackupProviderKind.Custom.ToString()
            && string.IsNullOrWhiteSpace(Context.Settings.BackupCustomRoot))
        {
            _toast(Context.T("backupChooseFolder"), ToastKind.Info);
            ChooseCustomFolder();
            return;
        }

        try
        {
            await Context.Backup.ConnectAsync().ConfigureAwait(true);
            _toast(Context.T("backupConnected"), ToastKind.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _toast(Context.T("backupSyncFailed"), ToastKind.Error);
            Context.Logger.Warn("Backup connect failed.");
        }

        RefreshData();
    }

    private async Task SyncAsync()
    {
        try
        {
            var result = await Context.Backup.SyncNowAsync().ConfigureAwait(true);
            _toast(Context.T(result.Status == BackupWriteStatus.Written ? "backupSynced" : "backupSyncFailed"),
                result.Status == BackupWriteStatus.Written ? ToastKind.Success : ToastKind.Error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _toast(Context.T("backupSyncFailed"), ToastKind.Error);
        }

        RefreshData();
    }

    private async Task RestoreAsync()
    {
        var dialog = new Dialogs.PromptWindow(
            Context.T("backupRestore"),
            Context.T("backupRestoreConfirm", Context.BackupFolder),
            Context.T("backupRestore"),
            Context.T("commonCancel"))
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var restored = false;
        try
        {
            restored = await Context.Backup.RestoreAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or InvalidOperationException
                                              or System.Text.Json.JsonException
                                              or Microsoft.Data.Sqlite.SqliteException)
        {
            Context.Logger.Warn("Restore failed in the view.");
        }

        _toast(Context.T(restored ? "backupRestoreDone" : "backupRestoreFailed"),
            restored ? ToastKind.Success : ToastKind.Error);
        if (restored)
        {
            Context.NotifyDataChanged();
        }

        RefreshData();
    }

    private async Task DisconnectAsync()
    {
        await Context.Backup.DisconnectAsync().ConfigureAwait(true);
        _toast(Context.T("backupDisconnected"), ToastKind.Info);
        RefreshData();
    }

    private void OpenFolder()
    {
        try
        {
            var folder = Context.BackupFolder;
            Directory.CreateDirectory(folder);
            BackupLocationDetector.OpenFolder(folder);
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or IOException
                                              or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _toast(Context.T("openFolderFailed"), ToastKind.Error);
        }
    }
}
