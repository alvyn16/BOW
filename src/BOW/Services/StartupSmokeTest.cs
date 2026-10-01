using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace BOW.Services;

/// <summary>Checks packaged resources and WebView2 without opening the user's browser profile.</summary>
internal static class StartupSmokeTest
{
    public static async Task RunAsync(Application app, string reportPath)
    {
        Window? window = null;
        WebView2? webView = null;
        var success = false;
        string? runtimeVersion = null;
        string? error = null;
        try
        {
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null,
                Path.Combine(Path.GetDirectoryName(reportPath)!, "webview-profile"), null);
            webView = new WebView2();
            window = new Window { Title = "BOW startup check", Content = webView };
            window.Activate();
            await webView.EnsureCoreWebView2Async(environment);
            var core = webView.CoreWebView2 ?? throw new InvalidOperationException("WebView2 did not initialize.");
            runtimeVersion = core.Environment.BrowserVersionString;
            var navigation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args) =>
                navigation.TrySetResult(args.IsSuccess);
            core.NavigationCompleted += OnCompleted;
            try
            {
                core.NavigateToString("<!doctype html><html><body><p id='result'>BOW startup OK</p></body></html>");
                if (!await navigation.Task.WaitAsync(TimeSpan.FromSeconds(30)))
                    throw new InvalidOperationException("The startup test page failed to load.");
                var result = await core.ExecuteScriptAsync("document.getElementById('result').textContent");
                success = JsonSerializer.Deserialize<string>(result) == "BOW startup OK";
                if (!success) error = "The startup test page returned unexpected content.";
            }
            finally { core.NavigationCompleted -= OnCompleted; }
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            Environment.ExitCode = success ? 0 : 1;
            try { WriteReport(reportPath, success, runtimeVersion, error); }
            finally
            {
                webView?.Close();
                window?.Close();
                app.Exit();
            }
        }
    }

    internal static void WriteReport(string path, bool success, string? runtimeVersion, string? error)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            success, runtimeVersion, error,
            version = typeof(App).Assembly.GetName().Version?.ToString()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
