using BOW.Core;
using BOW.Services;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;

namespace BOW.UI.WebView;

public sealed class WebViewHost : UserControl
{
    private static readonly SemaphoreSlim SecurityDialogGate = new(1, 1);
    private BowTab? _tab;
    private bool _webViewReady;
    private bool _initializing;
    private bool _downloadNavigationPending;
    private bool _needsRecovery;
    private bool _disposed;
    private DateTimeOffset _ignoreUnresponsiveUntil;
    private System.Exception? _initializationException;
    private readonly TextBlock _errorTitle;
    private readonly TextBlock _errorDescription;
    private readonly HyperlinkButton _runtimeLink;
    private readonly Button _retryButton;
    private readonly Button _waitButton;

    public Microsoft.UI.Xaml.Controls.WebView2 WebView { get; }
    public Grid WebViewErrorPanel { get; }
    public Grid SleepPanel { get; }

    public WebViewHost()
    {
        var root = new Grid();

        WebView = new Microsoft.UI.Xaml.Controls.WebView2();
        root.Children.Add(WebView);

        WebViewErrorPanel = new Grid
        {
            Visibility = Visibility.Collapsed,
            Background = ThemeBrushes.WindowBackgroundBrush
        };
        var errStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 12 };
        _errorTitle = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        _errorDescription = new TextBlock { FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.6, TextWrapping = TextWrapping.Wrap, MaxWidth = 500 };
        _runtimeLink = new HyperlinkButton { Content = "Download WebView2 runtime", NavigateUri = new System.Uri("https://aka.ms/webview2"), HorizontalAlignment = HorizontalAlignment.Center };
        _retryButton = new Button
        {
            Content = "Try again",
            FontFamily = ThemeBrushes.UiFont,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _retryButton.Click += (_, _) =>
        {
            if (_tab is null) return;
            if (_needsRecovery)
            {
                App.MainWindow?.RecoverTab(_tab.Id);
                return;
            }
            WebViewErrorPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
            Navigate(_tab.Url);
        };
        _waitButton = new Button
        {
            Content = "Keep waiting",
            FontFamily = ThemeBrushes.UiFont,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _waitButton.Click += (_, _) =>
        {
            _needsRecovery = false;
            _ignoreUnresponsiveUntil = DateTimeOffset.UtcNow.AddMinutes(1);
            if (_tab is not null) _tab.NavigationFailed = false;
            WebViewErrorPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
        };
        errStack.Children.Add(_errorTitle);
        errStack.Children.Add(_errorDescription);
        errStack.Children.Add(_runtimeLink);
        errStack.Children.Add(_retryButton);
        errStack.Children.Add(_waitButton);
        WebViewErrorPanel.Children.Add(errStack);
        root.Children.Add(WebViewErrorPanel);

        SleepPanel = new Grid
        {
            Visibility = Visibility.Collapsed,
            Background = ThemeBrushes.WindowBackgroundBrush
        };
        var sleepStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 8 };
        sleepStack.Children.Add(new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE708", FontSize = 32, Opacity = 0.4 });
        sleepStack.Children.Add(new TextBlock { Text = "Tab sleeping", FontSize = 14, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center });
        sleepStack.Children.Add(new TextBlock { Text = "Click to wake", FontSize = 12, Opacity = 0.4, HorizontalAlignment = HorizontalAlignment.Center });
        SleepPanel.Children.Add(sleepStack);
        root.Children.Add(SleepPanel);

        this.Content = root;

        WebView.CoreWebView2Initialized += (_, args) => _initializationException = args.Exception;
        WebView.Loaded += OnLoaded;
        SleepPanel.PointerPressed += (_, _) => _ = WakeAsync();
    }

    public void SetTab(BowTab tab)
    {
        if (_tab is not null)
            _tab.PropertyChanged -= OnTabPropertyChanged;

        _tab = tab;
        _tab.PropertyChanged += OnTabPropertyChanged;
        if (_webViewReady && WebView.CoreWebView2 is { } core)
            core.IsMuted = tab.IsMuted;

        if (_tab.IsSleeping)
        {
            ShowSleepPanel();
        }
        else
        {
            SleepPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
            if (_webViewReady && !string.IsNullOrEmpty(tab.Url))
            {
                Navigate(tab.Url);
                _ = ApplyZoomAsync();
            }
        }

    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_tab is null) return;
            switch (e.PropertyName)
            {
                case nameof(BowTab.Url) when _webViewReady:
                    if (WebView.CoreWebView2.Source != _tab.Url) Navigate(_tab.Url);
                    break;
                case nameof(BowTab.ZoomFactor) when _webViewReady:
                    _ = ApplyZoomAsync();
                    break;
                case nameof(BowTab.IsMuted) when _webViewReady:
                    WebView.CoreWebView2.IsMuted = _tab.IsMuted;
                    break;
                case nameof(BowTab.IsSleeping):
                    if (_tab.IsSleeping) ShowSleepPanel();
                    else _ = WakeAsync();
                    break;
            }
        });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webViewReady || _initializing) return;
        _initializing = true;
        try
        {
            await WebView.EnsureCoreWebView2Async();
            var core = WebView.CoreWebView2 ?? throw _initializationException
                ?? new System.InvalidOperationException("WebView2 initialization completed without a browser instance.");
            _webViewReady = true;
            core.IsMuted = _tab?.IsMuted ?? false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;

            core.NavigationCompleted += OnNavigationCompleted;
            core.NavigationStarting += (_, _) => _downloadNavigationPending = false;
            core.SourceChanged += OnSourceChanged;
            core.DocumentTitleChanged += OnDocumentTitleChanged;
            core.DownloadStarting += OnDownloadStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
            core.ServerCertificateErrorDetected += (_, args) =>
                args.Action = CoreWebView2ServerCertificateErrorAction.Cancel;
            core.FaviconChanged += OnFaviconChanged;
            core.PermissionRequested += OnPermissionRequested;
            core.ProcessFailed += OnProcessFailed;

            if (_tab is not null && !_tab.IsSleeping && !string.IsNullOrEmpty(_tab.Url))
            {
                Navigate(_tab.Url);
                _ = ApplyZoomAsync();
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex}");
            bool runtimeMissing;
            try { runtimeMissing = string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
            catch { runtimeMissing = true; }
            _errorTitle.Text = runtimeMissing ? "WebView2 runtime not found" : "WebView2 could not start";
            _errorDescription.Text = runtimeMissing
                ? "BOW requires the WebView2 Evergreen runtime."
                : $"The browser engine failed to initialize (0x{ex.HResult:X8}). Try again after the browser process has stopped.";
            _runtimeLink.Visibility = runtimeMissing ? Visibility.Visible : Visibility.Collapsed;
            _needsRecovery = true;
            _retryButton.Content = "Try again";
            _retryButton.Visibility = Visibility.Visible;
            _waitButton.Visibility = Visibility.Collapsed;
            WebViewErrorPanel.Visibility = Visibility.Visible;
            WebView.Visibility = Visibility.Collapsed;
        }
        finally { _initializing = false; }
    }

    private void Navigate(string url)
    {
        if (string.IsNullOrEmpty(url) || url == "bow:newtab") return;
        if (_tab is not null)
        {
            _tab.HasLoadedSuccessfully = false;
            _tab.NavigationFailed = false;
            _tab.IsLoading = true;
        }
        try { WebView.CoreWebView2.Navigate(url); }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"Navigation failed: {ex}"); }
    }

    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_disposed || _needsRecovery) return;
        var url = sender.Source;
        var succeeded = e.IsSuccess;
        var status = e.WebErrorStatus;
        // WebView2 reports a failed navigation when a response becomes a download.
        // Keep the page that initiated it visible instead of showing a network error.
        var becameDownload = !succeeded && _downloadNavigationPending;
        if (!succeeded && !becameDownload && status == CoreWebView2WebErrorStatus.ConnectionAborted)
        {
            await System.Threading.Tasks.Task.Delay(500);
            becameDownload = _downloadNavigationPending;
        }
        _downloadNavigationPending = false;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || _needsRecovery) return;
            if (_tab != null && string.Equals(_tab.Url, url, StringComparison.OrdinalIgnoreCase)
                && !becameDownload)
            {
                _tab.IsLoading = false;
                _tab.HasLoadedSuccessfully = succeeded;
                _tab.NavigationFailed = !succeeded
                    && status != CoreWebView2WebErrorStatus.OperationCanceled;
            }
            if (succeeded && _tab is not null)
                HistoryService.Instance.RecordVisit(url, sender.DocumentTitle, _tab.FaviconUrl);
            if (succeeded)
            {
                _ignoreUnresponsiveUntil = DateTimeOffset.MinValue;
                WebViewErrorPanel.Visibility = Visibility.Collapsed;
                WebView.Visibility = Visibility.Visible;
            }
            else if (!becameDownload && status != CoreWebView2WebErrorStatus.OperationCanceled)
                ShowNavigationError(url, status);
            _ = ApplyZoomAsync();
            _ = RestoreScrollAsync();
        });
    }

    private void ShowNavigationError(string url, CoreWebView2WebErrorStatus status)
    {
        _needsRecovery = false;
        _retryButton.Content = "Try again";
        _waitButton.Visibility = Visibility.Collapsed;
        _errorTitle.Text = status is CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect
            or CoreWebView2WebErrorStatus.CertificateExpired
            or CoreWebView2WebErrorStatus.CertificateRevoked
            or CoreWebView2WebErrorStatus.CertificateIsInvalid
            ? "This connection is not secure" : "This page could not be opened";
        var detail = status switch
        {
            CoreWebView2WebErrorStatus.HostNameNotResolved => "The site address could not be found. Check the spelling or your connection.",
            CoreWebView2WebErrorStatus.Timeout => "The site took too long to respond. Check your connection and try again.",
            CoreWebView2WebErrorStatus.Disconnected => "The network connection was lost.",
            CoreWebView2WebErrorStatus.CannotConnect or CoreWebView2WebErrorStatus.ServerUnreachable =>
                "BOW could not connect to this site. It may be offline or blocking connections.",
            CoreWebView2WebErrorStatus.CertificateExpired => "The site's security certificate has expired.",
            CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect =>
                "The site's security certificate does not match its address.",
            CoreWebView2WebErrorStatus.CertificateRevoked => "The site's security certificate was revoked.",
            CoreWebView2WebErrorStatus.CertificateIsInvalid => "The site's security certificate is invalid.",
            _ => $"The connection failed ({status})."
        };
        _errorDescription.Text = $"{url}\n{detail}";
        _runtimeLink.Visibility = Visibility.Collapsed;
        _retryButton.Visibility = Visibility.Visible;
        WebViewErrorPanel.Visibility = Visibility.Visible;
        WebView.Visibility = Visibility.Collapsed;
    }

    private async void OnPermissionRequested(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        var permission = e.PermissionKind switch
        {
            CoreWebView2PermissionKind.Camera => "camera",
            CoreWebView2PermissionKind.Microphone => "microphone",
            CoreWebView2PermissionKind.Geolocation => "location",
            CoreWebView2PermissionKind.Notifications => "notifications",
            _ => null
        };
        if (permission is null)
        {
            e.State = CoreWebView2PermissionState.Deny;
            e.SavesInProfile = false;
            return;
        }

        var deferral = e.GetDeferral();
        var gateAcquired = false;
        try
        {
            e.State = CoreWebView2PermissionState.Deny;
            e.SavesInProfile = false;
            await SecurityDialogGate.WaitAsync();
            gateAcquired = true;
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var origin)
                || origin.Scheme is not ("http" or "https")
                || App.MainWindow?.RootGrid.XamlRoot is not { } xamlRoot) return;
            var isCurrentSite = Uri.TryCreate(_tab?.Url, UriKind.Absolute, out var currentAddress)
                && string.Equals(currentAddress.GetLeftPart(UriPartial.Authority),
                    origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);

            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = $"{origin.Host} wants to use your {permission}",
                Content = new TextBlock
                {
                    Text = $"Allow {origin.GetLeftPart(UriPartial.Authority)} to access {permission}?",
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = ThemeBrushes.UiFont
                },
                PrimaryButtonText = "Allow once",
                SecondaryButtonText = isCurrentSite ? "Always allow" : string.Empty,
                CloseButtonText = "Block",
                DefaultButton = ContentDialogButton.Close
            };
            var choice = await dialog.ShowAsync();
            e.State = choice is ContentDialogResult.Primary or ContentDialogResult.Secondary
                ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
            e.SavesInProfile = isCurrentSite && choice == ContentDialogResult.Secondary;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Permission prompt failed: {ex}");
            e.State = CoreWebView2PermissionState.Deny;
            e.SavesInProfile = false;
        }
        finally
        {
            if (gateAcquired) SecurityDialogGate.Release();
            deferral.Complete();
        }
    }

    private void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (_disposed || e.ProcessFailedKind is not
            (CoreWebView2ProcessFailedKind.BrowserProcessExited
            or CoreWebView2ProcessFailedKind.RenderProcessExited
            or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)) return;

        var kind = e.ProcessFailedKind;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || _tab is null) return;
            if (kind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive
                && DateTimeOffset.UtcNow < _ignoreUnresponsiveUntil) return;

            _needsRecovery = true;
            _tab.IsLoading = false;
            _tab.HasLoadedSuccessfully = false;
            _tab.NavigationFailed = true;
            _errorTitle.Text = kind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive
                ? "This tab is not responding" : "This tab stopped working";
            _errorDescription.Text = kind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive
                ? "You can wait for the page or recover the tab. Recovering reloads the page and may lose unsaved input."
                : "The browser process stopped. Recover this tab to reload its page. Your tabs remain in the session.";
            _runtimeLink.Visibility = Visibility.Collapsed;
            _retryButton.Content = "Recover tab";
            _retryButton.Visibility = Visibility.Visible;
            _waitButton.Visibility = kind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive
                ? Visibility.Visible : Visibility.Collapsed;
            WebViewErrorPanel.Visibility = Visibility.Visible;
            WebView.Visibility = Visibility.Collapsed;
        });
    }

    private async System.Threading.Tasks.Task RestoreScrollAsync()
    {
        if (_tab?.SavedScrollPosition is not { } scroll) return;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(scroll);
            var x = json.RootElement.GetProperty("x").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
            var y = json.RootElement.GetProperty("y").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
            await WebView.ExecuteScriptAsync($"window.scrollTo({x}, {y})");
            _tab.SavedScrollPosition = null;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Scroll restore failed: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task ApplyZoomAsync()
    {
        if (!_webViewReady || _tab is null) return;
        try
        {
            var factor = _tab.ZoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await WebView.ExecuteScriptAsync($"document.documentElement.style.zoom = '{factor}'");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Zoom failed: {ex.Message}");
        }
    }

    private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || _needsRecovery || _tab is null) return;
            _tab.IsLoading = true;
            _tab.HasLoadedSuccessfully = false;
            _tab.NavigationFailed = false;
            var url = sender.Source;
            if (_tab.Url != url) _tab.Url = url;
        });
    }

    private void OnDocumentTitleChanged(CoreWebView2 sender, object e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_tab is null) return;
            _tab.Title = string.IsNullOrEmpty(sender.DocumentTitle) ? FormatDisplayUrl(sender.Source) : sender.DocumentTitle;
            HistoryService.Instance.UpdateDetails(sender.Source, _tab.Title, _tab.FaviconUrl);
        });
    }

    private void OnFaviconChanged(CoreWebView2 sender, object e)
    {
        var url = sender.FaviconUri;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_tab is null) return;
            _tab.FaviconUrl = url;
            HistoryService.Instance.UpdateDetails(sender.Source, sender.DocumentTitle, url);
        });
    }

    private async void OnDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs e)
    {
        _downloadNavigationPending = true;
        var deferral = e.GetDeferral();
        try
        {
            var fileName = Path.GetFileName(e.ResultFilePath);
            if (BrowsingSafety.NeedsDownloadConfirmation(fileName))
            {
                if (App.MainWindow?.RootGrid.XamlRoot is not { } xamlRoot)
                {
                    e.Cancel = true;
                    return;
                }
                await SecurityDialogGate.WaitAsync();
                try
                {
                    var source = Uri.TryCreate(e.DownloadOperation.Uri, UriKind.Absolute, out var uri)
                        ? uri.Host : "an unknown site";
                    var dialog = new ContentDialog
                    {
                        XamlRoot = xamlRoot,
                        Title = "Download an application?",
                        Content = $"{fileName} from {source} can run code on your computer. Only download it if you expected this file.",
                        PrimaryButtonText = "Download",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Close
                    };
                    if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                finally { SecurityDialogGate.Release(); }
            }
            var folder = App.Store.Settings.DownloadFolder;
            if (App.Store.Settings.AskWhereToSaveDownloads)
            {
                var picker = new Windows.Storage.Pickers.FolderPicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads
                };
                picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker,
                    WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!));
                var choice = await picker.PickSingleFolderAsync();
                if (choice is null) { e.Cancel = true; return; }
                folder = choice.Path;
            }
            DownloadService.Instance.HandleDownload(e, folder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download setup failed: {ex}");
            e.Cancel = true;
        }
        finally { deferral.Complete(); }
    }

    private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (BrowsingSafety.IsWebAddress(e.Uri))
            DispatcherQueue.TryEnqueue(() => App.Store.AddTab(e.Uri));
        else if (BrowsingSafety.CanOfferExternalLaunch(e.Uri, e.IsUserInitiated))
            DispatcherQueue.TryEnqueue(() => _ = ConfirmExternalLaunchAsync(e.Uri, sender.Source));
    }

    private void OnLaunchingExternalUriScheme(CoreWebView2 sender,
        CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        e.Cancel = true;
        if (BrowsingSafety.CanOfferExternalLaunch(e.Uri, e.IsUserInitiated))
            _ = ConfirmExternalLaunchAsync(e.Uri, e.InitiatingOrigin);
    }

    private async Task ConfirmExternalLaunchAsync(string address, string? source)
    {
        if (App.MainWindow?.RootGrid.XamlRoot is not { } xamlRoot) return;
        await SecurityDialogGate.WaitAsync();
        try
        {
            var from = Uri.TryCreate(source, UriKind.Absolute, out var origin)
                && origin.Scheme is "http" or "https" ? origin.Host : "This page";
            var target = new Uri(address);
            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Open another app?",
                Content = $"{from} wants to open a {target.Scheme} link in another app.\n\n{address}",
                PrimaryButtonText = "Open app",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"External link launch failed: {ex}");
        }
        finally { SecurityDialogGate.Release(); }
    }

    public async System.Threading.Tasks.Task<bool> CanSleepAsync()
    {
        if (!_webViewReady) return true;
        try
        {
            var result = await WebView.ExecuteScriptAsync("""
                (() => {
                    if ([...document.querySelectorAll('audio,video')].some(m => !m.paused && !m.ended)) return false;
                    if ([...document.querySelectorAll('input,textarea')].some(e => e.value !== e.defaultValue || ('checked' in e && e.checked !== e.defaultChecked))) return false;
                    if ([...document.querySelectorAll('select')].some(e => [...e.options].some(o => o.selected !== o.defaultSelected))) return false;
                    return !document.querySelector('[contenteditable="true"]');
                })()
                """);
            return result == "true";
        }
        catch { return false; }
    }

    public async System.Threading.Tasks.Task<bool> SleepAsync()
    {
        if (_tab is null || !_webViewReady || WebView.CoreWebView2 is not { } core) return false;
        try
        {
            return await core.TrySuspendAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not suspend tab: {ex}");
            return false;
        }
    }

    public async System.Threading.Tasks.Task<bool> CaptureScrollAsync()
    {
        if (_tab is null || !_webViewReady) return false;
        try
        {
            var position = await WebView.ExecuteScriptAsync("({ x: window.scrollX, y: window.scrollY })");
            using var json = System.Text.Json.JsonDocument.Parse(position);
            if (json.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !json.RootElement.TryGetProperty("x", out var x)
                || !json.RootElement.TryGetProperty("y", out var y)
                || x.ValueKind != System.Text.Json.JsonValueKind.Number
                || y.ValueKind != System.Text.Json.JsonValueKind.Number)
                return false;
            _tab.SavedScrollPosition = position;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not save tab scroll position: {ex}");
            return false;
        }
    }

    private async System.Threading.Tasks.Task WakeAsync()
    {
        if (_tab is null) return;
        SleepPanel.Visibility = Visibility.Collapsed;
        WebView.Visibility = Visibility.Visible;

        if (!_webViewReady) return;
        if (WebView.CoreWebView2 is { IsSuspended: true } core) core.Resume();

        _tab.IsSleeping = false;
        await System.Threading.Tasks.Task.CompletedTask;
    }

    public void Dispose()
    {
        _disposed = true;
        if (_tab is not null) _tab.PropertyChanged -= OnTabPropertyChanged;
        _tab = null;
        try { WebView.Close(); }
        catch (Exception ex) { Debug.WriteLine($"Closing a failed tab: {ex}"); }
    }

    private void ShowSleepPanel()
    {
        SleepPanel.Visibility = Visibility.Visible;
        WebView.Visibility = Visibility.Collapsed;
    }

    private static string FormatDisplayUrl(string? url)
    {
        if (string.IsNullOrEmpty(url) || url == "bow:newtab") return string.Empty;
        if (System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri)) return uri.Host;
        return url;
    }

}
