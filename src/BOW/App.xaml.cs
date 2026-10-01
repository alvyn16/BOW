using BOW.Core;
using BOW.Services;
using BOW.UI.WebView;
using BOW.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace BOW;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private SessionAutoSaver? _sessionAutoSaver;
    private readonly string? _smokeTestReport;
    private readonly string? _benchmarkReport;
    private readonly System.Diagnostics.Stopwatch? _started;
    private bool _mainWindowClosed;

    public static BowStore Store { get; private set; } = null!;
    public static MainWindow? MainWindow { get; private set; }

    public App() : this(null) { }

    internal App(string? smokeTestReport, string? benchmarkReport = null,
        System.Diagnostics.Stopwatch? started = null)
    {
        _smokeTestReport = smokeTestReport;
        _benchmarkReport = benchmarkReport;
        _started = started;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_smokeTestReport is not null)
        {
            _ = StartupSmokeTest.RunAsync(this, _smokeTestReport);
            return;
        }
        Store = _benchmarkReport is null ? new BowStore() : new BowStore(
            new SettingsModel { TabSleepMinutes = 0 }, Array.Empty<SessionEntry>());
        _mainWindow = new MainWindow();
        _sessionAutoSaver = new SessionAutoSaver(Store,
            dispatch: action => _mainWindow.DispatcherQueue.TryEnqueue(() => action()));
        MainWindow = _mainWindow;
        _mainWindow.Closed += OnMainWindowClosed;
        ApplyTheme(_mainWindow, Store.Settings.Theme);
        _mainWindow.Activate();
        if (BrowserData.InteractionSnapshotPath is { } snapshotPath)
            _mainWindow.StartInteractionSnapshots(snapshotPath);
        if (_benchmarkReport is not null)
            _ = PerformanceBenchmark.RunAsync(this, _mainWindow, _benchmarkReport, _started!);
        else
            _ = CheckStartupUpdateAsync();
    }

    private async Task CheckStartupUpdateAsync()
    {
        var now = DateTimeOffset.UtcNow;
        if (!Store.Settings.CheckUpdatesOnStartup ||
            Store.Settings.LastUpdateCheck is { } checkedAt && checkedAt <= now && now - checkedAt < TimeSpan.FromDays(1)) return;
        try
        {
            Store.Settings.LastUpdateCheck = DateTimeOffset.UtcNow;
            SettingsService.Save(Store.Settings);
            var result = await new ReleaseUpdateService().CheckAsync(ReleaseUpdateService.InstalledVersion,
                Store.Settings.UpdateChannel == "Preview" ? "Preview" : "Stable");
            if (result.IsNewer && !_mainWindowClosed && _mainWindow is not null) _mainWindow.ShowUpdateAvailable(result);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        { System.Diagnostics.Debug.WriteLine("Background update check did not complete."); }
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        _mainWindowClosed = true;
        _sessionAutoSaver?.Dispose();
        SettingsService.Save(Store.Settings);
    }

    internal static void ApplyTheme(Window window, string theme)
    {
        ThemeBrushes.Apply(theme);
        if (window is MainWindow mainWindow) mainWindow.ApplyTitleBarTheme();
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }

    }

    internal static void ChangeTheme(string theme)
    {
        Store.Settings.Theme = theme;
        if (MainWindow is { } window)
            ApplyTheme(window, theme);
        else
            ThemeBrushes.Apply(theme);

        var icon = ThemeBrushes.IsDark ? "Black" : "White";
        Store.Settings.AppIconVariant = icon;
        MainWindow?.ApplyAppIcon(icon);
        MainWindow?.TopBar.SyncThemeIcon(theme);
        SettingsService.Save(Store.Settings);
    }
}
