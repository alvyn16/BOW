using BOW.Core;
using System.Collections.Specialized;
using System.ComponentModel;

namespace BOW.Services;

/// <summary>Persists tab state after changes, before an unexpected exit can lose it.</summary>
public sealed class SessionAutoSaver : IDisposable
{
    private readonly BowStore _store;
    private readonly Action<IEnumerable<BowTab>, Guid?> _save;

    public SessionAutoSaver(BowStore store, Action<IEnumerable<BowTab>, Guid?>? save = null)
    {
        _store = store;
        _save = save ?? SessionManager.Save;
        foreach (var tab in store.Tabs) tab.PropertyChanged += OnTabChanged;
        store.Tabs.CollectionChanged += OnTabsChanged;
        store.PropertyChanged += OnStoreChanged;
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (BowTab tab in e.OldItems) tab.PropertyChanged -= OnTabChanged;
        if (e.NewItems is not null)
            foreach (BowTab tab in e.NewItems) tab.PropertyChanged += OnTabChanged;
        Save();
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BowTab.Url) or nameof(BowTab.IsPinned)
            or nameof(BowTab.GroupName) or nameof(BowTab.IsMuted)) Save();
    }

    private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BowStore.ActiveTab)) Save();
    }

    private void Save() => _save(_store.Tabs, _store.ActiveTab?.Id);

    public void Dispose()
    {
        _store.Tabs.CollectionChanged -= OnTabsChanged;
        _store.PropertyChanged -= OnStoreChanged;
        foreach (var tab in _store.Tabs) tab.PropertyChanged -= OnTabChanged;
    }
}
