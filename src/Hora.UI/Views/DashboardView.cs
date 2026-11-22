using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hora.Core.Aggregation;
using Hora.Core.Presentation;
using Hora.Storage.Models;
using Hora.UI.Controls;

namespace Hora.UI.Views;

/// <summary>Today at a glance: total time, application ranking, and browser domains.</summary>
public sealed class DashboardView : PageBase
{
    private readonly Action<string> _navigate;
    private readonly TextBlock _totalValue = Ui.ValueText("0m", "DisplayText");
    private readonly TextBlock _activeValue = Ui.ValueText("0m");
    private readonly TextBlock _appsValue = Ui.ValueText("0");
    private readonly TextBlock _domainsValue = Ui.ValueText("0");
    private readonly TextBlock _comparison = Ui.Text("dashboardTrackingNote", "MutedText");
    private readonly TextBlock _weekCaption = Ui.ValueText("0m", "MutedText");
    private readonly StackPanel _appList = new();
    private readonly StackPanel _domainList = new();
    private readonly StackPanel _weeklyList = new();
    private readonly Border _emptyCard;
    private readonly DonutChart _donut = new() { Width = 168, Height = 168, Thickness = 18 };
    private readonly StackPanel _legend = new();
    private bool _loading;

    public DashboardView(AppContext context, Action<string> navigate)
        : base(context)
    {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));

        var root = new StackPanel();
        root.Children.Add(Header("dashboardTitle", "dashboardSubtitle"));

        var kpiRow = Columns(
            (1.3, Ui.Card(BuildHero())),
            (1, Ui.Card(BuildKpis())),
            (1, Ui.Card(BuildChart())));
        root.Children.Add(kpiRow);

        _emptyCard = Ui.Card(BuildEmptyState());
        _emptyCard.Margin = new Thickness(0, 14, 0, 0);
        _emptyCard.Visibility = Visibility.Collapsed;
        root.Children.Add(_emptyCard);

        var lists = Columns(
            (1, Ui.Card(BuildAppSection())),
            (1, Ui.Card(BuildDomainSection())));
        lists.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(lists);

        var weekly = Ui.Card(BuildWeeklySection());
        weekly.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(weekly);

        Content = root;
        RefreshData();
    }

    private FrameworkElement BuildHero()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("dashboardTotal", "LabelText"));
        _totalValue.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_totalValue);
        _comparison.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(_comparison);
        stack.Children.Add(Ui.Divider());

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var list = Ui.Secondary("dashboardViewList", () => SetView("List"));
        var chart = Ui.Secondary("dashboardViewChart", () => SetView("Chart"));
        var mini = Ui.Secondary("dashboardViewMini", () => SetView("Mini"));
        list.Margin = new Thickness(0, 0, 8, 0);
        chart.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(list);
        actions.Children.Add(chart);
        actions.Children.Add(mini);
        stack.Children.Add(actions);

        return stack;
    }

    private FrameworkElement BuildKpis()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Kpi("dashboardWeek", _activeValue));
        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.Kpi("dashboardTopApps", _appsValue));
        stack.Children.Add(Ui.Divider());
        stack.Children.Add(Ui.Kpi("dashboardDomains", _domainsValue));
        return stack;
    }

    private FrameworkElement BuildChart()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("dashboardViewChart", "LabelText"));
        _donut.HorizontalAlignment = HorizontalAlignment.Center;
        _donut.Margin = new Thickness(0, 10, 0, 10);
        stack.Children.Add(_donut);
        stack.Children.Add(_legend);
        return stack;
    }

    private FrameworkElement BuildEmptyState()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("dashboardEmpty", "HeadingText"));
        var hint = Ui.Text("dashboardEmptyHint", "MutedText");
        hint.Margin = new Thickness(0, 4, 0, 12);
        stack.Children.Add(hint);
        stack.Children.Add(Ui.Link("historyTitle", () => _navigate("history")));
        return stack;
    }

    private FrameworkElement BuildAppSection()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("dashboardTopApps", "HeadingText"));
        var note = Ui.Text("dashboardTrackingNote", "MutedText");
        note.Margin = new Thickness(0, 3, 0, 12);
        stack.Children.Add(note);
        stack.Children.Add(_appList);
        return stack;
    }

    private FrameworkElement BuildDomainSection()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("dashboardDomains", "HeadingText"));
        var note = Ui.Text("dashboardTrackingNote", "MutedText");
        note.Margin = new Thickness(0, 3, 0, 12);
        stack.Children.Add(note);
        stack.Children.Add(_domainList);
        return stack;
    }

    private FrameworkElement BuildWeeklySection()
    {
        var stack = new StackPanel();
        var heading = Ui.Text("dashboardWeek", "HeadingText");
        stack.Children.Add(heading);
        _weekCaption.Margin = new Thickness(0, 3, 0, 12);
        stack.Children.Add(_weekCaption);
        stack.Children.Add(_weeklyList);
        return stack;
    }

    private void SetView(string view)
    {
        if (Context.Settings.View == view)
        {
            return;
        }

        Context.Settings.View = view;
        Context.SaveSettings();
        Toast(Context.T("dashboardView" + view), ToastKind.Success);
    }

    public override void RefreshData()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        RunSafe(LoadAsync, "load");
    }

    private async Task LoadAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var todayKey = UsageSummary.DateKey(today);
            var weekStart = UsageSummary.DateKey(today.AddDays(-6));
            var rows = await Context.Repository.GetDayAsync(todayKey).ConfigureAwait(true);
            var weekRows = await Context.Repository.GetRangeAsync(weekStart, todayKey).ConfigureAwait(true);

            var apps = UsageSummary.TrackedApplications(rows);
            var domains = UsageSummary.ByDomain(rows);
            var total = UsageSummary.Total(rows) + Context.Engine.PendingActiveSeconds;

            _totalValue.Text = DurationFormatter.Format(total);
            _appsValue.Text = apps.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
            _domainsValue.Text = domains.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

            var weekTotal = UsageSummary.Total(weekRows) + Context.Engine.PendingActiveSeconds;
            _activeValue.Text = DurationFormatter.Format(weekTotal);
            _weekCaption.Text = Context.T("weekTotal") + ": " + DurationFormatter.Format(weekTotal);
            _comparison.Text = Context.T("dashboardTrackingNote");

            RenderApps(apps);
            RenderDomains(domains);
            RenderDonut(apps);
            RenderWeekly(weekRows);

            _emptyCard.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            Context.Logger.Warn("Dashboard load failed.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void RenderApps(IReadOnlyList<AppUsage> apps)
    {
        _appList.Children.Clear();
        if (apps.Count == 0)
        {
            _appList.Children.Add(Ui.Text("dashboardEmpty", "MutedText"));
            return;
        }

        var top = apps.Take(8).ToArray();
        var max = Math.Max(1, top.Max(row => row.Seconds));
        for (var index = 0; index < top.Length; index += 1)
        {
            var row = top[index];
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 10, 0),
            };
            badge.SetResourceReference(Border.BackgroundProperty, "BrushAccentFaint");
            badge.Child = new TextBlock
            {
                Text = (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            badge.Child.SetValue(TextBlock.ForegroundProperty, (Brush)FindResource("BrushAccent"));
            grid.Children.Add(badge);

            var middle = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var name = Ui.Literal(row.Label, "BodyStrong");
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            middle.Children.Add(name);
            middle.Children.Add(Ui.Bar(row.Seconds * 100.0 / max, "BrushChart" + ((index % 8) + 1)));
            Grid.SetColumn(middle, 1);
            grid.Children.Add(middle);

            var value = Ui.Literal(DurationFormatter.Format(row.Seconds), "BodyStrong");
            value.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);

            _appList.Children.Add(grid);
        }
    }

    private void RenderDomains(IReadOnlyList<DomainUsage> domains)
    {
        _domainList.Children.Clear();
        if (domains.Count == 0)
        {
            _domainList.Children.Add(Ui.Text("dashboardEmptyHint", "MutedText"));
            return;
        }

        var top = domains.Take(8).ToArray();
        var max = Math.Max(1, top.Max(row => row.Seconds));
        foreach (var row in top)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var middle = new StackPanel();
            var name = Ui.Literal(row.Domain, "BodyStrong");
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            middle.Children.Add(name);
            middle.Children.Add(Ui.Bar(row.Seconds * 100.0 / max, "BrushChart2"));
            grid.Children.Add(middle);

            var value = Ui.Literal(DurationFormatter.Format(row.Seconds), "BodyStrong");
            value.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);

            _domainList.Children.Add(grid);
        }
    }

    private void RenderDonut(IReadOnlyList<AppUsage> apps)
    {
        _legend.Children.Clear();
        var slices = UsageAggregator.Group(
            apps.Select(row => (row.Exe, row.Label, row.Seconds)),
            ChartPalette(),
            6,
            Context.T("commonNone"));
        _donut.Segments = slices
            .Select(slice => new DonutSegment(slice.Label, slice.Seconds, (Color)ColorConverter.ConvertFromString(slice.Color)))
            .ToArray();
        _donut.CenterText = DurationFormatter.Format(apps.Sum(row => row.Seconds));
        _donut.CaptionText = Context.T("commonToday");

        foreach (var slice in slices.Take(6))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(slice.Color)),
                Margin = new Thickness(0, 0, 8, 0),
            };
            row.Children.Add(dot);
            var label = Ui.Literal(slice.Label, "MutedText");
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.MaxWidth = 180;
            row.Children.Add(label);
            row.Children.Add(Ui.Literal(" " + slice.Percent.ToString("0", System.Globalization.CultureInfo.CurrentCulture) + "%", "MicroText"));
            _legend.Children.Add(row);
        }
    }

    private void RenderWeekly(IReadOnlyList<UsageRow> weekRows)
    {
        _weeklyList.Children.Clear();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var byDate = weekRows
            .Where(r => !SystemBuckets.IsSystem(r.Exe))
            .GroupBy(r => r.Date, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Seconds), StringComparer.Ordinal);

        var days = Enumerable.Range(0, 7).Select(offset => today.AddDays(offset - 6)).ToArray();
        var max = Math.Max(1, days.Max(day => byDate.TryGetValue(UsageSummary.DateKey(day), out var value) ? value : 0));
        var grid = new Grid();
        for (var index = 0; index < days.Length; index += 1)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var day = days[index];
            var seconds = byDate.TryGetValue(UsageSummary.DateKey(day), out var value) ? value : 0;
            var column = new StackPanel { Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Bottom };
            var bar = new Border
            {
                Height = Math.Max(4, 96 * seconds / max),
                CornerRadius = new CornerRadius(5),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            bar.SetResourceReference(Border.BackgroundProperty, index == days.Length - 1 ? "BrushAccent" : "BrushAccentSoft");
            column.Children.Add(bar);
            var label = Ui.Literal(DurationFormatter.Format(seconds), "MicroText");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 6, 0, 2);
            column.Children.Add(label);
            var name = Ui.Literal(DurationFormatter.FormatDay(day, Context.Settings.Language), "MicroText");
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            column.Children.Add(name);
            Grid.SetColumn(column, index);
            grid.Children.Add(column);
        }

        _weeklyList.Children.Add(grid);
    }

    private static string[] ChartPalette() => new[]
    {
        ThemeBrush("BrushChart1"), ThemeBrush("BrushChart2"), ThemeBrush("BrushChart3"), ThemeBrush("BrushChart4"),
        ThemeBrush("BrushChart5"), ThemeBrush("BrushChart6"), ThemeBrush("BrushChart7"), ThemeBrush("BrushChart8"),
    };

    private static string ThemeBrush(string key)
    {
        var brush = Application.Current.TryFindResource(key) as SolidColorBrush;
        return brush is null ? "#7C8CF8" : $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
    }
}
