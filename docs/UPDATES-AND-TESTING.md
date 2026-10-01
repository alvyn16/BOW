# Update checks, browser interactions, and .NET 10

## Scope

These changes implement roadmap items 3, 2, and 1: safe release checking,
automated browser interaction coverage, and a supported .NET 10 build.
They build on the session recovery and benchmark work in PR #3; that PR remains
separate and must be merged first when landing this branch.

## Acceptance

- Settings shows the full installed release version, stable/preview channel,
  a manual check, optional background checking, and loading/error/current/newer
  states. Checks use only BOW's public GitHub releases over HTTPS, ignore drafts,
  compare semantic versions, and offer a trusted repository release-page link.
  No credentials, browser URLs, downloaded executables, or automatic installation
  are involved. Background checks are off by default and bounded in frequency.
- Tests exercise actual WinUI/WebView2 browser navigation, tab reordering and
  drag-to-split routes, split exit/close, window maximization and page fullscreen,
  keyboard commands, downloads, and selected browsing-data clearing. All automated
  browser runs use isolated profiles and synthetic local pages/data. Evidence
  must distinguish command-path checks from real pointer/keyboard input tests.
- App and test projects target .NET 10, global.json pins a stable SDK, CI uses
  that SDK, the portable ZIP is self-contained, and the extracted build passes
  startup and browser interaction checks. Existing regression tests stay green.

## Build Order

1. Migrate app/tests/SDK and verify build, unit tests, and packaged startup.
2. Add release-checking service with deterministic tests, then its settings UI.
3. Add isolated native browser interaction coverage and CI reports.

## Verification Commands

```powershell
dotnet test src/BOW.Tests/BOW.Tests.csproj -c Release
dotnet build src/BOW/BOW.csproj -c Release -p:Platform=x64
./scripts/Build-Release.ps1
./scripts/Test-Startup.ps1 -Executable C:\path\to\extracted\BOW.exe
./scripts/Test-Interactions.ps1 -Executable C:\path\to\extracted\BOW.exe
```

Native tests require an unlocked Windows desktop and take control of the mouse
and keyboard while running. Do not interact with the desktop during the run.
The runner uses FlaUI UI Automation, physical pointer packets and keyboard input,
with synthetic pages served on loopback and a new temporary browser profile.
The read-only test snapshot observes split-pane geometry because WebView2's UI
Automation tree may expose only the last of two visible browser panes.
Reports and screenshots are written to `TestResults/interactions`; profile data
is not uploaded by CI. CI always builds this runner; execution is an opt-in
`workflow_dispatch` input because unattended runners may not have an interactive
desktop. A successful build or smoke test does not count as interaction coverage.

Update checks are off by default. Opting in contacts GitHub at most once per
24 hours during startup; manual checks contact it on demand. GitHub receives the
network request and normal connection metadata, but no browsing history or
credentials. Preview checks inspect the latest 100 published releases, including
stable releases; stable checks use GitHub's latest stable release endpoint.
Rate limits, timeouts, and invalid responses display an error, not "up to date".
Updates are notifications and release-page links only, with no automatic install.

The portable installer and trusted code-signing
identity are outside this change; existing signing requirements still apply.

## Local Evidence

- .NET 10 build: zero warnings/errors; 85 regression tests passed, none skipped.
- Self-contained ZIP: extracted startup and all 11 native interaction scenarios
  passed, including focused split-tab closing and page fullscreen.
- One isolated performance sanity sample: startup 1100 ms, tab-switch median
  12.9 ms. This is not a statistically meaningful before/after comparison.
- Application and interaction-runner transitive dependency vulnerability checks
  reported no known vulnerable packages from NuGet's current advisory source.
