# Story 2.4 round-2 corrections, 2026-10-23

All 32 round-2 patch items are implemented. [Patch-to-verification mapping](patch-to-verification.json) and [owned hunks](owned-hunks.json) distinguish this work from concurrent recovery/diagnostics changes. The diagnostics test's narrow CS8198 prerequisite repair is recorded separately.

| Verification | Result | Evidence |
| --- | --- | --- |
| Individual Builds and Platform Debug builds | Passed, zero warnings/errors | `builds-build.log`, `builds-test-only-build-final.log`, `platform-build.log` |
| Builds affected invocation | 539 passed, one failed, 540 total | `builds-affected.xml`, `builds-affected.log` |
| Corrected review class | 49 passed, zero failures/errors/skips | `builds-review-final.xml`, `builds-review-final.log` |
| Latest Builds coverage union | All 540 cases pass | `builds-coverage-union.json` |
| Final handoff / startup checks | 41 / 2 passed | `handoff-final.xml`, `startup-final.xml` |
| Platform affected checks | 41 passed, one separate opt-in occupied-listener skip | `platform-affected.xml`, `platform-affected.log` |
| Current environment/result schema mirrors | Byte-identical | `schema-parity.json` |
| Fresh real-service qualification | One passed in 101.140 seconds | `live-attempt-3/qualification.xml`, `live-attempt-3.log` |
| Independent parent acceptance audit | Schemas, intervals, isolation and cleanup verified | `parent-acceptance-audit.json` |

The Builds union is not a claim that one 540-case invocation passed. The full invocation's sole failure measured 1.5008334 seconds against a 1.5-second wall-clock assertion after synchronous OS process-tree termination. The test-only correction verifies that the expired original token prevents new durable state writes, retaining the original two-second overall and process-exit assertions. Its entire 49-case class passed afterward; production code did not change after the full invocation. Earlier fixture/assertion failures are retained in the initial logs/XML.

The positive run IDs are `0d85ae12fe7f68f5b60a7a3e8a8664be` and `eb2c5693981cc789b98e7c3bf490c78f`. Each observed six healthy resources and a passed `platform/verify-run-owner` task. Startup took 89.60 and 95.30 seconds, followed by the full configured 120-second execution interval for each entry. Both persisted the identical key through their own Redis endpoint, read their own run value, and could not observe the peer's unique key. `persisted` derives from the actual `SAVE` reply. Neither command emitted `HXR003`.

`live-attempt-3/diagnostics/<run>/` retains the runner's descriptor snapshot, execution timing, native report, immutable selection, readiness/tasks and mapping metadata. The gated fixture validates the archived snapshots and retains these observations before removing its private workspace. Each explicit cleanup succeeded at its first call. Exact-owner Docker/process scanning found no survivors, and all three attempted fixture workspaces are deleted; see `exact-owner-cleanup-audit.json` and the per-attempt cleanup receipts.

The first two live attempts refused before provisioning: the ambient Aspire CLI was 13.6.1 (`live/`, HXR015), then an older Dapr capsule lacked placement/scheduler (`live-final/`, HXR013). The positive attempt reused the existing private Aspire 13.6.3 CLI and complete Dapr 1.18.7 capsule. No global tool installation, dependency, catalog or pin changed. Exact commands, revisions, 58 source fingerprints, descriptor timing/data observations and the unchanged approved frozen fingerprint are retained in the JSON files.

Shared infrastructure requires an actual selected Platform declaration. The qualified technical closure includes it; ordinary EventStore, Tenants and Memories closures currently omit it and refuse descriptor handoff with an explicit prerequisite message. Their run/debug paths remain available. Package-mode live execution, CI orchestration, attachment admission and candidate acceptance are not qualified.

The initial read-only application inspection observed Tenants AppHost PID 1800389; it was absent at final inspection. Its external lifecycle cause is unconfirmed. No lifecycle command targeted it, and every proof cleanup used only the recorded proof run identities. Accordingly this evidence does not claim its continuous availability; see `independent-applications-after.json`.
