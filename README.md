# BOW

BOW is a Windows browser built with WinUI 3, .NET 8, and WebView2.

## Build

On Windows, install the .NET 8 SDK and Windows SDK, then run:

```powershell
dotnet build src/BOW/BOW.csproj -c Debug -p:Platform=x64
dotnet test src/BOW.Tests/BOW.Tests.csproj -c Debug
```

The browser executable is produced under `src/BOW/bin/x64/Debug/`.

## Browsing data

BOW stores tabs, history, and download records as JSON in `%LOCALAPPDATA%\BOW`.
Before saving URLs, it removes user-info credentials and common authentication
parameters such as `code`, `access_token`, and `session_id`. Existing JSON files
are sanitized when loaded. This is a best-effort filter; websites can use other
parameter names or put secrets in URL paths. Settings > Privacy & browsing can
clear selected browsing history, site data, cache, and download records for a
chosen time range from both WebView2 and BOW's local history. It does not delete
downloaded files, saved passwords, autofill, or the current tab session.

Tracking protection can be set to Off, Basic, Balanced (default), or Strict.
Idle background tabs can unload to free memory after the configured interval
and reload when reopened; if unloading is unavailable, WebView2 is suspended.
Tabs playing media stay awake; individual sites can be
excluded from automatic sleep in Settings or the tab menu. Tab changes are
saved after a brief pause, and the current session is saved again on close.

Drag a tab onto another tab to reorder it. Drag an inactive tab onto the active
tab, or into the page area, to preview split view and open it beside the current
page. The page-area targets allow left or right placement. Closing split view
keeps both tabs open.
Use Ctrl+Shift+2 to leave split view while keeping both tabs. Ctrl+W closes the
focused pane's tab; its partner remains open.
Press Ctrl+Y to maximize the window while keeping the toolbar and tabs visible;
press it again to restore the window. Shortcuts can be changed in Settings.
Ctrl+A goes back and Ctrl+D goes forward in the focused tab's page history.
