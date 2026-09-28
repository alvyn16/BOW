using BOW.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace BOW.UI.Security;

/// <summary>Persistent permissions for the website in the active tab.</summary>
public sealed class SitePermissionsSettingsView : UserControl
{
    private static readonly (string Label, CoreWebView2PermissionKind Kind)[] Permissions =
    [
        ("Camera", CoreWebView2PermissionKind.Camera),
        ("Microphone", CoreWebView2PermissionKind.Microphone),
        ("Location", CoreWebView2PermissionKind.Geolocation),
        ("Notifications", CoreWebView2PermissionKind.Notifications)
    ];

    private static readonly (string Label, CoreWebView2PermissionState State)[] Choices =
    [
        ("Ask", CoreWebView2PermissionState.Default),
        ("Allow", CoreWebView2PermissionState.Allow),
        ("Block", CoreWebView2PermissionState.Deny)
    ];

    private readonly Uri? _address;
    private readonly CoreWebView2? _core;
    private readonly TextBlock _status;
    private readonly Dictionary<CoreWebView2PermissionKind, Button[]> _buttons = new();

    public SitePermissionsSettingsView(Uri? address, CoreWebView2? core,
        bool loadedSuccessfully, bool navigationFailed)
    {
        _address = address;
        _core = core;
        var panel = new StackPanel { Spacing = 8 };
        var site = new TextBlock
        {
            Text = address is null ? "Open a website to manage its permissions." : address.AbsoluteUri,
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.TextBrush,
            Margin = new Thickness(0, 0, 0, 2),
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(site);
        if (address is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = address.Scheme == "http" ? "Not secure · HTTP connection"
                    : loadedSuccessfully ? "HTTPS connection established"
                    : navigationFailed ? "HTTPS connection failed" : "HTTPS connection pending",
                FontFamily = ThemeBrushes.UiFont,
                FontSize = 11,
                Foreground = ThemeBrushes.MutedTextBrush,
                Margin = new Thickness(0, 0, 0, 10)
            });
        }

        foreach (var (label, kind) in Permissions)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = ThemeBrushes.UiFont,
                FontSize = 12,
                Foreground = ThemeBrushes.TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            var choices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var buttons = new Button[Choices.Length];
            for (var i = 0; i < Choices.Length; i++)
            {
                var choice = Choices[i];
                var button = new Button
                {
                    Content = choice.Label,
                    Width = 70,
                    Height = 30,
                    FontFamily = ThemeBrushes.UiFont,
                    FontSize = 11,
                    Foreground = ThemeBrushes.TextBrush,
                    Background = ThemeBrushes.ControlSurfaceBrush,
                    BorderBrush = ThemeBrushes.TopBarBorderBrush,
                    BorderThickness = new Thickness(1),
                    IsEnabled = false
                };
                AutomationProperties.SetName(button, $"{label}: {choice.Label}");
                button.Click += async (_, _) => await SaveAsync(kind, choice.State);
                buttons[i] = button;
                choices.Children.Add(button);
            }
            _buttons.Add(kind, buttons);
            Grid.SetColumn(choices, 1);
            row.Children.Add(choices);
            panel.Children.Add(new Border
            {
                Child = row,
                Padding = new Thickness(12),
                Background = ThemeBrushes.ControlSurfaceBrush,
                BorderBrush = ThemeBrushes.TopBarBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            });
        }

        _status = new TextBlock
        {
            Text = address is null ? "Visit a site, then return to this section."
                : core is null ? "Wait for the active page to load, then reopen this section."
                : "Loading saved permissions…",
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 11,
            Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        panel.Children.Add(_status);
        Content = panel;
        if (core is not null && address is not null)
            Loaded += async (_, _) => { await LoadAsync(); };
    }

    private async Task<CoreWebView2PermissionState?> LoadAsync(
        CoreWebView2PermissionKind? verifyKind = null)
    {
        if (_core is null || _address is null) return null;
        try
        {
            var settings = await _core.Profile.GetNonDefaultPermissionSettingsAsync();
            CoreWebView2PermissionState? verifiedState = null;
            foreach (var (label, kind) in Permissions)
            {
                var saved = settings.FirstOrDefault(item => item.PermissionKind == kind
                    && Uri.TryCreate(item.PermissionOrigin, UriKind.Absolute, out var origin)
                    && string.Equals(origin.GetLeftPart(UriPartial.Authority),
                        _address.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));
                var current = saved?.PermissionState ?? CoreWebView2PermissionState.Default;
                if (kind == verifyKind) verifiedState = current;
                for (var i = 0; i < Choices.Length; i++)
                {
                    var button = _buttons[kind][i];
                    var selected = Choices[i].State == current;
                    button.Background = selected ? ThemeBrushes.SelectedBrush : ThemeBrushes.ControlSurfaceBrush;
                    button.IsEnabled = true;
                    AutomationProperties.SetName(button,
                        $"{label}: {Choices[i].Label}{(selected ? ", selected" : string.Empty)}");
                }
            }
            _status.Text = "Ask prompts you when this site requests access.";
            return verifiedState;
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not load permissions: {ex.Message}";
            return null;
        }
    }

    private async Task SaveAsync(CoreWebView2PermissionKind kind, CoreWebView2PermissionState state)
    {
        if (_core is null || _address is null) return;
        foreach (var button in _buttons[kind]) button.IsEnabled = false;
        try
        {
            await _core.Profile.SetPermissionStateAsync(kind,
                _address.GetLeftPart(UriPartial.Authority), state);
            var savedState = await LoadAsync(kind);
            if (savedState is null) return;
            if (savedState != state)
                _status.Text = "The browser did not save this change. Try again.";
            else
                _status.Text = "Permission saved for this site.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not save permission: {ex.Message}";
        }
        finally
        {
            foreach (var button in _buttons[kind]) button.IsEnabled = true;
        }
    }
}
