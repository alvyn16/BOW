using BOW.Core;

namespace BOW.Tests;

public class ShortcutTests
{
    [Fact]
    public void ChangedShortcutPersistsAndConflictsAreRejected()
    {
        var settings = new SettingsModel();
        Assert.True(ShortcutCatalog.TrySetBinding(settings, "address", "Ctrl+K", out _));
        Assert.Equal("Ctrl+K", ShortcutCatalog.GetBinding(settings, ShortcutCatalog.Commands[0]));
        Assert.False(ShortcutCatalog.TrySetBinding(settings, "new-tab", "Ctrl+K", out var error));
        Assert.Contains("Search or enter address", error);
        Assert.Equal("Ctrl+T", ShortcutCatalog.GetBinding(settings, ShortcutCatalog.Commands[1]));

        var restored = System.Text.Json.JsonSerializer.Deserialize<SettingsModel>(
            System.Text.Json.JsonSerializer.Serialize(settings))!;
        Assert.Equal("Ctrl+K", ShortcutCatalog.GetBinding(restored, ShortcutCatalog.Commands[0]));
    }

    [Theory]
    [InlineData("T")]
    [InlineData("Shift+T")]
    [InlineData("Ctrl+Ctrl+T")]
    [InlineData("Ctrl+Unknown")]
    public void UnsafeOrInvalidBindingsAreRejected(string binding) =>
        Assert.False(ShortcutCatalog.TryParse(binding, out _));
}
