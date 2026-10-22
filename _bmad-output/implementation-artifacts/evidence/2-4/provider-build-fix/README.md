# Composition provider build isolation correction

`BuildModelAsync` now supplies `--artifacts-path <workspace>/build/platform-model`. The existing `--output <workspace>/hosts/platform-model`, copied provider assembly path, verified catalog, source/Debug and package/Release selection, and original remaining startup deadline are unchanged. Independent builds remain concurrent; no shared-output retries were added.

The Debug test-project build passed with zero warnings/errors. The focused `PlatformModelBuildIsolationTests` passed **2/2** in 11.648 seconds. Each case runs two actual concurrent three-project builds and checks all restore/intermediate/output paths, source filesystem stability, catalog observations, configuration and copied assemblies. Exact commands and the two added source-review paths are in `verification.json`.

The failed parent live receipts and logs under `../parent-final/live/` were preserved unchanged, including the successful cleanup audit. Full regressions and a fresh serial live proof remain parent verification work. No pins, schemas, frozen spec or unrelated source files changed.
