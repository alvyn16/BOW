using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace BOW.UI.TabStrip;

internal sealed class TabDragGesture
{
    private readonly UIElement _element;
    private uint? _pointerId;
    private Windows.Foundation.Point _start;
    private bool _starting;

    public bool WasDragged { get; private set; }

    public TabDragGesture(UIElement element)
    {
        _element = element;
        element.AddHandler(UIElement.PointerPressedEvent,
            new PointerEventHandler(OnPressed), true);
        element.AddHandler(UIElement.PointerMovedEvent,
            new PointerEventHandler(OnMoved), true);
        element.AddHandler(UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnReleased), true);
        element.PointerCaptureLost += OnReleased;
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_element);
        if (!point.Properties.IsLeftButtonPressed) return;
        _pointerId = e.Pointer.PointerId;
        _start = point.Position;
        WasDragged = false;
        _element.CapturePointer(e.Pointer);
    }

    private async void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_starting || _pointerId != e.Pointer.PointerId) return;
        var point = e.GetCurrentPoint(_element);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _pointerId = null;
            return;
        }
        var dx = point.Position.X - _start.X;
        var dy = point.Position.Y - _start.Y;
        if (dx * dx + dy * dy < 64) return;

        _pointerId = null;
        _starting = true;
        WasDragged = true;
        _element.ReleasePointerCapture(e.Pointer);
        try { await _element.StartDragAsync(point); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Tab drag could not start: {ex}"); }
        finally
        {
            _starting = false;
            App.MainWindow?.EndTabDrag();
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId) return;
        _pointerId = null;
        // Let Button process release and Click before releasing our capture.
        _element.DispatcherQueue.TryEnqueue(() => _element.ReleasePointerCapture(e.Pointer));
    }
}
