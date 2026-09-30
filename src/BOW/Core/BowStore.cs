using BOW.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace BOW.Core;

public sealed record ClosedTabEntry(Guid Id, string Url, string Title,
    bool IsPinned, string? GroupName, bool IsMuted, DateTimeOffset ClosedAt);

/// <summary>
/// Central application state. Single source of truth for all tabs and settings.
/// </summary>
public partial class BowStore : ObservableObject
{
    [ObservableProperty]
    private BowTab? _activeTab;

    public ObservableCollection<BowTab> Tabs { get; } = new();

    public SettingsModel Settings { get; }

    // Stack of recently closed tab URLs for Ctrl+Shift+T
    private readonly Stack<ClosedTabEntry> _closedTabsStack = new();
    public IReadOnlyList<ClosedTabEntry> RecentlyClosedTabs => _closedTabsStack.ToArray();
    public IReadOnlyList<string> GroupNames => Tabs.Select(tab => tab.GroupName)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase).Cast<string>().ToArray();

    public BowStore(SettingsModel? settings = null, IEnumerable<SessionEntry>? session = null)
    {
        Settings = settings ?? SettingsService.Load();

        var entries = session ?? SessionManager.Load();
        BowTab? restoredActiveTab = null;
        foreach (var entry in entries.Where(e => Settings.RestoreSessionOnStart || e.IsPinned))
        {
            var tab = CreateTab(entry.Url);
            tab.IsPinned = entry.IsPinned;
            tab.GroupName = entry.GroupName;
            tab.IsMuted = entry.IsMuted;
            Tabs.Add(tab);
            if (entry.IsActive) restoredActiveTab = tab;
        }

        if (Tabs.Count == 0)
        {
            AddTab("bow:newtab");
        }
        else
        {
            ActiveTab = restoredActiveTab ?? Tabs[0];
        }
    }

    /// <summary>Adds a new tab and makes it active.</summary>
    public BowTab AddTab(string url)
    {
        var tab = CreateTab(url);
        Tabs.Add(tab);
        SetActiveTab(tab.Id);
        return tab;
    }

    /// <summary>Closes the tab with the given ID. Activates neighbor if it was active.</summary>
    public void CloseTab(Guid id)
    {
        var tab = Tabs.FirstOrDefault(t => t.Id == id);
        if (tab is null) return;

        // Save to closed stack (omit newtab pages)
        if (!string.IsNullOrEmpty(tab.Url) && tab.Url != "bow:newtab")
            _closedTabsStack.Push(new ClosedTabEntry(Guid.NewGuid(), tab.Url, tab.Title,
                tab.IsPinned, tab.GroupName, tab.IsMuted, DateTimeOffset.UtcNow));

        var index = Tabs.IndexOf(tab);
        BowTab? splitPartner = null;
        if (tab.SplitPartnerId is Guid partnerId)
        {
            splitPartner = Tabs.FirstOrDefault(t => t.Id == partnerId);
            if (splitPartner is not null)
            {
                splitPartner.IsSplitPartner = false;
                splitPartner.SplitPartnerId = null;
            }
        }
        Tabs.Remove(tab);

        if (ActiveTab?.Id == id)
        {
            if (Tabs.Count == 0)
            {
                AddTab("bow:newtab");
            }
            else
            {
                SetActiveTab(splitPartner is not null && Tabs.Contains(splitPartner)
                    ? splitPartner.Id : Tabs[Math.Max(0, index - 1)].Id);
            }
        }
    }

    /// <summary>Makes the tab with the given ID the active tab.</summary>
    public void SetActiveTab(Guid id)
    {
        var tab = Tabs.FirstOrDefault(t => t.Id == id);
        if (tab is null) return;
        tab.LastActiveAt = DateTimeOffset.UtcNow;
        tab.IsSleeping = false;
        ActiveTab = tab;
    }

    /// <summary>Reopens the last closed tab.</summary>
    public void ReopenLastClosedTab()
    {
        if (_closedTabsStack.Count == 0) return;
        ReopenClosedTab(_closedTabsStack.Peek().Id);
    }

    public void ClearRecentlyClosedSince(DateTimeOffset? since)
    {
        var remaining = _closedTabsStack.Where(entry =>
            since is not null && entry.ClosedAt < since.Value).Reverse().ToArray();
        _closedTabsStack.Clear();
        foreach (var entry in remaining) _closedTabsStack.Push(entry);
    }

    public void ReopenClosedTab(Guid id)
    {
        var entry = _closedTabsStack.FirstOrDefault(item => item.Id == id);
        if (entry is null) return;
        var remaining = _closedTabsStack.Where(item => item.Id != id).Reverse().ToArray();
        _closedTabsStack.Clear();
        foreach (var item in remaining) _closedTabsStack.Push(item);
        var tab = AddTab(entry.Url);
        tab.IsPinned = entry.IsPinned;
        tab.GroupName = entry.GroupName;
        tab.IsMuted = entry.IsMuted;
    }

    public BowTab? DuplicateTab(Guid id)
    {
        var source = Tabs.FirstOrDefault(tab => tab.Id == id);
        if (source is null) return null;
        var duplicate = CreateTab(source.Url);
        duplicate.IsPinned = source.IsPinned;
        duplicate.GroupName = source.GroupName;
        duplicate.IsMuted = source.IsMuted;
        Tabs.Insert(Tabs.IndexOf(source) + 1, duplicate);
        SetActiveTab(duplicate.Id);
        return duplicate;
    }

    public void MoveTab(Guid sourceId, Guid targetId, bool after = false)
    {
        var source = Tabs.FirstOrDefault(tab => tab.Id == sourceId);
        var target = Tabs.FirstOrDefault(tab => tab.Id == targetId);
        if (source is null || target is null || source == target) return;
        source.GroupName = target.GroupName;
        var sourceIndex = Tabs.IndexOf(source);
        var targetIndex = Tabs.IndexOf(target) + (after ? 1 : 0);
        if (sourceIndex < targetIndex) targetIndex--;
        Tabs.Move(sourceIndex, targetIndex);
    }

    public void SetTabGroup(Guid id, string? groupName)
    {
        var tab = Tabs.FirstOrDefault(item => item.Id == id);
        if (tab is null) return;
        groupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
        tab.GroupName = groupName;
        if (groupName is null) return;
        var lastMember = Tabs.LastOrDefault(item => item != tab
            && string.Equals(item.GroupName, groupName, StringComparison.OrdinalIgnoreCase));
        if (lastMember is not null)
        {
            var targetIndex = Tabs.IndexOf(lastMember);
            if (Tabs.IndexOf(tab) < targetIndex) targetIndex--;
            Tabs.Move(Tabs.IndexOf(tab), targetIndex + 1);
        }
    }

    public void RenameGroup(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        foreach (var tab in Tabs.Where(tab => string.Equals(tab.GroupName, oldName,
            StringComparison.OrdinalIgnoreCase))) tab.GroupName = newName.Trim();
    }

    public void RemoveGroup(string name)
    {
        foreach (var tab in Tabs.Where(tab => string.Equals(tab.GroupName, name,
            StringComparison.OrdinalIgnoreCase))) tab.GroupName = null;
    }

    /// <summary>Splits the active tab — creates a partner beside it.</summary>
    public void SplitActiveTab()
    {
        var primary = ActiveTab;
        if (primary is null || primary.IsSplitPartner) return;

        var partner = CreateTab(primary.Url);
        primary.IsSplitPartner = true;
        primary.SplitPartnerId = partner.Id;
        partner.IsSplitPartner = true;
        partner.SplitPartnerId = primary.Id;
        Tabs.Add(partner);
    }

    /// <summary>Places an existing tab beside the active tab without duplicating either page.</summary>
    public bool CanSplitWithTab(Guid draggedId)
    {
        var current = ActiveTab;
        var dragged = Tabs.FirstOrDefault(tab => tab.Id == draggedId);
        return current is not null && dragged is not null && current != dragged
            && !current.IsSplitPartner && !dragged.IsSplitPartner
            && !string.IsNullOrEmpty(current.Url) && current.Url != "bow:newtab"
            && !string.IsNullOrEmpty(dragged.Url) && dragged.Url != "bow:newtab";
    }

    public bool SplitWithTab(Guid draggedId, bool placeOnLeft)
    {
        if (!CanSplitWithTab(draggedId)) return false;
        var current = ActiveTab!;
        var dragged = Tabs.First(tab => tab.Id == draggedId);

        current.IsSplitPartner = true;
        current.SplitPartnerId = dragged.Id;
        dragged.IsSplitPartner = true;
        dragged.SplitPartnerId = current.Id;
        if (placeOnLeft) SetActiveTab(dragged.Id);
        return true;
    }

    /// <summary>Closes a split layout while keeping both tabs open.</summary>
    public void JoinSplitTab(Guid primaryId)
    {
        var primary = Tabs.FirstOrDefault(t => t.Id == primaryId);
        if (primary?.SplitPartnerId is Guid partnerId)
        {
            var partner = Tabs.FirstOrDefault(t => t.Id == partnerId);
            if (partner is not null)
            {
                partner.IsSplitPartner = false;
                partner.SplitPartnerId = null;
            }
        }
        if (primary is not null)
        {
            primary.IsSplitPartner = false;
            primary.SplitPartnerId = null;
        }
    }

    private static BowTab CreateTab(string url) => new() { Url = url };
}
