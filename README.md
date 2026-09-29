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
parameter names or put secrets in URL paths. Settings > Browsing > Clear browsing
history removes BOW's saved page visits. It does not clear website cookies, the
current tab session, downloaded files, or download records.

Idle background tabs use WebView2 suspension and resume without reloading. Tab
changes are saved after a brief pause, and the current session is saved again
when the window closes.
