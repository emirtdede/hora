using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Hora.Core.Presentation;
using Hora.Storage.Models;

namespace Hora.UI.Views;

/// <summary>Seven day history with a per-day breakdown and browser domains.</summary>
public sealed class HistoryView : PageBase
{
    private readonly StackPanel _weekStrip = new();
    private readonly StackPanel _dayDetail = new();
    private readonly TextBlock _dayTotal = Ui.ValueText("0m");
    private readonly TextBlock _dayDate = Ui.Text("commonToday", "BodyStrong");
    private DateOnly _selected = DateOnly.FromDateTime(DateTime.Now);
    private bool _followToday = true;
    private bool _loading;

    public HistoryView(AppContext context)
        : base(context)
    {
        var root = new StackPanel();
        root.Children.Add(Header("historyTitle", "historySubtitle"));

        var strip = Ui.Card(_weekStrip);
        root.Children.Add(strip);

        var summary = Ui.Card(BuildDaySummary());
        summary.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(summary);

        var detail = Ui.Card(_dayDetail);
        detail.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(detail);

        Content = root;
        RefreshData();
    }

    private FrameworkElement BuildDaySummary()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(Ui.Text("historyDayTotal", "LabelText"));
        _dayTotal.Margin = new Thickness(0, 6, 0, 0);
        left.Children.Add(_dayTotal);
        grid.Children.Add(left);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _dayDate.HorizontalAlignment = HorizontalAlignment.Right;
        right.Children.Add(_dayDate);
        var note = Ui.Text("dashboardTrackingNote", "MutedText");
        note.HorizontalAlignment = HorizontalAlignment.Right;
        note.MaxWidth = 280;
        right.Children.Add(note);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        return grid;
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
            // Left open overnight, the page keeps following the current day until
            // the user deliberately picks an older one.
            if (_followToday)
            {
                _selected = today;
            }

            var from = today.AddDays(-6);
            var rows = await Context.Repository.GetRangeAsync(UsageSummary.DateKey(from), UsageSummary.DateKey(today))
                .ConfigureAwait(true);

            var byDate = rows
                .Where(r => !Hora.Core.Aggregation.SystemBuckets.IsSystem(r.Exe))
                .GroupBy(r => r.Date, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

            RenderStrip(today, byDate, rows);
            RenderDay(byDate);
        }
        catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            Context.Logger.Warn("History load failed.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void RenderStrip(DateOnly today, IReadOnlyDictionary<string, UsageRow[]> byDate, IReadOnlyList<UsageRow> allRows)
    {
        _weekStrip.Children.Clear();
        var heading = Ui.Text("dashboardWeek", "HeadingText");
        _weekStrip.Children.Add(heading);

        var weekTotal = UsageSummary.Total(allRows);
        var totalText = Ui.Literal(Context.T("weekTotal") + ": " + DurationFormatter.Format(weekTotal), "MutedText");
        totalText.Margin = new Thickness(0, 3, 0, 14);
        _weekStrip.Children.Add(totalText);

        var grid = new Grid();
        var days = Enumerable.Range(0, 7).Select(offset => today.AddDays(offset - 6)).ToArray();
        for (var index = 0; index < days.Length; index += 1)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var day = days[index];
            var key = UsageSummary.DateKey(day);
            var seconds = byDate.TryGetValue(key, out var dayRows) ? UsageSummary.Total(dayRows) : 0;
            var button = new Button
            {
                Style = (Style)FindResource("RailRow"),
                Margin = new Thickness(3, 0, 3, 0),
                Padding = new Thickness(10, 10, 10, 10),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Tag = day == _selected ? "active" : null,
            };

            var stack = new StackPanel();
            var label = Ui.Literal(DurationFormatter.FormatDay(day, Context.Settings.Language), "MicroText");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(label);
            var value = Ui.Literal(DurationFormatter.Format(seconds), "BodyStrong");
            value.HorizontalAlignment = HorizontalAlignment.Center;
            value.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(value);
            button.Content = stack;
            button.Click += (_, _) =>
            {
                _selected = day;
                _followToday = day == today;
                RefreshData();
            };
            button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,
                DurationFormatter.FormatDay(day, Context.Settings.Language));
            Grid.SetColumn(button, index);
            grid.Children.Add(button);
        }

        _weekStrip.Children.Add(grid);
    }

    private void RenderDay(IReadOnlyDictionary<string, UsageRow[]> byDate)
    {
        _dayDetail.Children.Clear();
        var key = UsageSummary.DateKey(_selected);
        _dayDate.Text = DurationFormatter.FormatDay(_selected, Context.Settings.Language);
        if (!byDate.TryGetValue(key, out var rows) || rows.Length == 0)
        {
            _dayTotal.Text = DurationFormatter.Format(0);
            var empty = Ui.Text("historyEmpty", "MutedText");
            empty.Margin = new Thickness(0, 8, 0, 0);
            _dayDetail.Children.Add(empty);
            return;
        }

        var apps = UsageSummary.TrackedApplications(rows);
        var domains = UsageSummary.ByDomain(rows);
        _dayTotal.Text = DurationFormatter.Format(apps.Sum(row => row.Seconds));

        _dayDetail.Children.Add(Ui.Text("dashboardTopApps", "HeadingText"));
        var list = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var max = Math.Max(1, apps.Count == 0 ? 1 : apps.Max(row => row.Seconds));
        foreach (var app in apps.Take(12))
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            var name = Ui.Literal(app.Label, "BodyStrong");
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            left.Children.Add(name);
            left.Children.Add(Ui.Bar(app.Seconds * 100.0 / max, "BrushChart1"));
            grid.Children.Add(left);
            var value = Ui.Literal(DurationFormatter.Format(app.Seconds), "BodyStrong");
            value.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
            list.Children.Add(grid);
        }

        _dayDetail.Children.Add(list);

        if (domains.Count > 0)
        {
            _dayDetail.Children.Add(Ui.Divider());
            _dayDetail.Children.Add(Ui.Text("dashboardDomains", "HeadingText"));
            var domainList = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            foreach (var domain in domains.Take(10))
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = Ui.Literal(domain.Domain, "BodyStrong");
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                row.Children.Add(name);
                var value = Ui.Literal(DurationFormatter.Format(domain.Seconds), "MutedText");
                Grid.SetColumn(value, 1);
                row.Children.Add(value);
                domainList.Children.Add(row);
            }

            _dayDetail.Children.Add(domainList);
        }

        var range = Ui.Literal(
            apps.Count.ToString(CultureInfo.CurrentCulture) + " / " + domains.Count.ToString(CultureInfo.CurrentCulture),
            "MicroText");
        range.Margin = new Thickness(0, 12, 0, 0);
        _dayDetail.Children.Add(range);
    }
}
