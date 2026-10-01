# BOW

BOW is a Windows browser built with WinUI 3, .NET 8, and WebView2.

## Download

Download the Windows x64 ZIP from [Releases](https://github.com/alvyn16/BOW/releases).
Extract the entire archive and run `BOW.exe` from a writable folder.
The portable package includes .NET and Windows App SDK dependencies; it needs
the [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
installed separately. Preview builds are unsigned.
See [installation, updates, and troubleshooting](docs/INSTALL.md).

## Build

On Windows x64, install the .NET SDK selected by `global.json` and the Windows
SDK (including `makepri.exe`), then run:

```powershell
dotnet build src/BOW/BOW.csproj -c Debug -p:Platform=x64
dotnet test src/BOW.Tests/BOW.Tests.csproj -c Debug
```

The browser executable is produced under `src/BOW/bin/x64/Debug/`.

## Release and verification

Build a portable, self-contained Release ZIP and SHA-256 file:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Release.ps1
```

The archive is written to `dist/`. Extract it before running the startup check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Startup.ps1 -Executable C:\path\to\extracted\BOW.exe
```

The startup check loads WinUI resources, initializes WebView2 with a temporary
profile, and verifies an offline test page. It does not open the regular browser
session and is not a substitute for testing navigation and browser interactions.

GitHub Actions runs unit tests, builds the portable ZIP, and verifies startup
for pushes to `main` and `codex/**`, and pull requests targeting `main`.
To publish another release, update `<Version>` in `src/BOW/BOW.csproj`, add
`docs/releases/v<version>.md`, merge the change, then push the matching `v<version>`
tag. The tag workflow publishes the release only after these checks pass.
Versions containing a suffix, such as `-preview.1`, are marked as prereleases.

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
Alt+Left goes back and Alt+Right goes forward in the focused tab's page history.

## License

BOW source is licensed under [MIT](LICENSE). Bundled dependencies retain their
own terms; see [third-party notices](THIRD-PARTY-NOTICES.md) and the `licenses/`
folder in release archives.
