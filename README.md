# BOW

BOW is a Windows browser built with WinUI 3, .NET 8, and WebView2.

## Build

On Windows, install the .NET 8 SDK and Windows SDK, then run:

```powershell
dotnet build src/BOW/BOW.csproj -c Debug -p:Platform=x64
dotnet test src/BOW.Tests/BOW.Tests.csproj -c Debug
```

The browser executable is produced under `src/BOW/bin/x64/Debug/`.
