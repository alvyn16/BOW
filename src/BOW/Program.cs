using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Threading;

namespace BOW;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var benchmarkReport = args.Length == 2 && args[0] == "--benchmark"
            ? System.IO.Path.GetFullPath(args[1]) : null;
        if (benchmarkReport is not null)
        {
            var profile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(benchmarkReport)!,
                "profile-" + Guid.NewGuid());
            Services.BrowserData.DirectoryPath = profile;
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", System.IO.Path.Combine(profile, "webview"));
        }
        if (args.Length == 1 && args[0] == "--install-webview2")
        {
            try
            {
                Services.WebViewRuntimeInstaller.InstallAsync().GetAwaiter().GetResult();
                Console.WriteLine("WebView2 setup completed.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Environment.ExitCode = 1;
            }
            return;
        }
        var smokeReport = args.Length == 2 && args[0] == "--smoke-test"
            ? System.IO.Path.GetFullPath(args[1]) : null;
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                new App(smokeReport, benchmarkReport, started);
            });
        }
        catch (Exception ex) when (smokeReport is not null || benchmarkReport is not null)
        {
            Services.StartupSmokeTest.WriteReport((smokeReport ?? benchmarkReport)!, false, null, ex.ToString());
            Environment.ExitCode = 1;
        }
    }
}
