using BOW.Core;
using System.Collections.Specialized;
using System.ComponentModel;

namespace BOW.Services;

/// <summary>Persists tab state after changes, before an unexpected exit can lose it.</summary>
public sealed class SessionAutoSaver : IDisposable
{
    private readonly BowStore _store;
    private readonly Action<IEnumerable<BowTab>, Guid?> _save;
    private readonly Action<Action> _dispatch;
    private readonly Timer _timer;
    private int _generation;
    private bool _disposed;

    public SessionAutoSaver(BowStore store, Action<IEnumerable<BowTab>, Guid?>? save = null,
        Action<Action>? dispatch = null)
    {
        _store = store;
        _save = save ?? SessionManager.Save;
        _dispatch = dispatch ?? (action => action());
        _timer = new Timer(_ =>
        {
            var generation = Volatile.Read(ref _generation);
            _dispatch(() =>
            {
                if (!_disposed && generation == Volatile.Read(ref _generation)) Save();
            });
        });
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
        ScheduleSave();
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BowTab.Url) or nameof(BowTab.IsPinned)
            or nameof(BowTab.GroupName) or nameof(BowTab.IsMuted)) ScheduleSave();
    }

    private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BowStore.ActiveTab)) ScheduleSave();
    }

    private void ScheduleSave()
    {
        Interlocked.Increment(ref _generation);
        _timer.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private void Save() => _save(_store.Tabs, _store.ActiveTab?.Id);

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        _store.Tabs.CollectionChanged -= OnTabsChanged;
        _store.PropertyChanged -= OnStoreChanged;
        foreach (var tab in _store.Tabs) tab.PropertyChanged -= OnTabChanged;
        Save();
    }
}
