# Install BOW

BOW preview releases support Windows x64. The project targets Windows 10
version 1809 or newer and Windows 11; startup is automatically checked on
Windows Server 2022 in CI. Other Windows versions have not yet been tested.

## Portable download

1. Download the `BOW-<version>-win-x64.zip` archive from
   [GitHub Releases](https://github.com/alvyn16/BOW/releases).
2. Extract the entire archive to a folder you can write to, such as
   `%LOCALAPPDATA%\Programs\BOW`. Keep all files and subfolders together.
3. Install Microsoft's [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
   if it is not already installed.
4. Open the extracted folder and run `BOW.exe`. No .NET SDK, separate .NET
   runtime, or separate Windows App SDK runtime installation is needed.

The preview executable is not code-signed. Windows may display an unknown
publisher warning. Verify the source and checksum before deciding to run it.

To verify the download, compare the SHA-256 value from the adjacent `.sha256`
file with:

```powershell
Get-FileHash .\BOW-0.1.0-preview.1-win-x64.zip -Algorithm SHA256
```

## Update or remove

Close BOW before updating. Extract a newer archive into a new folder and run
its `BOW.exe`; keep the previous folder until the new version works.

BOW's JSON history, settings, download records, and session are stored in
`%LOCALAPPDATA%\BOW`. WebView2 keeps cookies and site sign-ins in a separate
browser profile. Moving the application folder may change the default profile
location. Back up the current application folder and `%LOCALAPPDATA%\BOW` with
BOW closed before updating. BOW has no automatic updater.

To remove the portable application, close it and delete its extracted folder.
This does not delete downloaded files or the JSON data in `%LOCALAPPDATA%\BOW`.
Use Settings > Privacy & browsing before removal to clear selected data, or
remove the JSON data folder separately if you also want to discard it.

## Troubleshooting

- If BOW reports that WebView2 is missing, install the Evergreen Runtime and reopen BOW.
- If the application does not open, extract the complete ZIP again; do not copy only `BOW.exe`.
- Avoid running from a ZIP or a protected folder such as `Program Files`.
- Report startup failures with the release version, Windows version, and error message at
  [BOW Issues](https://github.com/alvyn16/BOW/issues). Do not include browsing data or credentials.
