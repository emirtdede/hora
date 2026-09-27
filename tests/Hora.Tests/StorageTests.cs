using Hora.Storage.Db;

namespace Hora.Tests;

public sealed class StorageTests
{
    [Fact]
    public async Task Repository_RoundTrip()
    {
        var db = Path.Combine(Path.GetTempPath(), $"hora-{Guid.NewGuid():N}.db");
        var repo = new UsageRepository(db);
        await repo.InitializeAsync();

        await repo.AddSecondsAsync("2026-09-27", "chrome", "youtube.com", 60);
        await repo.AddSecondsAsync("2026-09-27", "chrome", "youtube.com", 60);
        await repo.AddSecondsAsync("2026-09-27", "code", "", 30);

        var day = await repo.GetDayAsync("2026-09-27");
        Assert.Equal(2, day.Count);
        Assert.Equal(120, day.First(r => r.Exe == "chrome").Seconds);

        var range = await repo.GetRangeAsync("2026-09-21", "2026-09-27");
        Assert.Equal(150, range.Sum(r => r.Seconds));
    }
}
