# Third-party notices

BOW's source is licensed under MIT. Bundled dependencies retain their own
licenses; BOW's MIT license does not replace those terms.

The release archive includes dependency notices in `licenses/`, copied from
the resolved NuGet packages and runtime packs. These include Microsoft's
Windows App SDK redistribution terms, .NET runtime notices, and the
CommunityToolkit.Mvvm and NuGet.Versioning licenses. Review those files when redistributing BOW.

NuGet.Versioning is Apache-2.0 licensed. Its license and copyright notice are
maintained in `docs/licenses/` and explicitly copied into the release's
`licenses/nuget.versioning/` directory because the package declares a license
expression without supplying a license file.

Native interaction tests use FlaUI (MIT) and the ASP.NET Core test-page server.
The test runner is development tooling and is not included in BOW's release ZIP.

Microsoft Edge WebView2 Evergreen Runtime is installed separately. The archive
includes Microsoft's signed Evergreen Bootstrapper, which downloads and installs
the runtime from Microsoft when needed. Microsoft's terms apply to that installer
and runtime; BOW's MIT license does not apply to them. See Microsoft's
[WebView2 distribution documentation](https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution).

Dependency versions are declared in `src/BOW/BOW.csproj`; the build resolves
and records the complete dependency graph in `src/BOW/obj/project.assets.json`.
