using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BOW.UI;

public sealed class QuietToggle : Button
{
    private readonly Border _thumb;
    private readonly string _label;
    private bool _isOn;

    public event Action<bool>? Changed;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value) return;
            _isOn = value;
            Refresh();
            Changed?.Invoke(value);
        }
    }

    public QuietToggle(string label)
    {
        _label = label;
        Width = 42;
        Height = 24;
        Padding = new Thickness(3);
        CornerRadius = new CornerRadius(12);
        BorderThickness = new Thickness(0);
        _thumb = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = ThemeBrushes.WindowBackgroundBrush
        };
        Content = _thumb;
        Click += (_, _) => IsOn = !IsOn;
        Refresh();
    }

    private void Refresh()
    {
        Background = IsOn ? ThemeBrushes.SwitchTrackBrush : ThemeBrushes.TopBarBorderBrush;
        HorizontalContentAlignment = IsOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        AutomationProperties.SetName(this, $"{_label}, {(IsOn ? "on" : "off")}");
    }
}
