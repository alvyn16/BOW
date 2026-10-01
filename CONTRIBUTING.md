# Contributing to BOW

BOW is a Windows x64 WinUI 3 browser. Read README.md for the required .NET
and Windows SDKs, build commands, and portable release verification.

## Changes

Open an issue describing the problem before a large change. For fixes, create a
branch, keep the change focused, and open a pull request targeting `main`.
Include the behavior before/after, test results, and screenshots for UI changes.
Add regression tests where possible. Do not commit generated builds, browser
profiles, signing keys, tokens, or real browsing history.

## Verification

Run `dotnet test src/BOW.Tests/BOW.Tests.csproj -c Release` and
`dotnet build src/BOW/BOW.csproj -c Release -p:Platform=x64`.
For distribution changes, build the ZIP and run the extracted executable's
startup check as documented in README.md. For performance changes, use
docs/PERFORMANCE.md with the same machine, workload, and sample count before
and after; report variation, not just the fastest run.

Review correctness, privacy, accessibility, and performance before merging.
Do not call a preview stable solely because automated tests passed.

## Reports

Use the issue templates for bugs and feature requests. Report security concerns
privately according to SECURITY.md. Be respectful and include reproducible,
non-sensitive examples rather than personal browsing data.
