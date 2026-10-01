namespace BOW.Services;

internal static class BrowserData
{
    internal static string? InteractionSnapshotPath { get; set; }
    internal static string DirectoryPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BOW");
    internal static string FilePath(string name) => Path.Combine(DirectoryPath, name);
}
