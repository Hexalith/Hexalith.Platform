# Story 2.3 round-3 review patches — 2026-10-23

All fifteen round-3 Patch findings are implemented. No production composition source or tool/catalog pins changed; the Works test harness now resolves credentials from the actual Sentry bind mount, including its HA replica setup. The added model checks cover both ordinary signed and file-entry selections.

| Check | Result | Evidence |
| --- | --- | --- |
| Normal Composition Debug build | Passed; zero warnings/errors; 20.28 s | [build](build-composition-final.log) |
| Focused patched matrix | Eight passed; zero failed/skipped; 1.118 s | [tests](tests-focused.log) |
| Affected ordinary Composition suite | 143 passed; zero failed; four expected live skips; 147 total; 52.308 s | [tests](tests-ordinary.log) |
| Normal Works integration Debug build | Passed; zero warnings/errors; 40.12 s | [build](build-works.log) |
| Works hosting/harness suite | 13 passed; zero failed/skipped; 0.233 s | [tests](tests-works-harness.log) |
| Occupied Docker Sentry publish boundary | One passed; zero failed/skipped; 2.410 s | [test](tests-occupied-port.log), [receipts/cleanup](occupied-port-cleanup.json) |

The first Composition build caught two test assertion overload errors; those were corrected before the clean build and passing tests above. Its [failed build log](build-composition.log) is retained. Ordinary-suite Redis Unhealthy messages are expected model-only health-check observations; the xUnit result has zero failures.

The frozen matrix rows executed here are: identical signed/file selections (private ports, certificates, discovery and storage); external certificate parents and checkout/symlink refusal, including existing and dangling per-run child links; selected-native-runtime refusal with retained diagnostics; and occupied Sentry binding refusal while a separately owned Docker listener and its marker survive candidate cleanup. All four sidecar listener ports are disjoint across the identical selections; rendered Redis components and mTLS Sentry addresses match their run resources. Actual native argument callbacks write metadata-only listener receipts. Persistence flags and store paths are asserted in the composed resources.

The occupied-port case exercises Docker's publish boundary, not an AppHost startup or forced occupied allocation. Its raw Docker output remains in the fixed [occupied-port directory](../occupied-port/), unaffected by the concurrent-evidence override. Both proof containers were removed by their own checked labels; no proof containers remain. No foreign application was stopped.

[Commands](commands.txt) and [current revisions/source hashes](source-snapshot.json) identify this patch verification. The six composition production files match HEAD byte for byte. The existing [six-server concurrent and file-entry qualification](../resume-20261023T065107Z/README.md) remains the live behavior evidence; these unchanged production paths were not rerun. The Works preview's deferred live endpoint qualification remains deferred; the passing harness tests do not extend that claim. Shared checkout build outputs are documented developer inputs, while runner test-suite project outputs remain private.
