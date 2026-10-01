# Third-party notices

BOW's source is licensed under MIT. Bundled dependencies retain their own
licenses; BOW's MIT license does not replace those terms.

The release archive includes dependency notices in `licenses/`, copied from
the resolved NuGet packages and runtime packs. These include Microsoft's
Windows App SDK redistribution terms, .NET runtime notices, and the
CommunityToolkit.Mvvm license. Review those files when redistributing BOW.

Microsoft Edge WebView2 Evergreen Runtime is installed separately and is not
included in BOW's release archive. Its terms are provided by Microsoft.

Dependency versions are declared in `src/BOW/BOW.csproj`; the build resolves
and records the complete dependency graph in `src/BOW/obj/project.assets.json`.
