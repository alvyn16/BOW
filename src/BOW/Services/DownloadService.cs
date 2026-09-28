using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Web.WebView2.Core;

namespace BOW.Services;

/// <summary>Represents a single download.</summary>
public partial class DownloadItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Uri { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string LocalPath { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;

    [ObservableProperty] private long _bytesReceived;
    [ObservableProperty] private long _totalBytes;
    [ObservableProperty] private DownloadState _state = DownloadState.InProgress;
    [ObservableProperty] private string? _failureReason;

    public double Progress => TotalBytes > 0 ? (double)BytesReceived / TotalBytes : 0;

    internal CoreWebView2DownloadOperation? Operation { get; set; }
}

/// <summary>Singleton download service — collects downloads from all WebView2 instances.</summary>
public sealed class DownloadService
{
    public static readonly DownloadService Instance = new();
    public System.Collections.ObjectModel.ObservableCollection<DownloadItem> Downloads { get; } = new();

    private DownloadService()
    {
        foreach (var record in DownloadHistoryStore.Load(DownloadHistoryStore.DefaultPath))
            Downloads.Add(new DownloadItem
            {
                Id = record.Id,
                Uri = record.Uri,
                FileName = record.FileName,
                LocalPath = record.LocalPath,
                StartedAt = record.StartedAt,
                State = record.State is DownloadState.InProgress or DownloadState.Paused
                    ? DownloadState.Failed : record.State,
                BytesReceived = record.BytesReceived,
                TotalBytes = record.TotalBytes,
                FailureReason = record.State is DownloadState.InProgress or DownloadState.Paused
                    ? "BOW closed before this download finished." : record.FailureReason
            });
        Persist();
    }

    public void HandleDownload(CoreWebView2DownloadStartingEventArgs e, string downloadFolder)
    {
        e.Handled = true;
        var op = e.DownloadOperation;
        var fileName = System.IO.Path.GetFileName(e.ResultFilePath);
        if (string.IsNullOrEmpty(fileName))
            fileName = $"download_{DateTime.Now:yyyyMMdd_HHmmss}";

        try { System.IO.Directory.CreateDirectory(downloadFolder); }
        catch (Exception ex)
        {
            e.Cancel = true;
            Downloads.Insert(0, new DownloadItem
            {
                Uri = op.Uri, FileName = fileName, LocalPath = downloadFolder,
                State = DownloadState.Failed,
                FailureReason = $"Could not use the selected folder: {ex.Message}"
            });
            Persist();
            return;
        }

        var localPath = System.IO.Path.Combine(downloadFolder, fileName);
        var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var extension = System.IO.Path.GetExtension(fileName);
        for (var suffix = 1; System.IO.File.Exists(localPath); suffix++)
        {
            fileName = $"{stem} ({suffix}){extension}";
            localPath = System.IO.Path.Combine(downloadFolder, fileName);
        }
        e.ResultFilePath = localPath;

        var item = new DownloadItem
        {
            Uri = op.Uri,
            FileName = fileName,
            LocalPath = localPath,
            TotalBytes = op.TotalBytesToReceive,
            Operation = op
        };

        op.BytesReceivedChanged += (_, _) =>
        {
            item.BytesReceived = op.BytesReceived;
            item.TotalBytes = op.TotalBytesToReceive;
        };
        op.StateChanged += (_, _) =>
        {
            if (op.State == CoreWebView2DownloadState.Interrupted
                && item.State is not (DownloadState.Cancelled or DownloadState.Paused))
                item.FailureReason = ExplainFailure(op.InterruptReason);
            item.State = op.State switch
            {
                CoreWebView2DownloadState.Completed => DownloadState.Completed,
                CoreWebView2DownloadState.Interrupted when item.State == DownloadState.Cancelled => DownloadState.Cancelled,
                CoreWebView2DownloadState.Interrupted when item.State == DownloadState.Paused => DownloadState.Paused,
                CoreWebView2DownloadState.Interrupted => DownloadState.Failed,
                _ => DownloadState.InProgress
            };
            if (item.State is DownloadState.Completed or DownloadState.Cancelled or DownloadState.Failed)
            {
                item.BytesReceived = op.BytesReceived;
                item.TotalBytes = op.TotalBytesToReceive;
                item.Operation = null;
                Persist();
            }
        };

        Downloads.Insert(0, item);
        while (Downloads.Count > 200) Downloads.RemoveAt(Downloads.Count - 1);
        Persist();
    }

    public void Pause(Guid id)
    {
        var item = Downloads.FirstOrDefault(d => d.Id == id);
        item?.Operation?.Pause();
        if (item is not null) { item.State = DownloadState.Paused; Persist(); }
    }

    public void Resume(Guid id)
    {
        var item = Downloads.FirstOrDefault(d => d.Id == id);
        item?.Operation?.Resume();
        if (item is not null) { item.State = DownloadState.InProgress; Persist(); }
    }

    public void Cancel(Guid id)
    {
        var item = Downloads.FirstOrDefault(d => d.Id == id);
        item?.Operation?.Cancel();
        if (item is not null) { item.State = DownloadState.Cancelled; Persist(); }
    }

    public void OpenFile(Guid id)
    {
        var item = Downloads.FirstOrDefault(d => d.Id == id);
        if (item?.State == DownloadState.Completed && System.IO.File.Exists(item.LocalPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.LocalPath)
            {
                UseShellExecute = true
            });
    }

    public void ShowInExplorer(Guid id)
    {
        var item = Downloads.FirstOrDefault(d => d.Id == id);
        if (item is not null && System.IO.File.Exists(item.LocalPath))
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.LocalPath}\"");
    }

    private void Persist()
    {
        try
        {
            DownloadHistoryStore.Save(DownloadHistoryStore.DefaultPath, Downloads.Select(item =>
                new DownloadRecord(item.Id, item.Uri, item.FileName, item.LocalPath,
                    item.StartedAt, item.State, item.BytesReceived, item.TotalBytes, item.FailureReason)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not save download history: {ex.Message}");
        }
    }

    private static string ExplainFailure(CoreWebView2DownloadInterruptReason reason) => reason switch
    {
        CoreWebView2DownloadInterruptReason.FileNoSpace => "The disk is full. Free space or choose another folder.",
        CoreWebView2DownloadInterruptReason.FileAccessDenied => "BOW cannot write to that folder. Choose another location.",
        CoreWebView2DownloadInterruptReason.FileNameTooLong => "The file path is too long. Choose a shorter location.",
        CoreWebView2DownloadInterruptReason.FileMalicious => "The file was blocked because it may be harmful.",
        CoreWebView2DownloadInterruptReason.FileBlockedByPolicy => "The file was blocked by system policy.",
        CoreWebView2DownloadInterruptReason.NetworkDisconnected => "The network connection was lost.",
        CoreWebView2DownloadInterruptReason.NetworkTimeout => "The connection timed out.",
        CoreWebView2DownloadInterruptReason.ServerForbidden => "The server refused the download.",
        CoreWebView2DownloadInterruptReason.ServerUnauthorized => "The server requires sign-in.",
        CoreWebView2DownloadInterruptReason.UserShutdown => "BOW closed before this download finished.",
        CoreWebView2DownloadInterruptReason.DownloadProcessCrashed => "The browser download process stopped unexpectedly.",
        _ => $"The download stopped ({reason})."
    };
}
