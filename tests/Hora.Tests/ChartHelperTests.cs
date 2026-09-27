using Hora.Core.Presentation;

namespace Hora.Tests;

public sealed class ChartHelperTests
{
    private static readonly string[] Palette = { "#111111", "#222222", "#333333" };

    [Fact]
    public void GroupSmall_MergesTinySlicesIntoOther()
    {
        var rows = new List<(string Label, string IconKey, long Seconds)>
        {
            ("A", "a", 190),
            ("E", "e", 4),
            ("B", "b", 3),
            ("C", "c", 2),
            ("D", "d", 1),
        };

        var slices = ChartHelper.GroupSmall(rows, Palette);

        Assert.Equal(3, slices.Count);
        Assert.Equal("A", slices[0].Label);
        Assert.Equal(190, slices[0].Seconds);
        Assert.Equal("E", slices[1].Label);
        Assert.Equal(ChartHelper.OtherLabel, slices[2].Label);
        Assert.Equal(6, slices[2].Seconds);
        Assert.Equal(ChartHelper.OtherColor, slices[2].Color);
        Assert.Equal(string.Empty, slices[2].IconKey);
    }

    [Fact]
    public void GroupSmall_KeepsAllWhenAboveThreshold()
    {
        var rows = new List<(string Label, string IconKey, long Seconds)>
        {
            ("A", "a", 50),
            ("B", "b", 30),
            ("C", "c", 20),
        };

        var slices = ChartHelper.GroupSmall(rows, Palette);

        Assert.Equal(3, slices.Count);
        Assert.DoesNotContain(slices, s => s.Label == ChartHelper.OtherLabel);
        Assert.Equal("#111111", slices[0].Color);
        Assert.Equal("a", slices[0].IconKey);
    }

    [Fact]
    public void GroupSmall_DropsNegligibleOther()
    {
        var rows = new List<(string Label, string IconKey, long Seconds)>
        {
            ("A", "a", 995),
            ("B", "b", 3),
            ("C", "c", 2),
        };

        var slices = ChartHelper.GroupSmall(rows, Palette);

        Assert.DoesNotContain(slices, s => s.Label == ChartHelper.OtherLabel);
        Assert.Single(slices);
    }

    [Fact]
    public void GroupSmall_EmptyReturnsEmpty()
    {
        var slices = ChartHelper.GroupSmall(
            new List<(string Label, string IconKey, long Seconds)>(), Palette);
        Assert.Empty(slices);
    }
}
