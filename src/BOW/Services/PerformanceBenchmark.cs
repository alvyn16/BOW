using BOW.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text.Json;

namespace BOW.Services;

internal static class PerformanceBenchmark
{
    internal static async Task RunAsync(Application app, MainWindow window, string report,
        Stopwatch started)
    {
        var stages = new List<object>();
        var switches = new List<double>();
        double? startupMs = null;
        string? runtime = null;
        string? error = null;
        try
        {
            Directory.CreateDirectory(BrowserData.DirectoryPath);
            var page = BrowserData.FilePath("workload.html");
            File.WriteAllText(page, "<!doctype html><title>BOW benchmark</title><style>body{font:16px Arial}p{padding:4px;border-bottom:1px solid #ddd}</style>" +
                string.Concat(Enumerable.Range(0, 1000).Select(i => $"<p>Benchmark row {i}: fixed offline workload</p>")));
            var url = new Uri(page).AbsoluteUri;
            var store = App.Store;
            var first = store.ActiveTab!;
            first.Url = url;
            await ReadyAsync(window, first);
            startupMs = started.Elapsed.TotalMilliseconds;
            var environment = window.BenchmarkCore(first.Id)!.Environment;
            runtime = environment.BrowserVersionString;
            foreach (var count in new[] { 1, 10, 30 })
            {
                while (store.Tabs.Count < count)
                {
                    var tab = store.AddTab(url + "#tab" + store.Tabs.Count);
                    await ReadyAsync(window, tab);
                }
                await Task.Delay(1500);
                stages.Add(new { tabs = count, phase = "loaded", memory = SampleMemory(environment) });
            }
            // Warm switches through the real store/content layout, ending at a rendered frame.
            for (var i = 0; i < 30; i++)
            {
                var tab = store.Tabs[i];
                var timer = Stopwatch.StartNew();
                store.SetActiveTab(tab.Id);
                await RenderAsync();
                var text = await window.BenchmarkCore(tab.Id)!.ExecuteScriptAsync("document.title");
                if (text != "\"BOW benchmark\"") throw new InvalidOperationException("Switch target was not ready.");
                switches.Add(timer.Elapsed.TotalMilliseconds);
            }
            var beforeSleep = SampleMemory(environment);
            foreach (var tab in store.Tabs.Where(t => t != store.ActiveTab).ToList())
                await window.SleepTabNowAsync(tab);
            if (store.Tabs.Count(t => t.IsSleeping) != 29)
                throw new InvalidOperationException("Not all background tabs went to sleep.");
            await Task.Delay(3000);
            var awakeCore = window.BenchmarkCore(store.ActiveTab!.Id)
                ?? throw new InvalidOperationException("Active tab was unloaded while sleeping background tabs.");
            if (await awakeCore.ExecuteScriptAsync("document.title") != "\"BOW benchmark\"")
                throw new InvalidOperationException("Active tab was not responsive after sleeping background tabs.");
            var afterSleep = SampleMemory(awakeCore.Environment);
            stages.Add(new { tabs = 30, phase = "29-background-tabs-sleeping", memory = afterSleep,
                beforeSleepMemory = beforeSleep,
                reclaimedPrivateBytes = beforeSleep.PrivateBytes - afterSleep.PrivateBytes });
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            Environment.ExitCode = error is null ? 0 : 1;
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                success = error is null, error, startupMs, runtime,
                version = typeof(App).Assembly.GetName().Version?.ToString(),
                os = Environment.OSVersion.ToString(), processors = Environment.ProcessorCount,
                isolatedProfile = BrowserData.DirectoryPath, workloadRows = 1000,
                stages, switchSamplesMs = switches,
                switchMedianMs = Percentile(switches, .5), switchP95Ms = Percentile(switches, .95)
            }, new JsonSerializerOptions { WriteIndented = true }));
            window.Close();
            app.Exit();
        }
    }

    private static async Task ReadyAsync(MainWindow window, BowTab tab)
    {
        var timeout = Stopwatch.StartNew();
        while (!tab.HasLoadedSuccessfully || tab.IsLoading || window.BenchmarkCore(tab.Id) is null)
        {
            if (tab.NavigationFailed) throw new InvalidOperationException("Benchmark navigation failed.");
            if (timeout.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Tab initialization timed out.");
            await Task.Delay(25);
        }
        await RenderAsync();
    }

    private static async Task RenderAsync()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = 0;
        void Frame(object? sender, object args) { if (++frames >= 2) ready.TrySetResult(); }
        CompositionTarget.Rendering += Frame;
        try { await ready.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { CompositionTarget.Rendering -= Frame; }
    }

    private sealed record MemorySample(long PrivateBytes, long WorkingSetBytes, int Processes);
    private static MemorySample SampleMemory(CoreWebView2Environment environment)
    {
        long privateBytes = 0, workingSet = 0;
        var ids = environment.GetProcessInfos().Select(p => (int)p.ProcessId)
            .Append(Environment.ProcessId).Distinct().ToList();
        if (ids.Count <= 1) throw new InvalidOperationException("Runtime process enumeration returned no browser processes.");
        foreach (var id in ids)
        {
            using var process = Process.GetProcessById(id);
            privateBytes += process.PrivateMemorySize64;
            workingSet += process.WorkingSet64;
        }
        return new(privateBytes, workingSet, ids.Count);
    }

    private static double? Percentile(List<double> values, double quantile) => values.Count == 0
        ? null : values.Order().ElementAt((int)Math.Ceiling(values.Count * quantile) - 1);
}
