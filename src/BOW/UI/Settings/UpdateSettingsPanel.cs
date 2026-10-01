using BOW.Core;
using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace BOW.UI.Settings;

internal sealed class UpdateSettingsPanel : StackPanel
{
    internal UpdateSettingsPanel(SettingsModel settings)
    {
        Spacing = 12;
        settings.UpdateChannel = settings.UpdateChannel == "Preview" ? "Preview" : "Stable";
        var status = new TextBlock
        {
            Text = "", FontSize = 13, TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.TextBrush
        };
        void SavePreferences()
        {
            try { SettingsService.Save(settings); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { status.Text = "Could not save update preferences. Check folder permissions."; }
        }
        Children.Add(new TextBlock
        {
            Text = "Version " + ReleaseUpdateService.InstalledVersion,
            FontSize = 13, Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        var channel = new ComboBox
        {
            Header = "Release channel", ItemsSource = new[] { "Stable", "Preview" },
            SelectedItem = settings.UpdateChannel == "Preview" ? "Preview" : "Stable",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(channel, "Release channel");
        channel.SelectionChanged += (_, _) =>
        {
            settings.UpdateChannel = channel.SelectedItem?.ToString() ?? "Stable";
            SavePreferences();
        };
        Children.Add(channel);
        var automatic = new ToggleSwitch
        {
            Header = "Check for updates on startup", IsOn = settings.CheckUpdatesOnStartup,
            OnContent = "On", OffContent = "Off"
        };
        automatic.Toggled += (_, _) =>
        {
            settings.CheckUpdatesOnStartup = automatic.IsOn;
            SavePreferences();
        };
        Children.Add(automatic);
        var check = new Button { Content = "Check for updates", HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetAutomationId(check, "CheckForUpdates");
        AutomationProperties.SetLiveSetting(status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        var release = new HyperlinkButton { Content = "View release", Visibility = Visibility.Collapsed, Padding = new Thickness(0) };
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false; channel.IsEnabled = false;
            release.Visibility = Visibility.Collapsed;
            status.Text = "Checking for updates...";
            try
            {
                var result = await new ReleaseUpdateService().CheckAsync(
                    ReleaseUpdateService.InstalledVersion, settings.UpdateChannel);
                settings.LastUpdateCheck = DateTimeOffset.UtcNow;
                SettingsService.Save(settings);
                status.Text = result.AvailableVersion is null ? "No releases are available in this channel."
                    : result.IsNewer ? "BOW " + result.AvailableVersion + " is available."
                    : "You are up to date for this channel.";
                if (result.IsNewer)
                {
                    release.NavigateUri = result.ReleasePage;
                    release.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                or IOException or UnauthorizedAccessException)
            { status.Text = "Could not check for updates. Check your connection and try again."; }
            finally { check.IsEnabled = true; channel.IsEnabled = true; }
        };
        Children.Add(check); Children.Add(status); Children.Add(release);
    }
}
