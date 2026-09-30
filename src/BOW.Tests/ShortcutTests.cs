using BOW.Core;

namespace BOW.Tests;

public class ShortcutTests
{
    [Fact]
    public void ChangedShortcutPersistsAndConflictsAreRejected()
    {
        var settings = new SettingsModel();
        Assert.True(ShortcutCatalog.TrySetBinding(settings, "address", "Ctrl+K", out _));
        Assert.Equal("Ctrl+K", ShortcutCatalog.GetBinding(settings,
            ShortcutCatalog.Commands.Single(command => command.Id == "address")));
        Assert.False(ShortcutCatalog.TrySetBinding(settings, "new-tab", "Ctrl+K", out var error));
        Assert.Contains("Search or enter address", error);
        Assert.Equal("Ctrl+T", ShortcutCatalog.GetBinding(settings,
            ShortcutCatalog.Commands.Single(command => command.Id == "new-tab")));

        var restored = System.Text.Json.JsonSerializer.Deserialize<SettingsModel>(
            System.Text.Json.JsonSerializer.Serialize(settings))!;
        Assert.Equal("Ctrl+K", ShortcutCatalog.GetBinding(restored,
            ShortcutCatalog.Commands.Single(command => command.Id == "address")));
    }

    [Fact]
    public void HistoryShortcuts_DefaultToAltArrows()
    {
        var settings = new SettingsModel();
        var back = ShortcutCatalog.Commands.Single(command => command.Id == "back");
        var forward = ShortcutCatalog.Commands.Single(command => command.Id == "forward");

        Assert.Equal("Alt+Left", ShortcutCatalog.GetBinding(settings, back));
        Assert.Equal("Alt+Right", ShortcutCatalog.GetBinding(settings, forward));
        Assert.True(ShortcutCatalog.TryParse("alt+left", out var left));
        Assert.Equal("Alt+Left", left.ToString());
        Assert.False(ShortcutCatalog.TrySetBinding(settings, forward.Id, "Alt+Left", out _));
    }

    [Fact]
    public void LeaveSplitShortcut_IsCustomizableAndDistinctFromCloseTab()
    {
        var settings = new SettingsModel();
        var leaveSplit = ShortcutCatalog.Commands.Single(command => command.Id == "leave-split");
        Assert.Equal("Ctrl+Shift+2", ShortcutCatalog.GetBinding(settings, leaveSplit));
        Assert.True(ShortcutCatalog.TrySetBinding(settings, leaveSplit.Id, "Ctrl+Alt+S", out _));
        Assert.False(ShortcutCatalog.TrySetBinding(settings, leaveSplit.Id, "Ctrl+W", out _));
    }

    [Fact]
    public void MaximizeShortcut_DefaultsToCtrlYAndCanBeChanged()
    {
        var settings = new SettingsModel();
        var fullScreen = ShortcutCatalog.Commands.Single(command => command.Id == "full-screen");
        Assert.Equal("Maximize or restore window", fullScreen.Label);
        Assert.Equal("Ctrl+Y", ShortcutCatalog.GetBinding(settings, fullScreen));
        Assert.True(ShortcutCatalog.TrySetBinding(settings, fullScreen.Id, "Ctrl+F11", out _));
        Assert.False(ShortcutCatalog.TrySetBinding(settings, fullScreen.Id, "Ctrl+W", out _));
    }

    [Theory]
    [InlineData("T")]
    [InlineData("Shift+T")]
    [InlineData("Ctrl+Ctrl+T")]
    [InlineData("Ctrl+Unknown")]
    public void UnsafeOrInvalidBindingsAreRejected(string binding) =>
        Assert.False(ShortcutCatalog.TryParse(binding, out _));
}
