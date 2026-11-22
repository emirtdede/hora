using Hora.Core.Input;
using Xunit;

namespace Hora.Tests;

public sealed class HotkeyCatalogTests
{
    [Theory]
    [InlineData("Ctrl+Alt+H")]
    [InlineData("Ctrl+Shift+H")]
    [InlineData("Alt+Shift+F12")]
    [InlineData("Win+Ctrl+1")]
    public void Accepts_well_formed_gestures(string gesture) => Assert.True(HotkeyCatalog.IsValid(gesture));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("Ctrl+H+H")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Alt+F25")]
    [InlineData("Meta+Alt+H")]
    [InlineData("Ctrl+Alt+HH")]
    [InlineData("not a gesture")]
    public void Rejects_malformed_gestures(string? gesture) => Assert.False(HotkeyCatalog.IsValid(gesture));

    [Fact]
    public void Every_preset_is_valid() => Assert.All(HotkeyCatalog.Presets, gesture => Assert.True(HotkeyCatalog.IsValid(gesture)));
}
