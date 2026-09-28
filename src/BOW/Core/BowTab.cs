using CommunityToolkit.Mvvm.ComponentModel;

namespace BOW.Core;

/// <summary>
/// Represents a single browser tab's state.
/// </summary>
public partial class BowTab : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _title = "New Tab";

    [ObservableProperty]
    private string? _faviconUrl;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private string? _groupName;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isSleeping;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasLoadedSuccessfully;

    [ObservableProperty]
    private bool _navigationFailed;

    [ObservableProperty]
    private double _zoomFactor = 1.0;

    [ObservableProperty]
    private string? _savedScrollPosition;

    [ObservableProperty]
    private bool _isSplitPartner;

    [ObservableProperty]
    private Guid? _splitPartnerId;

    /// <summary>Last time this tab was the active tab.</summary>
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;
}
