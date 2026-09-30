namespace BOW.Core;

public sealed record ShortcutDefinition(string Id, string Label, string DefaultBinding);

public sealed record ShortcutChord(string Key, bool Control, bool Alt, bool Shift)
{
    public override string ToString() =>
        (Control ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") + Key;
}

public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutDefinition> Commands { get; } =
    [
        new("address", "Search or enter address", "Ctrl+L"),
        new("new-tab", "New tab", "Ctrl+T"),
        new("close-tab", "Close tab", "Ctrl+W"),
        new("leave-split", "Leave split view", "Ctrl+Shift+2"),
        new("full-screen", "Toggle full screen", "F11"),
        new("reopen-tab", "Reopen closed tab", "Ctrl+Shift+T"),
        new("tab-switcher", "Show tab switcher", "Ctrl+Shift+A"),
        new("next-tab", "Next tab", "Ctrl+Tab"),
        new("previous-tab", "Previous tab", "Ctrl+Shift+Tab"),
        new("zoom-in", "Zoom in", "Ctrl+Plus"),
        new("zoom-out", "Zoom out", "Ctrl+Minus"),
        new("zoom-reset", "Reset zoom", "Ctrl+0"),
        new("dismiss", "Close popup or leave Zen mode", "Escape"),
        new("settings", "Open settings", "Ctrl+I")
    ];

    public static string GetBinding(SettingsModel settings, ShortcutDefinition command) =>
        settings.KeyboardShortcuts is not null
        && settings.KeyboardShortcuts.TryGetValue(command.Id, out var saved)
        && TryParse(saved, out var chord) ? chord.ToString() : command.DefaultBinding;

    public static bool TrySetBinding(SettingsModel settings, string commandId, string binding, out string error)
    {
        error = string.Empty;
        var command = Commands.FirstOrDefault(item => item.Id == commandId);
        if (command is null || !TryParse(binding, out var chord))
        {
            error = "Choose a valid shortcut with Ctrl or Alt, or use Escape or an F key.";
            return false;
        }
        var normalized = chord.ToString();
        var conflict = Commands.FirstOrDefault(item => item.Id != commandId
            && string.Equals(GetBinding(settings, item), normalized, StringComparison.OrdinalIgnoreCase));
        if (conflict is not null)
        {
            error = $"Already used by {conflict.Label}.";
            return false;
        }
        settings.KeyboardShortcuts ??= new Dictionary<string, string>();
        settings.KeyboardShortcuts[commandId] = normalized;
        return true;
    }

    public static bool TryParse(string? binding, out ShortcutChord chord)
    {
        chord = new ShortcutChord("", false, false, false);
        if (string.IsNullOrWhiteSpace(binding)) return false;
        var parts = binding.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 4) return false;
        bool ctrl = false, alt = false, shift = false;
        foreach (var part in parts[..^1])
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) && !ctrl) ctrl = true;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase) && !alt) alt = true;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase) && !shift) shift = true;
            else return false;
        }
        var key = parts[^1].ToUpperInvariant();
        key = key switch { "ADD" => "Plus", "SUBTRACT" => "Minus", _ => key };
        if (key.Length == 1 && char.IsLetter(key[0])) { }
        else if (key.Length == 1 && char.IsDigit(key[0])) { }
        else if (key is "TAB" or "ESCAPE" or "PLUS" or "MINUS")
            key = key switch { "TAB" => "Tab", "ESCAPE" => "Escape", "PLUS" => "Plus", _ => "Minus" };
        else if (key.Length is 2 or 3 && key[0] == 'F'
            && int.TryParse(key[1..], out var f) && f is >= 1 and <= 12) { }
        else return false;
        if (!ctrl && !alt && key != "Escape" && !(key.Length > 1 && key.StartsWith('F'))) return false;
        chord = new ShortcutChord(key, ctrl, alt, shift);
        return true;
    }
}
