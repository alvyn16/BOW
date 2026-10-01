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

    public static BowStore Store { get; private set; } = null!;
    public static MainWindow? MainWindow { get; private set; }

    public App() : this(null) { }

    internal App(string? smokeTestReport)
    {
        _smokeTestReport = smokeTestReport;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_smokeTestReport is not null)
        {
            _ = StartupSmokeTest.RunAsync(this, _smokeTestReport);
            return;
        }
        Store = new BowStore();
        _mainWindow = new MainWindow();
        _sessionAutoSaver = new SessionAutoSaver(Store,
            dispatch: action => _mainWindow.DispatcherQueue.TryEnqueue(() => action()));
        MainWindow = _mainWindow;
        _mainWindow.Closed += OnMainWindowClosed;
        ApplyTheme(_mainWindow, Store.Settings.Theme);
        _mainWindow.Activate();
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
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
