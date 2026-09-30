using BOW.Core;
using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BOW.UI.TabStrip;

internal static class TabMenuBuilder
{
    public static MenuFlyout CreateTabMenu(BowStore store, BowTab tab)
    {
        var menu = new MenuFlyout();
        var duplicate = new MenuFlyoutItem { Text = "Duplicate tab" };
        duplicate.Click += (_, _) => store.DuplicateTab(tab.Id);
        menu.Items.Add(duplicate);

        var mute = new MenuFlyoutItem { Text = tab.IsMuted ? "Unmute tab" : "Mute tab" };
        mute.Click += (_, _) => tab.IsMuted = !tab.IsMuted;
        menu.Items.Add(mute);

        var pin = new MenuFlyoutItem { Text = tab.IsPinned ? "Unpin tab" : "Pin tab" };
        pin.Click += (_, _) => tab.IsPinned = !tab.IsPinned;
        menu.Items.Add(pin);

        var sleep = new MenuFlyoutItem
        {
            Text = "Sleep tab now",
            IsEnabled = tab != store.ActiveTab && !tab.IsSleeping && !tab.IsLoading
        };
        sleep.Click += async (_, _) =>
        {
            if (App.MainWindow is { } window) await window.SleepTabNowAsync(tab);
        };
        menu.Items.Add(sleep);

        if (Uri.TryCreate(tab.Url, UriKind.Absolute, out var address)
            && address.Scheme is "http" or "https")
        {
            var host = address.Host;
            var excluded = TabSleepPolicy.IsExcluded(tab.Url, store.Settings.TabSleepExcludedHosts);
            var exception = new MenuFlyoutItem
            {
                Text = excluded ? "Allow sleeping this site" : "Never sleep this site"
            };
            exception.Click += (_, _) =>
            {
                store.Settings.TabSleepExcludedHosts ??= [];
                store.Settings.TabSleepExcludedHosts.RemoveAll(item =>
                    string.Equals(item, host, StringComparison.OrdinalIgnoreCase));
                if (!excluded) store.Settings.TabSleepExcludedHosts.Add(host);
                SettingsService.Save(store.Settings);
            };
            menu.Items.Add(exception);
        }

        var groups = new MenuFlyoutSubItem { Text = "Move to group" };
        var noGroup = new MenuFlyoutItem { Text = "No group" };
        noGroup.Click += (_, _) => store.SetTabGroup(tab.Id, null);
        groups.Items.Add(noGroup);
        foreach (var name in store.GroupNames)
        {
            var group = new MenuFlyoutItem { Text = name };
            group.Click += (_, _) => store.SetTabGroup(tab.Id, name);
            groups.Items.Add(group);
        }
        groups.Items.Add(new MenuFlyoutSeparator());
        var newGroup = new MenuFlyoutItem { Text = "New group…" };
        newGroup.Click += async (_, _) =>
        {
            var name = await PromptForGroupNameAsync("New tab group");
            if (name is not null) store.SetTabGroup(tab.Id, name);
        };
        groups.Items.Add(newGroup);
        menu.Items.Add(groups);

        var canPair = store.CanSplitWithTab(tab.Id);
        var split = new MenuFlyoutItem
        {
            Text = tab.IsSplitPartner ? "Leave split view" : canPair ? "Open beside current tab" : "Split view"
        };
        split.Click += (_, _) =>
        {
            if (tab.IsSplitPartner) store.JoinSplitTab(tab.Id);
            else if (canPair) store.SplitWithTab(tab.Id, false);
            else { store.SetActiveTab(tab.Id); store.SplitActiveTab(); }
        };
        menu.Items.Add(split);
        menu.Items.Add(new MenuFlyoutSeparator());

        var close = new MenuFlyoutItem { Text = "Close tab" };
        close.Click += (_, _) => store.CloseTab(tab.Id);
        menu.Items.Add(close);
        return menu;
    }

    public static MenuFlyout CreateTabsMenu(BowStore store)
    {
        var menu = new MenuFlyout();
        var reopen = new MenuFlyoutSubItem { Text = "Recently closed tabs" };
        if (store.RecentlyClosedTabs.Count == 0)
            reopen.Items.Add(new MenuFlyoutItem { Text = "No recently closed tabs", IsEnabled = false });
        else
        {
            foreach (var entry in store.RecentlyClosedTabs.Take(12))
            {
                var title = string.IsNullOrWhiteSpace(entry.Title) || entry.Title == "New Tab"
                    ? entry.Url : entry.Title;
                if (title.Length > 42) title = title[..39] + "…";
                var item = new MenuFlyoutItem { Text = title };
                item.Click += (_, _) => store.ReopenClosedTab(entry.Id);
                reopen.Items.Add(item);
            }
        }
        menu.Items.Add(reopen);
        return menu;
    }

    public static MenuFlyout CreateGroupMenu(BowStore store, string name)
    {
        var menu = new MenuFlyout();
        var rename = new MenuFlyoutItem { Text = "Rename group" };
        rename.Click += async (_, _) =>
        {
            var updated = await PromptForGroupNameAsync("Rename tab group", name);
            if (updated is not null) store.RenameGroup(name, updated);
        };
        menu.Items.Add(rename);
        var remove = new MenuFlyoutItem { Text = "Remove group" };
        remove.Click += (_, _) => store.RemoveGroup(name);
        menu.Items.Add(remove);
        return menu;
    }

    private static async Task<string?> PromptForGroupNameAsync(string title, string initial = "")
    {
        if (App.MainWindow?.RootGrid.XamlRoot is not { } root) return null;
        var input = new TextBox { Text = initial, PlaceholderText = "Group name", MaxLength = 36 };
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = input,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text)
            ? input.Text.Trim() : null;
    }
}
