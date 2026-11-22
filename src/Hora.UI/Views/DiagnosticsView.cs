using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hora.Core.Localization;
using Hora.Storage.Backup;
using Hora.Storage.Paths;
using Hora.UI.Platform;
using Hora.UI.Services;

namespace Hora.UI.Views;

/// <summary>Health checks, redacted report export, and log access.</summary>
public sealed class DiagnosticsView : PageBase
{
    private readonly Action<string, ToastKind> _toast;
    private readonly StackPanel _checks = new();
    private readonly StackPanel _logs = new();
    private readonly TextBlock _result = Ui.Text("diagnosticsRunChecks", "BodyStrong");
    private readonly List<DiagnosticsCheckResult> _results = new();
    private bool _checking;
    private readonly TextBlock _systemInfo = new()
    {
        Style = (Style)Application.Current.FindResource("CodeText"),
        Margin = new Thickness(0, 8, 0, 0),
    };

    public DiagnosticsView(AppContext context, Action<string, ToastKind> toast)
        : base(context)
    {
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));

        var root = new StackPanel();
        root.Children.Add(Header("diagnosticsTitle", "diagnosticsSubtitle"));

        var checks = Ui.Card(BuildChecks());
        root.Children.Add(checks);

        var info = Ui.Card(BuildSystemInfo());
        info.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(info);

        var logs = Ui.Card(BuildLogs());
        logs.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(logs);

        Content = root;
        RefreshData();
    }

    private FrameworkElement BuildChecks()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("diagnosticsRunChecks", "HeadingText"));
        _result.Margin = new Thickness(0, 4, 0, 12);
        stack.Children.Add(_result);
        stack.Children.Add(_checks);

        var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(Ui.Primary("diagnosticsRunChecks", () => RunSafe(RunChecksAsync, "checks")));
        var copy = Ui.Secondary("diagnosticsCopyReport", CopyReport);
        copy.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(copy);
        var export = Ui.Secondary("diagnosticsExportReport", ExportReport);
        export.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(export);
        stack.Children.Add(actions);
        return stack;
    }

    private FrameworkElement BuildSystemInfo()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("diagnosticsSystemInfo", "HeadingText"));
        _systemInfo.Text = SystemInfoReport.Build(Context);
        stack.Children.Add(_systemInfo);
        var note = Ui.Text("diagnosticsRedact", "MutedText");
        note.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(note);
        return stack;
    }

    private FrameworkElement BuildLogs()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("diagnosticsLogs", "HeadingText"));
        var note = Ui.Text("diagnosticsLogsHelp", "MutedText");
        note.Margin = new Thickness(0, 3, 0, 12);
        stack.Children.Add(note);
        stack.Children.Add(_logs);
        var open = Ui.Secondary("diagnosticsOpenLogs", OpenLogs);
        open.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(open);
        return stack;
    }

    public override void RefreshData()
    {
        _systemInfo.Text = SystemInfoReport.Build(Context);
        RenderLogs();
        RunSafe(RunChecksAsync, "checks");
    }

    /// <summary>
    /// The write probe touches the disk, so it runs on the pool and the rows are
    /// rendered once when it finishes. The page never blocks the UI thread.
    /// </summary>
    private async Task RunChecksAsync()
    {
        if (_checking)
        {
            return;
        }

        _checking = true;
        try
        {
            var results = await Task.Run(() =>
            {
                var list = new List<(string Key, bool Passed)>
                {
                    ("diagnosticsCheckStorage", ProbeStorage()),
                    ("diagnosticsCheckSettings",
                        File.Exists(HoraPaths.SettingsPath) || Context.Settings.CompletedSetup),
                    ("diagnosticsCheckBackup", !BackupLocationDetector.IsUnsafePath(Context.BackupFolder)),
                    ("diagnosticsCheckLocale",
                        LanguageCatalog.IsSupported(Context.Settings.Language)
                        && Translator.Instance.Catalog(Context.Settings.Language).Count > 0),
                    ("diagnosticsCheckHotkey", HotkeyService.TryParse(Context.Settings.Hotkey, out _, out _)),
                };
                return list;
            }).ConfigureAwait(true);

            _checks.Children.Clear();
            _results.Clear();
            var failures = 0;
            foreach (var (key, passed) in results)
            {
                _results.Add(new DiagnosticsCheckResult(key, passed));
                failures += AddCheckRow(key, passed);
            }

            _result.Text = Context.T(failures == 0 ? "diagnosticsAllPassed" : "diagnosticsSomeFailed");
            _result.SetResourceReference(TextBlock.ForegroundProperty,
                failures == 0 ? "BrushSuccess" : "BrushDanger");
        }
        finally
        {
            _checking = false;
        }
    }

    private static bool ProbeStorage()
    {
        try
        {
            HoraPaths.EnsureCreated();
            var probe = Path.Combine(HoraPaths.AppDir, $".write-{Environment.ProcessId}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or System.Security.SecurityException)
        {
            return false;
        }
    }

    private int AddCheckRow(string labelKey, bool passed)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource("Icon"),
            Data = (Geometry)FindResource(passed ? "IconCheck" : "IconWarning"),
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 10, 0),
        };
        icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,
            passed ? "BrushSuccess" : "BrushDanger");
        row.Children.Add(icon);

        var label = Ui.Text(labelKey, "MutedText");
        label.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        _checks.Children.Add(row);
        return passed ? 0 : 1;
    }

    private void RenderLogs()
    {
        _logs.Children.Clear();
        IReadOnlyList<string> lines;
        try
        {
            lines = Context.Logger.Tail(8);
        }
        catch (IOException)
        {
            lines = Array.Empty<string>();
        }

        if (lines.Count == 0)
        {
            _logs.Children.Add(Ui.Text("diagnosticsNoLogs", "MicroText"));
            return;
        }

        foreach (var line in lines)
        {
            var text = Ui.Literal(line, "CodeText");
            text.FontSize = 10.5;
            text.Margin = new Thickness(0, 0, 0, 2);
            _logs.Children.Add(text);
        }
    }

    private void CopyReport()
    {
        try
        {
            Clipboard.SetText(BuildReport());
            _toast(Context.T("reportCopied"), ToastKind.Success);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _toast(Context.T("unknownError"), ToastKind.Error);
        }
    }

    private void ExportReport()
    {
        try
        {
            Directory.CreateDirectory(HoraPaths.ReportDir);
            var name = "hora-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt";
            var path = Path.Combine(HoraPaths.ReportDir, name);
            File.WriteAllText(path, BuildReport());
            _toast(Context.T("diagnosticsReportSaved", path), ToastKind.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _toast(Context.T("unknownError"), ToastKind.Error);
        }
    }

    private string BuildReport()
    {
        var passed = _results.Count(result => result.Passed);
        var counts = Context.T("diagnosticsChecksCount",
            passed.ToString(CultureInfo.CurrentCulture),
            _results.Count.ToString(CultureInfo.CurrentCulture));
        return Core.Logging.Redact.Message(
            SystemInfoReport.Build(Context) + Environment.NewLine + counts + Environment.NewLine);
    }

    private void OpenLogs()
    {
        try
        {
            Directory.CreateDirectory(HoraPaths.LogDir);
            BackupLocationDetector.OpenFolder(HoraPaths.LogDir);
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or IOException
                                              or System.ComponentModel.Win32Exception)
        {
            _toast(Context.T("openFolderFailed"), ToastKind.Error);
        }
    }
}

/// <summary>Outcome of one diagnostics probe, kept for the exported report.</summary>
internal sealed record DiagnosticsCheckResult(string Key, bool Passed);
