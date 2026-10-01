using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.Core.Capturing;
using FlaUI.UIA3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json;

namespace BOW.InteractionTests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Expected BOW executable and report directory.");
        var report = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(report);
        var profile = Path.Combine(report, "profile-" + Guid.NewGuid());
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(profile, "settings.json"), JsonSerializer.Serialize(new
        {
            TabLayout = "Sidebar", TabSleepMinutes = 0, RestoreSessionOnStart = false,
            DownloadFolder = Path.Combine(profile, "downloads"), AskWhereToSaveDownloads = false
        }));
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        using var server = builder.Build();
        server.MapGet("/download", () => Results.File("BOW synthetic download"u8.ToArray(), "text/plain", "bow-test.txt"));
        server.MapGet("/{page}", (string page) => Results.Content(Page(page), "text/html"));
        server.StartAsync().GetAwaiter().GetResult();
        var url = server.Urls.Single();
        using var automation = new UIA3Automation();
        using var app = Application.Launch(Path.GetFullPath(args[0]), "--ui-test-profile \"" + profile + "\"");
        Window? window = null;
        var passed = new List<string>();
        string? error = null;
        try
        {
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))
                ?? throw new InvalidOperationException("BOW window was not found.");
            window.SetForeground();
            Navigate(window, url + "/one");
            ByName(window, "Next").Patterns.Invoke.Pattern.Invoke(); Tab(window, "two");
            Chord(VirtualKeyShort.ALT, VirtualKeyShort.LEFT); Tab(window, "one");
            Chord(VirtualKeyShort.ALT, VirtualKeyShort.RIGHT); Tab(window, "two");
            passed.Add("navigation links and Alt+Left/Right");
            Navigate(window, url + "/one");
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T); Navigate(window, url + "/two");
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T); Navigate(window, url + "/three");
            Click(Tab(window, "one"));
            Wait(() => ActiveTitle(profile) == "one", "selected first tab");
            var second = Tab(window, "two").BoundingRectangle;
            Drag(Center(Tab(window, "three").BoundingRectangle), new Point((int)second.Left + 40, (int)second.Top + 2));
            Wait(() => Tab(window, "three").BoundingRectangle.Top < Tab(window, "two").BoundingRectangle.Top, "tab reorder");
            passed.Add("real pointer tab reordering");
            Drag(Center(Tab(window, "two").BoundingRectangle), Center(Tab(window, "one").BoundingRectangle));
            Wait(() => HasTwoPanes(profile), "drag to split");
            Capture.Element(window).ToFile(Path.Combine(report, "split.png"));
            passed.Add("real pointer drag-to-split");
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_2);
            Wait(() => VisiblePaneCount(profile) == 1, "leave split");
            if (Tabs(window).Length != 3) throw new InvalidOperationException("Leaving split closed a tab.");
            passed.Add("leave split shortcut keeps tabs");
            Click(Tab(window, "one"));
            Drag(Center(Tab(window, "two").BoundingRectangle), Center(Tab(window, "one").BoundingRectangle));
            Click(ByName(window, "Focus two"));
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            Wait(() => Tabs(window).Length == 2 && !Tabs(window).Any(t => t.Name == "Tab: two"), "close focused split pane");
            passed.Add("Ctrl+W removes focused split tab");
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_T); Tab(window, "two");
            passed.Add("reopen closed tab shortcut");
            var original = window.BoundingRectangle;
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => window.BoundingRectangle.Width > original.Width, "maximize");
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => Math.Abs(window.BoundingRectangle.Width - original.Width) < 3, "restore");
            passed.Add("Ctrl+Y maximize and restore");
            Click(ByName(window, "Fullscreen"));
            Wait(() => !Visible(window, "Quick settings"), "page fullscreen chrome hidden");
            Capture.Element(window).ToFile(Path.Combine(report, "fullscreen.png"));
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Wait(() => Visible(window, "Quick settings"), "fullscreen exit");
            passed.Add("page fullscreen and Escape");
            Click(ByName(window, "Download"));
            var file = Path.Combine(profile, "downloads", "bow-test.txt");
            Wait(() => File.Exists(file) && File.ReadAllText(file) == "BOW synthetic download", "download completion");
            passed.Add("actual WebView2 download saves expected bytes");
            Click(ByName(window, "Seed data")); Click(ByName(window, "Read data")); ByName(window, "State present");
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_I);
            ByName(window, "Privacy & browsing", ControlType.Button).AsButton().Invoke();
            ByName(window, "Browsing history").AsCheckBox().IsChecked = false;
            ByName(window, "Cached images and files").AsCheckBox().IsChecked = false;
            ByName(window, "Clear selected data").AsButton().Invoke();
            ByName(window, "Clear data", ControlType.Button).AsButton().Invoke();
            ByName(window, "Selected browsing data cleared.");
            ById(window, "BackToBrowser").AsButton().Invoke();
            Click(ByName(window, "Read data")); ByName(window, "State missing");
            if (!File.Exists(file)) throw new InvalidOperationException("Clearing site data removed a download file.");
            passed.Add("settings clears site data without deleting downloads");
            Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_I);
            ByName(window, "About", ControlType.Button).AsButton().Invoke();
            ById(window, "CheckForUpdates");
            Thread.Sleep(300);
            Capture.Element(window).ToFile(Path.Combine(report, "updates.png"));
            passed.Add("update settings renders with accessible manual check");
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            if (window is not null)
            {
                try
                {
                    Capture.Element(window).ToFile(Path.Combine(report, "failure.png"));
                    File.WriteAllText(Path.Combine(report, "tree.json"), JsonSerializer.Serialize(window.FindAllDescendants().Select(e => new
                    { name = e.Properties.Name.ValueOrDefault, id = e.Properties.AutomationId.ValueOrDefault,
                        type = e.Properties.ControlType.ValueOrDefault.ToString() }), new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { /* Preserve the original assertion if capture fails. */ }
            }
        }
        finally
        {
            app.Close();
            if (!app.HasExited) app.Kill();
            server.StopAsync().GetAwaiter().GetResult();
        }
        File.WriteAllText(Path.Combine(report, "interactions.json"), JsonSerializer.Serialize(new
        { success = error is null, passed, error }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(error ?? $"Passed {passed.Count} native browser interaction scenarios.");
        return error is null ? 0 : 1;
    }

    private static string Page(string name) => $"""
        <!doctype html><html><title>{System.Net.WebUtility.HtmlEncode(name)}</title>
        <body><h1>{System.Net.WebUtility.HtmlEncode(name)}</h1><a href="/two">Next</a>
        <button>Focus {System.Net.WebUtility.HtmlEncode(name)}</button>
        <button onclick="document.documentElement.requestFullscreen()">Fullscreen</button>
        <a href="/download">Download</a>
        <button onclick="localStorage.setItem('bow','yes');document.cookie='bow=yes;path=/'">Seed data</button>
        <button onclick="document.getElementById('state').textContent=localStorage.getItem('bow')==='yes'&&document.cookie.includes('bow=yes')?'State present':'State missing'">Read data</button>
        <p id="state" role="status"></p></body></html>
        """;

    private static AutomationElement[] Tabs(Window window) => window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
        .Where(e => e.Properties.AutomationId.ValueOrDefault?.StartsWith("Tab-") == true).ToArray();
    private static AutomationElement Tab(Window window, string name) => ByName(window, "Tab: " + name, ControlType.Button);
    private static AutomationElement ByName(Window window, string name, ControlType? type = null)
    {
        AutomationElement? result = null;
        Wait(() => (result = window.FindFirstDescendant(cf => type is null ? cf.ByName(name) : cf.ByName(name).And(cf.ByControlType(type.Value)))) is not null, name);
        return result!;
    }
    private static AutomationElement ById(Window window, string id)
    {
        AutomationElement? result = null;
        Wait(() => (result = window.FindFirstDescendant(cf => cf.ByAutomationId(id))) is not null, id);
        return result!;
    }
    private static bool Visible(Window window, string name) => window.FindAllDescendants(cf => cf.ByName(name)).Any(e => !e.IsOffscreen);
    private static string? ActiveTitle(string profile)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(profile, "ui-state.json")));
        var active = json.RootElement.GetProperty("activeTab").GetString();
        return json.RootElement.GetProperty("tabs").EnumerateArray().First(t => t.GetProperty("id").GetString() == active)
            .GetProperty("title").GetString();
    }
    private static int VisiblePaneCount(string profile)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(profile, "ui-state.json")));
        return json.RootElement.GetProperty("panes").EnumerateArray().Count(p => p.GetProperty("visible").GetBoolean());
    }
    private static bool HasTwoPanes(string profile)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(profile, "ui-state.json")));
        var panes = json.RootElement.GetProperty("panes").EnumerateArray().Where(p => p.GetProperty("visible").GetBoolean())
            .OrderBy(p => p.GetProperty("x").GetDouble()).ToArray();
        return panes.Length == 2 && panes.All(p => p.GetProperty("width").GetDouble() > 100)
            && panes[0].GetProperty("x").GetDouble() + panes[0].GetProperty("width").GetDouble() <= panes[1].GetProperty("x").GetDouble() + 2;
    }
    private static Point Center(Rectangle r) => new(r.Left + r.Width / 2, r.Top + r.Height / 2);
    private static void Click(AutomationElement element)
    {
        MovePointer(Center(element.BoundingRectangle)); Thread.Sleep(100);
        Mouse.Down(MouseButton.Left); Thread.Sleep(50); Mouse.Up(MouseButton.Left); Thread.Sleep(100);
    }
    private static void Drag(Point from, Point to)
    {
        MovePointer(from); Thread.Sleep(150); Mouse.Down(MouseButton.Left); Thread.Sleep(150);
        try
        {
            for (var i = 1; i <= 20; i++)
            {
                MovePointer(new Point(from.X + (to.X - from.X) * i / 20, from.Y + (to.Y - from.Y) * i / 20));
                Thread.Sleep(40);
            }
            Thread.Sleep(200);
        }
        finally { Mouse.Up(MouseButton.Left); }
        Thread.Sleep(300);
    }
    private static void MovePointer(Point point)
    {
        // SendInput supplies pointer packets to WinUI; SetCursorPos only relocates the cursor.
        var width = User32.GetSystemMetrics(SystemMetric.SM_CXVIRTUALSCREEN);
        var height = User32.GetSystemMetrics(SystemMetric.SM_CYVIRTUALSCREEN);
        var left = User32.GetSystemMetrics(SystemMetric.SM_XVIRTUALSCREEN);
        var top = User32.GetSystemMetrics(SystemMetric.SM_YVIRTUALSCREEN);
        var input = INPUT.MouseInput(new MOUSEINPUT
        {
            dx = (point.X - left) * 65536 / width + 65536 / (width * 2),
            dy = (point.Y - top) * 65536 / height + 65536 / (height * 2),
            dwFlags = MouseEventFlags.MOUSEEVENTF_MOVE | MouseEventFlags.MOUSEEVENTF_ABSOLUTE | MouseEventFlags.MOUSEEVENTF_VIRTUALDESK
        });
        if (User32.SendInput(1, new[] { input }, INPUT.Size) != 1)
            throw new InvalidOperationException("Mouse input could not be injected.");
    }
    private static void Chord(params VirtualKeyShort[] keys)
    {
        try
        {
            foreach (var key in keys) { Keyboard.Press(key); Thread.Sleep(50); }
            Thread.Sleep(150);
        }
        finally { foreach (var key in keys.Reverse()) Keyboard.Release(key); }
        Thread.Sleep(200);
    }
    private static void Navigate(Window window, string url)
    {
        Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_L);
        Keyboard.Type(url); Keyboard.Type(VirtualKeyShort.RETURN);
        Tab(window, url.Split('/').Last());
    }
    private static void Wait(Func<bool> condition, string label)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Timed out: " + label);
            Thread.Sleep(100);
        }
    }
}
