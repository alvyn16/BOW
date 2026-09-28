using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BOW.UI.Downloads;

public sealed class DownloadsPanel : UserControl
{
    private readonly StackPanel _items = new() { Spacing = 12 };
    private readonly List<(DownloadItem Item, System.ComponentModel.PropertyChangedEventHandler Handler)> _subscriptions = new();

    public DownloadsPanel()
    {
        var root = new StackPanel { Width = 360, Padding = new Thickness(12), Spacing = 12 };
        root.Children.Add(new TextBlock { Text = "Downloads", FontSize = 18 });
        root.Children.Add(new ScrollViewer { Content = _items, MaxHeight = 420 });
        Content = root;

        DownloadService.Instance.Downloads.CollectionChanged += (_, _) => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        foreach (var (item, handler) in _subscriptions) item.PropertyChanged -= handler;
        _subscriptions.Clear();
        _items.Children.Clear();
        if (DownloadService.Instance.Downloads.Count == 0)
        {
            _items.Children.Add(new TextBlock { Text = "No downloads yet" });
            return;
        }

        foreach (var item in DownloadService.Instance.Downloads)
            _items.Children.Add(CreateRow(item));
    }

    private UIElement CreateRow(DownloadItem item)
    {
        var row = new StackPanel { Spacing = 6 };
        row.Children.Add(new TextBlock { Text = item.FileName, TextTrimming = TextTrimming.CharacterEllipsis });
        row.Children.Add(new TextBlock
        {
            Text = item.LocalPath,
            FontSize = 11,
            Opacity = 0.6,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var progress = new TextBlock { FontSize = 11, Opacity = 0.6 };
        row.Children.Add(progress);
        var status = new TextBlock { FontSize = 12, Opacity = 0.7 };
        row.Children.Add(status);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var pause = new Button { Content = "Pause" };
        var resume = new Button { Content = "Resume" };
        var cancel = new Button { Content = "Cancel" };
        var open = new Button { Content = "Open" };
        var show = new Button { Content = "Show in folder" };
        pause.Click += (_, _) => DownloadService.Instance.Pause(item.Id);
        resume.Click += (_, _) => DownloadService.Instance.Resume(item.Id);
        cancel.Click += (_, _) => DownloadService.Instance.Cancel(item.Id);
        open.Click += (_, _) => DownloadService.Instance.OpenFile(item.Id);
        show.Click += (_, _) => DownloadService.Instance.ShowInExplorer(item.Id);
        actions.Children.Add(pause);
        actions.Children.Add(resume);
        actions.Children.Add(cancel);
        actions.Children.Add(open);
        actions.Children.Add(show);
        row.Children.Add(actions);

        void Update()
        {
            progress.Text = item.State == DownloadState.InProgress
                ? item.TotalBytes > 0 ? $"{item.Progress:P0} complete" : "Downloading…"
                : string.Empty;
            status.Text = item.State switch
            {
                DownloadState.InProgress when item.TotalBytes > 0 =>
                    $"{item.Progress:P0} · {item.BytesReceived / 1048576.0:F1} MB",
                DownloadState.Failed => $"Failed · {item.FailureReason ?? "The download was interrupted."}",
                DownloadState.Completed when !System.IO.File.Exists(item.LocalPath) =>
                    "Downloaded file was moved or deleted",
                _ => item.State.ToString()
            };
            pause.Visibility = item.State == DownloadState.InProgress ? Visibility.Visible : Visibility.Collapsed;
            resume.Visibility = item.State == DownloadState.Paused ? Visibility.Visible : Visibility.Collapsed;
            cancel.Visibility = item.State is DownloadState.InProgress or DownloadState.Paused ? Visibility.Visible : Visibility.Collapsed;
            open.Visibility = item.State == DownloadState.Completed && System.IO.File.Exists(item.LocalPath)
                ? Visibility.Visible : Visibility.Collapsed;
            show.Visibility = System.IO.File.Exists(item.LocalPath)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        System.ComponentModel.PropertyChangedEventHandler handler = (_, _) => row.DispatcherQueue.TryEnqueue(Update);
        item.PropertyChanged += handler;
        _subscriptions.Add((item, handler));
        Update();
        return row;
    }
}
