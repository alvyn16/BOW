# Desktop baseline

Measured October 1, 2026 using a Release x64 build and the fixed 1,000-row
offline workload described in PERFORMANCE.md. Windows 10.0.26200.0,
12 logical processors, WebView2 154.0.4258.37; three independent fresh-profile runs.
These are local synthetic measurements, not promises for other machines or sites.

| Metric | Run 1 | Run 2 | Run 3 |
| --- | ---: | ---: | ---: |
| Startup to first ready page (ms) | 896.4 | 867.4 | 874.9 |
| Warm switch median (ms) | 11.8 | 11.7 | 11.3 |
| Warm switch p95 (ms) | 26.1 | 18.9 | 24.4 |
| Private memory, 1 loaded tab (MiB) | 244.2 | 243.7 | 241.7 |
| Private memory, 10 loaded tabs (MiB) | 558.8 | 573.7 | 564.6 |
| Private memory, 30 loaded tabs (MiB) | 1233.2 | 1252.4 | 1249.2 |
| Private memory after sleeping 29 tabs (MiB) | 582.3 | 560.9 | 582.8 |
| Private memory reclaimed from immediately before sleep (MiB) | 815.2 | 846.3 | 824.7 |

The 30-tab loaded snapshot precedes the warm switches. Reclaimed memory is
compared with a separate sample immediately before sleeping, so subtracting the
earlier loaded snapshot from the after-sleep value is not the same measurement.
All samples include BOW and WebView2 processes. After sleeping, six processes
remained and the active tab passed its responsiveness check in all runs.

No performance optimization was applied in this task. The first instrumentation
attempt queried a closed tab's environment after unloading and undercounted memory;
those runs were rejected. The retained harness queries the surviving active tab's
environment and fails when runtime process enumeration is empty.

Keep raw reports in `TestResults/performance-verified` locally. Run the script
again to establish a comparable baseline before optimizing; do not compare this
desktop baseline directly with hosted GitHub runners.
