using Microsoft.Win32;
using System.Diagnostics;

namespace BOW.Services;

internal static class WebViewRuntimeInstaller
{
    internal static string InstallerPath => Path.Combine(AppContext.BaseDirectory,
        "runtime", "MicrosoftEdgeWebview2Setup.exe");

    internal static bool IsInstalled()
    {
        const string client = @"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        foreach (var path in new[]
        {
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\" + client,
            @"HKEY_CURRENT_USER\Software\" + client
        })
        {
            if (Registry.GetValue(path, "pv", null) is string value
                && Version.TryParse(value, out var version) && version > new Version(0, 0, 0, 0))
                return true;
        }
        return false;
    }

    internal static async Task InstallAsync()
    {
        // Keep the verified installer open without write/delete sharing until it exits.
        using var file = new FileStream(InstallerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await Task.Run(() => MicrosoftInstallerTrust.Verify(InstallerPath));
        using var process = Process.Start(new ProcessStartInfo(InstallerPath)
        {
            Arguments = "/silent /install", UseShellExecute = false, CreateNoWindow = true
        }) ?? throw new InvalidOperationException("The WebView2 installer could not start.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill();
            throw new TimeoutException("WebView2 installation timed out. Check your connection and try again.");
        }
        if (process.ExitCode != 0 || !IsInstalled())
            throw new InvalidOperationException($"WebView2 installation did not complete (exit {process.ExitCode}).");
    }
}
