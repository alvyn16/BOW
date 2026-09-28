namespace BOW.Core;

/// <summary>
/// Persisted application settings. Serialized to/from JSON.
/// </summary>
public class SettingsModel
{
    /// <summary>"Auto" | "Light" | "Dark"</summary>
    public string Theme { get; set; } = "Auto";

    /// <summary>"Black" | "White". White is the default app icon.</summary>
    public string AppIconVariant { get; set; } = "White";

    /// <summary>"Strip" | "Sidebar"</summary>
    public string TabLayout { get; set; } = "Strip";

    /// <summary>Folder where downloads are saved.</summary>
    public string DownloadFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";

    public bool AskWhereToSaveDownloads { get; set; } = false;

    /// <summary>"DuckDuckGo" | "Startpage" | "Google"</summary>
    public string SearchEngine { get; set; } = "DuckDuckGo";

    /// <summary>Used when SearchEngine == "Custom".</summary>
    public string CustomSearchUrl { get; set; } = string.Empty;

    /// <summary>Minutes before an idle tab is put to sleep.</summary>
    public int TabSleepMinutes { get; set; } = 10;

    /// <summary>Restore the previous session on launch.</summary>
    public bool RestoreSessionOnStart { get; set; } = true;

    /// <summary>Zen mode: hide all chrome, show only web content.</summary>
    public bool ZenMode { get; set; } = false;

    /// <summary>Show the top bar / window frame chrome.</summary>
    public bool ShowWindowFrame { get; set; } = true;

    /// <summary>Smooth scrolling in WebView2.</summary>
    public bool SmoothScrolling { get; set; } = true;

    /// <summary>Command IDs mapped to keyboard chords. Missing IDs use defaults.</summary>
    public Dictionary<string, string> KeyboardShortcuts { get; set; } = new();
}
