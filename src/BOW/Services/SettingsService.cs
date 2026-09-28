using System.Text.Json;

namespace BOW.Services;

/// <summary>
/// Loads and saves settings from %LOCALAPPDATA%\BOW\settings.json.
/// </summary>
public static class SettingsService
{
    private static readonly string _settingsPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BOW", "settings.json");

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>Loads settings from disk. Returns defaults if file is missing or corrupt.</summary>
    public static BOW.Core.SettingsModel Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new BOW.Core.SettingsModel();

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<BOW.Core.SettingsModel>(json) ?? new BOW.Core.SettingsModel();
        }
        catch
        {
            return new BOW.Core.SettingsModel();
        }
    }

    /// <summary>Saves settings atomically (write to .tmp, then rename).</summary>
    public static void Save(BOW.Core.SettingsModel settings)
    {
        var dir = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(dir);

        var tmp = _settingsPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(tmp, json);
        File.Move(tmp, _settingsPath, overwrite: true);
    }
}
