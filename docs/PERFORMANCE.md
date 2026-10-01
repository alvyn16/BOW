# Repeatable performance measurements

Build BOW in Release mode using README.md, then run:

```powershell
./scripts/Measure-Performance.ps1 -Executable C:\path\to\BOW.exe -Runs 3 -ReportDirectory TestResults/performance
```

The benchmark opens the actual BOW window and uses the normal tab activation,
WebViewHost, and manual tab-sleep/unload paths. It uses fresh, isolated settings,
history, session, download records, and WebView2 profiles in the report directory.
It never loads the normal browsing profile. The fixed offline workload is a
1,000-row HTML page; no website or internet response time is part of the workload.
Each invocation creates a new profile. Profiles are retained for inspection;
they contain only synthetic benchmark data.

## Metrics

- Startup: managed entry point through first page readiness and two UI rendering
  callbacks. This excludes native process launch before `Main`; it is not a full
  cold-boot or installed-session startup measurement.
- Memory: aggregate private bytes and working set of BOW plus WebView2 processes
  after loading 1, 10, and 30 tabs. Working sets can double-count shared pages;
  prefer private bytes when comparing memory use. Samples are taken after a fixed
  settling delay, not a claim of peak or steady-state memory.
- Tab switch: 30 warm switches through the real tab store/content layout, two
  rendering callbacks, and a renderer responsiveness check. Reports include raw
  samples, median, and p95. This is a synthetic responsiveness measurement,
  not hardware-level screen presentation latency.
- Sleep: memory before and three seconds after manually sleeping 29 background
  tabs. The active tab must remain responsive. Negative reclaimed bytes are
  reported honestly rather than clamped to zero.

Every JSON run records the workload, runtime, OS, processor count, isolated
profile, and raw results. `summary.json` records timing medians and ranges.
Failures and timeouts fail the script rather than becoming successful samples.
Do not run the benchmark against an older executable without `--benchmark`.

## Comparisons

Use the same hardware, display refresh rate/scaling, power mode, OS, WebView2
runtime, Release configuration, and run count. Close unrelated busy applications.
Alternate baseline and candidate runs where possible; compare at least three
samples, their ranges, and their per-stage memory results. First-run caches and
background activity can affect results even with fresh browser profiles.
Do not claim an improvement smaller than observed variation.

The Windows workflow offers an optional benchmark on manual dispatch and uploads
its reports. Hosted runners are useful for reproducibility checks, not direct
comparison with desktop hardware. No arbitrary latency/RAM gate is imposed until
enough runs establish a representative baseline. A reported regression should
be reproduced before changing code. This task adds measurement tooling; it does
not claim a performance optimization.

Record measured baselines and optimization attempts in PERFORMANCE-BASELINE.md.
