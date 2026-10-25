---
title: '2.5 Apply outcome, retention and cleanup rules to local runs'
type: 'feature'
created: '2026-10-24'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '654d2299fbba59ea1998c0a2791e5b928131e38f'
builds_baseline_commit: '3052386abb54ff111f498a8f6424300ea7cdda93'
story_key: '2-5-apply-outcome-retention-and-cleanup-rules-to-local-runs'
context:
  - '_bmad-output/implementation-artifacts/epic-2-context.md'
  - 'docs/contracts/test-environment-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete the local declared-suite lifecycle: successful environments clean themselves up; failed environments remain available for debugging; developers can list them and explicitly clean them safely.

## Boundaries & Constraints

Apply the published v1 policy/result semantics in both local source and package modes. Preserve source/Debug, package/Release, authoritative mapping, readiness and independent deadlines. Commit the first outcome before cleanup or optional archival; later cancellation, cleanup errors and retries cannot replace its cause/time or erase failure retention. Owner cancellation is `cancelled`; reserve `withdrawn` for future attached runs.

Retain surviving resources after test/startup failure and startup/execution timeout until explicit `down --run-id`. Cancelled runs without prior failure clean up. Cleanup is bounded, retryable and exact-owner only; preserve separately started Aspire applications and peer runs. Keep results and classified, redacted diagnostics outside deleted workspaces. Unknown observations stay explicit.

Add `hexalith-module list --output human|json` for tracked environments, showing owner/environment identity, age, failed runs, retention, pending cleanup and recorded attachment/deadline fields. Admission remains unsupported: show empty/unavailable attachment information honestly. Existing `down --run-id` is the explicit stop/cleanup operation.

Exclude attachment admission/withdrawal orchestration, CI workflow changes, candidate acceptance, domain fixture migration, publication and pin changes. Preserve CI's existing cleanup policy.

## I/O & Edge-Case Matrix

| State | Required behavior |
| --- | --- |
| Local success | Persist `passed`, then automatically delete owned resources. |
| Test/startup failure or timeout | Persist `failed`; retain survivors, including partial startup. |
| Cancellation before/after failure | Record `cancelled` if first; otherwise keep failure and retention. |
| Retained or pending cleanup | List owner, stable age, failure IDs, disposition and cleanup status. |
| Explicit down/retry | Delete only owned resources; repeated success remains safe. |
| Deletion/discovery/recording failure | Report exact known leftovers and uncertainty; preserve retry state; never report complete. |
| Environment gone | Valid result, inspected reports and redacted diagnostics remain accessible. |

</frozen-after-approval>

## Code Map

Paths `B/` = `references/Hexalith.Builds/`; `R/` = `B/src/libraries/Hexalith.Builds.Tooling/Runtime/`; `T/` = `B/test/Hexalith.Builds.Module.Tests/`.

- `R/ModuleCommandExecutionService.cs` already coordinates retention/cleanup; retention currently requires source mode and exact-down drops leftover details.
- `R/CompositionEngine.cs` preserves first outcomes under `outcome.lock`. Reuse teardown/scanning and per-entry archives. Its terminal readiness reread can block on a FIFO; do not carry that defect into immediate recording.
- `R/CompositionRunStateStore.cs` lacks enumeration; `CompositionRunState.cs` lacks stable creation time. `R/ModuleTestRunRecord.cs` is internal metadata, not the v1 result producer.
- `B/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentDescriptorWriter.cs` supplies immutable bindings; the existing contract reader validates results. Current scanner covers containers/processes; workspaces and claims are separately owned.

## Tasks & Acceptance

**Execution:**

- [x] `R/CompositionRunPlanFactory.cs`, `R/CompositionRunState.cs`, `R/CompositionRunStateStore.cs` — preserve stable request/run/attempt/environment identities and creation time before provisioning; enumerate validated states, with honest legacy unknowns.
- [x] `R/ModuleCommandExecutionService.cs`, `R/CompositionEngine.cs`, `R/NativeTestExecutor.cs`, `R/ModuleTestRunRecord.cs` — enforce the matrix, record promptly from runner-held observations, preserve first outcomes and failure retention through explicit-stop races.
- [x] New `B/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestResultWriter.cs` — atomically publish validated `test-result.json`, reusing descriptor bindings or truthful partial-startup bindings; persist classified diagnostic references and evolving termination/retention/cleanup without fabricating proof.
- [x] `R/CompositionRunResourceScanner.cs`, `R/CompositionRunResources.cs`, `B/src/libraries/Hexalith.Builds.Tooling/Diagnostics/ToolCommandResult.cs`, `B/src/libraries/Hexalith.Builds.Tooling/Diagnostics/ToolDiagnosticFormatter.cs` — expose resource/workspace/claim leftovers and discovery failures in human/JSON output; release ownership only after verified deletion.
- [x] `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, new `R/CompositionRunListing.cs` — add read-only listing, independent of manifest drift, using durable identity and disposition.
- [x] New `T/LocalRunLifecycleTests.cs`, `T/TestResultWriterTests.cs`, `T/CompositionRunListingTests.cs`; existing `T/CompositionEngineTests.cs`, `T/ModuleRunnerReviewRegressionTests.cs`, `T/CompositionRedactionTests.cs` — cover every matrix row, cross-process outcome/cleanup races, bounded FIFO handling, partial-startup results and secret-bearing inputs.
- [x] New `tests/Hexalith.Platform.Composition.Tests/PlatformLocalRunLifecycleTests.cs`, extend `eng/test-environment-provisioning/suite.py` — exercise real success cleanup, failed/partial-startup retention, explicit cleanup/retry and preservation of an independently started peer; inspect actual resources and retained results.
- [x] `B/README.md`, `docs/contracts/test-environment-v1.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — document commands/result locations and synchronize completion with retained evidence under `evidence/2-5/`.

**Acceptance Criteria:**

- Given any matrix state, when its lifecycle transition completes, then command output, durable first outcome and observed resources agree.
- Given a deleted environment, when its result is read, then the v1 reader accepts it and referenced diagnostics/reports still exist without credentials.
- Given a peer application, when this run cleans up, then the peer remains running and its data is unchanged.

## Implementation Notes

- Implemented stable issued identities and request age, prompt first-outcome recording, local source/package failure retention, exact cleanup with leftovers/uncertainty, read-only listing and validated durable v1 results. Existing inspected per-entry reports and authority gates remain in use.
- Parent acceptance audit found that HXR021 is shared by task failure and startup expiry. Startup recording now carries the observed cause explicitly; producer tests and the live proof assert both causes. The rejected live observation and its failing offline audit are retained separately.
- Parent verification: Debug Builds and Platform builds pass without warnings/errors; 504 affected Builds tests pass without skips/errors/failures. Fresh live cause qualification and workflow review remain pending. Package runtime qualification remains unavailable until authoritative release declarations are published.

- Parent final implementation verification: 504 affected tests and the fresh four-scenario real-service proof pass. The offline audit verifies all seven matrix rows, current source fingerprints, archived/runtime byte equality, diagnostic hashes and owned cleanup. Independent workflow review follows.

- Final reviewed verification: 534/534 affected Builds tests, one four-scenario real-service proof (278.032 seconds), and relocated-evidence audit pass. Both Debug builds have zero warnings/errors. All 21 individual review/audit findings are corrected; nothing is deferred. Final source fingerprints, immutable runtime/archive fields, diagnostic hashes and exact-owner cleanup audits pass. Package runtime qualification remains unavailable under the existing authority gate.

## Spec Change Log

## Review Triage Log

### Independent workflow review, 2026-10-24

All three layers completed before triage. Each finding was graded individually before grouping; the two verification gaps are accepted on their filed evidence. Sixteen findings form fourteen patch groups. The corrections use existing private state, locking, publication and formatting paths, add no public surface, and guard demonstrated states.

| Finding | Verdict | Route and verified evidence |
| --- | --- | --- |
| B1 | medium | Patch: The non-null disposition branch permits complete to become in-progress; concurrent down callers record before acquiring the provisioning lease and can recreate tracking. |
| B2 | medium | Patch: DownCore reads only active state while RecordTestOutcome already uses owner.json; missing/malformed active JSON skips durable cleanup updates despite successful deletion. |
| B3 | medium | Patch: The result is marked complete before DeleteStateAfterCleanupAsync; a false return or lock failure writes incomplete only to active state, leaving inconsistent dispositions. |
| B4 | high | Patch: New FindOwned traversal calls synchronous File.OpenRead and unbounded JSON parsing; a FIFO blocks before cancellation can be observed. Existing nonblocking bounded primitives are available. |
| B5 | medium | Patch: RemoveAsync overwrites its last observation with a failed-discovery result, so known container IDs disappear without verified absence. |
| B6 | high | Patch: RecordTestOutcome rewrites native.trx and functional-observations.json with non-atomic byte writes after those files are referenced by published hashes; interruption can corrupt retained results. |
| B7 | medium | Patch: A callback exception prevents NativeTestExecutionResult from returning its timestamp; the service then starts a fresh stopping allowance instead of using the original observation. |
| B8 | medium | Patch: Typed JSON deserialization accepts a null nested terminalOutcome; enumeration checks only top-level fields, and listing dereferences it outside the structured refusal handler. |
| B9 | low | Patch: Human formatting omits the documented attempt identity while JSON carries it; this is a direct output correction. |
| B10 | low | Patch: The referenced lifecycle diagnostic persists discoveryUnverified but drops the producer discovery categories; the versioned result cannot explain which observation failed. |
| B11 | medium | Patch: The advertised offline audit calls exists on the original absolute temporary result paths, so retained evidence alone cannot be audited from another checkout. |
| E1 | medium | Patch: The external harness reproduced complete becoming in-progress and tracking reappearing after a stale cleanup callback; initial down records before lease acquisition make the overlap reachable. |
| E2 | medium | Patch: Both DownCore state reads can observe an expired shared stopping token; the initial read lost its exception handler, so bounded cleanup throws rather than returning retryable uncertainty. |
| E3 | medium | Patch: The external harness removed active state and reproduced successful empty cleanup with the surviving result still not-requested/failure-retained; durable owner metadata is available. |
| V1 | medium | Patch: Pre-verified gap: restoring the source-only command condition escapes source-only command cases and package engine tests that force RetainOnFailure=true. |
| V2 | medium | Patch: Pre-verified gap: deleting the executor snapshot assignment still yields reader-valid partial results, and no result assertion compares populated bindings to the original descriptor. |


### Parent patch audit

| Finding | Verdict | Route and verified evidence |
| --- | --- | --- |
| R1 | high | Patch: Patch audit: requiring CreatedAt/ExecutionContext rejected legitimate ordinary run/debug and legacy owner snapshots. InitializeState intentionally leaves test context unavailable for those runs. Optional identity validation preserves ownership without inventing fields. |
| R2 | high | Patch: Full post-review run: FailedBookkeepingStillStopsOwnedProcessAsync(diagnostics) loses HXT008 and exact-owner teardown because cleanup-lock directory publication fails before the sweep. The current bound must still protect best-effort runtime deletion and return recording failure. |
| R3 | low | Patch: DownStopsOnlyTaggedResourcesAsync uses literal workspace w, which is invalid under the existing exact-owner path invariant. Correct the fixture path while retaining tagged/foreign process assertions. |
| R4 | low | Patch: Three non-retaining selected startup tests require active tracking after verified cleanup. Their persistent summary, first cause and original deadline must instead be asserted through retained v1 results after tracking deletion; legacy assertions remain. |

### Final timing-test correction

| Finding | Verdict | Route and verified evidence |
| --- | --- | --- |
| R5 | low | Patch: A full run exposed the token-at-delegate-entry timing assertion as scheduler-sensitive. A 500 ms cancellable operation distinguishes the original exhausted allowance from a renewed full allowance; the original timestamp and elapsed assertions remain. Five repeated focused runs passed. |

## Design Notes

No unresolved intent gaps. Dispatch spans runner persistence, CLI and contract output. Only disposable run-owned data is deleted during verification; no deployment or hosted-data mutation. Reuse existing inspected archives; package runtime qualification remains dependent on published authoritative artifacts.

## Verification

Build Builds Module.Tests and Platform Composition.Tests individually in Debug with `dotnet build <project> -c Debug -m:1`. Invoke built xUnit assemblies with `-class` filters for the listed tests, selection/startup and report-redaction regressions; require no unexpected failures/skips. Run schema parity and the gated real-service lifecycle proof. Retain exact commands, full revisions, parsed results, resource/peer audits and cleanup receipts; report unavailable package/live prerequisites separately.

### Review Findings

Implementation group only (Builds source, `docs/contracts/test-environment-v1.md`, `eng/test-environment-provisioning/suite.py`, `PlatformLocalRunLifecycleTests.cs`), 2026-10-25. Builds tests, spec/sprint text, the evidence archive, `README.md`, and `startup-verify.sh` were not in this diff.

- [ ] [Review][Patch] Unreadable runner documents are treated as missing and overwritten, so a locked or malformed outcome can be replaced [CompositionDocumentStore.cs:68]
- [ ] [Review][Patch] A later archive or cleanup-recording failure leaves the command failed while the durable first outcome stays passed [NativeTestExecutor.cs:281]
- [ ] [Review][Patch] Explicit down stores an empty unverified remainder before teardown, dropping the last known leftover ids [CompositionEngine.cs:1126]
- [ ] [Review][Patch] Partial startup publishes an empty resource list instead of an unobserved binding [TestResultWriter.cs:235]
- [ ] [Review][Patch] A preserved outcome still absorbs later rule ids, so success cleanup cites HXC130 and down omits the result path [CompositionEngine.cs:633]
- [ ] [Review][Patch] Cancelling the executor token commits owner cancellation, and the later execution-lifetime correction does not replace it [NativeTestExecutor.cs:420]
- [ ] [Review][Patch] The first and inspected outcome callbacks run with an already-cancelled budget token and throw before the outcome is committed [NativeTestExecutor.cs:407]
- [ ] [Review][Patch] One unverifiable claim file makes cleanup incomplete for every run [CompositionResourceClaimStore.cs:135]
- [ ] [Review][Patch] Human list hides leftovers, enumeration accepts an attempt id equal to the environment id, and one bad state file hides every environment [ToolDiagnosticFormatter.cs:73]
- [ ] [Review][Patch] Legacy evidence marks workspace or claim leftovers unverified and then omits their ids [ModuleRunEvidenceWriter.cs:261]
- [ ] [Review][Patch] The last workspace-delete attempt can report the directory remaining after it has gone [CompositionWorkspace.cs:71]
- [ ] [Review][Patch] Owner cancellation is still asserted as withdrawn, so restoring that status would not fail the normal suite [SelectedObservationTests.cs:359]
- [ ] [Review][Patch] Non-hex docker output is not asserted separately from a retained container id [CompositionRunResourceScanner.cs:168]
- [ ] [Review][Patch] A symlinked run workspace is not covered by a deletion test [CompositionWorkspace.cs:52]
- [ ] [Review][Patch] Secret-bearing plan and readiness copies are not refused by a test [CompositionEngine.cs:934]
- [x] [Review][Defer] Off-Linux process discovery is never marked unverified by a normal test [CompositionRunResourceScanner.cs:34] — deferred: the normal lane is Linux and there is no OS seam, so a Windows-only fact would be skipped the same way the existing Windows skips are
- [x] [Review][Defer] The post-initialize state read is null-forgiving [CompositionEngine.cs:301] — deferred, unverified medium: confirm whether a null read immediately after InitializeStateAsync is recorded as a startup failure or escapes as a null dereference

#### Rejected

- `false` — Early HXT007 does not clean up a completed test failure. A nonzero exit is committed before report inspection, and cancellation is first only when the executor token is already cancelled.
- `false` — Off Linux, cleanup stays incomplete because the scan is unverified. Discovery failures are paired with `Unverified`, so `IsEmpty` cannot treat that scan as complete.
- `false` — `Directory.Delete` throws when the directory remains. A true return follows a successful delete or a missing directory.
- `low` — The gated live proof's extra package, hash, termination, and credential assertions, and its `finally` exception swap, are outside everyday runs. Package qualification is already an acknowledged boundary, and preserving the original exception is more than a direct correction.
- `false` — `suite.py` hits `assert report_path` before writing the report, so a missing report path is not a KeyError. Lifecycle profiles write their own Redis keys and receipt.
- `false` — A late callback does not resurrect a removed owner. The outcome lock re-reads the record, and a complete cleanup disposition skips the state write.
- `false` — `File.Delete` does not throw after the validation temp has been moved, and the writer is closed before validation, so that delete does not hide `InvalidDataException`.
- `false` — A missing native report path in `suite.py` fails the assert. It does not raise KeyError.
- `low` — The live proof's root walk fails only when `apphost.cs` is absent, which already stops that gated test. A typed exception adds a branch for a layout this proof does not run in.
- `false` — Startup recording does not drop published candidate proof. Partial bindings keep candidate `not-yet-populated`, which this story does not populate.
- `false` — Termination stays `not-yet-populated` because this diff's contract says an unauthenticated stopping observation must not be fabricated.
