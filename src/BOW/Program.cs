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
                new App(smokeReport);
            });
        }
        catch (Exception ex) when (smokeReport is not null)
        {
            Services.StartupSmokeTest.WriteReport(smokeReport, false, null, ex.ToString());
            Environment.ExitCode = 1;
        }
    }
}
