using System.Globalization;
using Hora.Storage.Db;
using Hora.Storage.Models;
using Hora.Storage.Paths;

// Development helper for the screenshot pipeline. It writes one week of
// realistic demo rows into the current profile database so the captured pages
// show a populated product. It never runs as part of the application.
var repository = new UsageRepository(HoraPaths.DbPath);
await repository.InitializeAsync();

var apps = new Dictionary<string, int>(StringComparer.Ordinal)
{
    ["chrome.exe"] = 7200,
    ["Code.exe"] = 5400,
    ["WINWORD.EXE"] = 2400,
    ["explorer.exe"] = 900,
    ["Teams.exe"] = 1800,
    ["Spotify.exe"] = 1200,
    ["notepad.exe"] = 480,
};

var domains = new[] { "github.com", "learn.microsoft.com", "youtube.com", "stackoverflow.com", "figma.com" };
var random = new Random(20261122);
var rows = new List<UsageRow>();

for (var offset = 6; offset >= 0; offset -= 1)
{
    var date = DateTime.Today.AddDays(-offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    var workday = offset % 7 is not (5 or 6);
    var scale = workday ? 1.0 : 0.35;

    foreach (var (app, baseSeconds) in apps)
    {
        var seconds = (long)(baseSeconds * scale * (0.75 + (random.NextDouble() * 0.5)));
        rows.Add(new UsageRow(date, app, string.Empty, seconds));
    }

    foreach (var domain in domains)
    {
        var seconds = (long)(random.Next(300, 2400) * (workday ? 1.0 : 0.4));
        rows.Add(new UsageRow(date, "chrome.exe", domain, seconds));
    }

    rows.Add(new UsageRow(date, "idle", string.Empty, (long)(2400 * (workday ? 1.0 : 0.5))));
    rows.Add(new UsageRow(date, "locked", string.Empty, 900));
    if (workday)
    {
        rows.Add(new UsageRow(date, "meeting", string.Empty, 1800));
    }
}

await repository.ReplaceAllAsync(rows);
Console.WriteLine($"Seeded {rows.Count} demo rows into {HoraPaths.DbPath}");
