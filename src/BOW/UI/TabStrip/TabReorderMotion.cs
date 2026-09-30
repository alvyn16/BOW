using System.Numerics;
using Microsoft.UI.Xaml;

namespace BOW.UI.TabStrip;

internal static class TabReorderMotion
{
    private static readonly Windows.UI.ViewManagement.UISettings UiSettings = new();

    public static Dictionary<Guid, double> Capture<T>(FrameworkElement panel,
        IReadOnlyDictionary<Guid, T> elements, bool vertical) where T : FrameworkElement
    {
        var positions = new Dictionary<Guid, double>();
        if (!panel.IsLoaded || !UiSettings.AnimationsEnabled) return positions;
        foreach (var (id, element) in elements)
        {
            if (!element.IsLoaded) continue;
            var point = element.TransformToVisual(panel)
                .TransformPoint(new Windows.Foundation.Point(0, 0));
            positions[id] = vertical ? point.Y : point.X;
        }
        return positions;
    }

    public static void Animate<T>(FrameworkElement panel, IReadOnlyDictionary<Guid, T> elements,
        IReadOnlyDictionary<Guid, double> previous, bool vertical) where T : FrameworkElement
    {
        if (previous.Count == 0) return;
        panel.UpdateLayout();
        foreach (var (id, element) in elements)
        {
            if (!previous.TryGetValue(id, out var oldPosition) || !element.IsLoaded) continue;
            var point = element.TransformToVisual(panel)
                .TransformPoint(new Windows.Foundation.Point(0, 0));
            var delta = oldPosition - (vertical ? point.Y : point.X);
            if (Math.Abs(delta) < 1 || Math.Abs(delta) > 1000) continue;
            element.TranslationTransition = null;
            element.Translation = vertical
                ? new Vector3(0, (float)delta, 0) : new Vector3((float)delta, 0, 0);
            element.TranslationTransition = new Vector3Transition
            {
                Duration = TimeSpan.FromMilliseconds(190)
            };
            panel.DispatcherQueue.TryEnqueue(() => element.Translation = Vector3.Zero);
        }
    }
}
