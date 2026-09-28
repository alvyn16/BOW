using BOW.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BOW.Services;

/// <summary>
/// Monitors idle tabs and puts them to sleep after the configured interval.
/// Sleep is triggered by setting BowTab.IsSleeping = true, which WebViewHost observes.
/// </summary>
public sealed class TabSleepService
{
    private readonly BowStore _store;
    private readonly Func<BowTab, Task<bool>> _sleepTab;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };

    public TabSleepService(BowStore store, Func<BowTab, Task<bool>> sleepTab)
    {
        _store = store;
        _sleepTab = sleepTab;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private async void OnTick(object? sender, object e)
    {
        var thresholdMinutes = _store.Settings.TabSleepMinutes;
        if (thresholdMinutes <= 0) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var tab in _store.Tabs.ToList())
        {
            // Never sleep the active tab
            if (tab.Id == _store.ActiveTab?.Id) continue;
            if (tab.IsSleeping) continue;
            if ((now - tab.LastActiveAt).TotalMinutes < thresholdMinutes) continue;

            try
            {
                if (await _sleepTab(tab) && tab != _store.ActiveTab)
                    tab.IsSleeping = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not sleep tab: {ex}");
            }
        }
    }
}
