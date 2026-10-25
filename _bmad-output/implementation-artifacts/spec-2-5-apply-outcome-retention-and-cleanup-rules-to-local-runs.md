---
title: '2.5 Apply outcome, retention and cleanup rules to local runs'
type: 'feature'
created: '2026-10-24'
status: 'done'
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


- 2026-10-25 review corrections: all 15 patch findings below are implemented. The final unchanged source passes all 584 affected Builds tests (142.164 seconds), both individual Debug builds with zero warnings/errors, current schema byte parity, and the relocated retained-evidence audit. Exact commands, full test XML, source fingerprints, individual patch mappings, and superseded observations are retained under `evidence/2-5/review-fixes-20261025/`. The command preserves its first terminal outcome while later archive/cleanup errors remain diagnostic and incomplete cleanup stays retryable. Fresh live verification was not run: Docker is unavailable in this WSL distribution and installed Aspire is 13.6.1 while the catalog requires 13.6.3. Package runtime qualification remains unavailable under the existing authority gate. The two explicit review deferrals remain; the story and sprint status stay `in-progress` for remaining review/qualification.

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

- [x] [Review][Patch] Unreadable runner documents are treated as missing and overwritten, so a locked or malformed outcome can be replaced [CompositionDocumentStore.cs:68]
- [x] [Review][Patch] A later archive or cleanup-recording failure leaves the command failed while the durable first outcome stays passed [NativeTestExecutor.cs:281]
- [x] [Review][Patch] Explicit down stores an empty unverified remainder before teardown, dropping the last known leftover ids [CompositionEngine.cs:1126]
- [x] [Review][Patch] Partial startup publishes an empty resource list instead of an unobserved binding [TestResultWriter.cs:235]
- [x] [Review][Patch] A preserved outcome still absorbs later rule ids, so success cleanup cites HXC130 and down omits the result path [CompositionEngine.cs:633]
- [x] [Review][Patch] Cancelling the executor token commits owner cancellation, and the later execution-lifetime correction does not replace it [NativeTestExecutor.cs:420]
- [x] [Review][Patch] The first and inspected outcome callbacks run with an already-cancelled budget token and throw before the outcome is committed [NativeTestExecutor.cs:407]
- [x] [Review][Patch] One unverifiable claim file makes cleanup incomplete for every run [CompositionResourceClaimStore.cs:135]
- [x] [Review][Patch] Human list hides leftovers, enumeration accepts an attempt id equal to the environment id, and one bad state file hides every environment [ToolDiagnosticFormatter.cs:73]
- [x] [Review][Patch] Legacy evidence marks workspace or claim leftovers unverified and then omits their ids [ModuleRunEvidenceWriter.cs:261]
- [x] [Review][Patch] The last workspace-delete attempt can report the directory remaining after it has gone [CompositionWorkspace.cs:71]
- [x] [Review][Patch] Owner cancellation is still asserted as withdrawn, so restoring that status would not fail the normal suite [SelectedObservationTests.cs:359]
- [x] [Review][Patch] Non-hex docker output is not asserted separately from a retained container id [CompositionRunResourceScanner.cs:168]
- [x] [Review][Patch] A symlinked run workspace is not covered by a deletion test [CompositionWorkspace.cs:52]
- [x] [Review][Patch] Secret-bearing plan and readiness copies are not refused by a test [CompositionEngine.cs:934]
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

### Resumed independent workflow review, 2026-10-25

All three reviewers completed before triage. Each finding received its own verdict before shared-root grouping. BH5/EH5 and EH3/EH6 share roots; all other accepted entries remain separate. The five low findings concern additional validation of historical no-credit artifacts, beyond the live producer assertions and current parent matrix audit.

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| BH1 | medium | Patch: RecordTestOutcomeAsync deletes active state before selected-test/result publication; a subsequent write failure returns incomplete from DownOwnedCoreAsync without restoring enumerable retry tracking. Preserve tracking on publication failure under the existing outcome lock. |
| BH2 | medium | Patch: Enumeration validates nested nulls but not TestRun.RunId/OwnerId, so a copied foreign record supplies retention and failed-run observations for the enclosing owner. Validate both identity bindings. |
| BH3 | medium | Patch: ReportStartupTimeoutAsync with null runId publishes only before-run summary even for a selected suite; ReportSetupTimeoutAsync directly reaches it. Reuse issued-state and terminal-recording paths for selected pre-allocation expiry. |
| BH4 | medium | Patch: ExecuteExactDownAsync normalizes a deserialized null Workspace before the engine validates ownership; ArgumentNullException bypasses structured results. Delegate ownership validation to the engine or guard normalization. |
| BH5 | medium | Patch: The live proof finally loop has no catch around DownAsync or receipt writes, so one failure skips later owners. Attempt every owner and aggregate failures after cleanup. |
| BH6 | low | Rejected: The historical portable audit explicitly audits selected retained observations and grants no qualification credit; a complete schema-validation extension for deliberately altered historical artifacts adds new validation machinery beyond a direct correction in an occasional audit. |
| BH7 | low | Rejected: The archived XML has a recorded fixed count and identities in separate receipts; the portable audit promises selected checks and no qualification credit. Adding negative inventory validation for substituted historical transcripts is additional occasional-audit machinery. |
| BH8 | low | Rejected: The historical matrix receipt names the executed passing cases, and the current parent audit independently reconciles all seven rows with final XML. Hardening the no-credit historical audit against substituted matrix receipts adds guards beyond a direct everyday correction. |
| BH9 | low | Rejected: The live producer asserts resource survival before cleanup and retains its observations; the offline script reports recorded final cleanup checks without claiming to rerun retention or granting qualification. Additional historical cross-artifact validation is an occasional-audit extension. |
| BH10 | low | Rejected: The live proof checks peer container identities and Redis value directly. The offline audit reports the retained sentinel observation with no qualification credit; reconstructing all historical ownership relationships adds occasional-audit validation beyond direct correction. |
| EH1 | medium | Patch: Confirmed standalone probe at /tmp/story25-edge-probe/Program.cs: initial owner.json plus durable outcome with known container, no active state, and failed Docker discovery yields an empty container remainder. Load the durable first outcome before recording cleanup in-progress. |
| EH2 | medium | Patch: The post-StartAsync missing-state fallback writes without the outcome lock or cleanup marker check. Exact down can finish between ready-session return and this fallback, recreating an owner whose complete outcome then skips cleanup. Guard initialization using existing lock/markers. |
| EH3 | medium | Patch: New ordinary mapping-refusal tracking deletion checks HXR023, while CleanupPreparedWorkspaceAsync reports HXW009. The newly added deletion loses prior discoverability on failed workspace removal; preserve state and return its run identity. |
| EH4 | medium | Patch: IsOwnedState accepts null resource arrays; subsequent failed discovery reaches Concat(previous.ContainerIds) and throws ArgumentNullException. The reviewer reproduced this with the built assembly. Refuse malformed nested arrays before teardown. |
| EH5 | medium | Patch: Same root cause as BH5: an exception in one final cleanup/receipt prevents all subsequent owner attempts. Group with BH5 after this individual verdict. |
| EH6 | medium | Patch: Same root cause as EH3: HXW009 never matches the new HXR023 deletion guard. Group with EH3 after this individual verdict. |
| VG1 | medium | Patch: Pre-verified regression gap: listing tests assert identity/age/cleanup/leftovers and the live proof asserts FailureRunIds, but no test asserts list retention. Add persisted terminal cases for failure-retained, released, not-retained and unavailable through both output formats. |
| P1 | medium | Patch: Parent trace: RecordTestOutcomeAsync commits selected-test.json before optional artifact/result/metadata writes; a later callback exception can leave its caller record null. Repeated recording failure then chooses retention from the later command error rather than the durable first pass. Recover the committed observation independently of optional work and cover command-level recording failure. |

### Parent broad verification after follow-up patches

The first full post-patch lane ran 598 cases: 595 passed, three failed, no errors/skips/unrun cases. Both Debug builds passed without warnings/errors. The failed run is retained under `evidence/2-5/review-fixes-20261025/pre-final-parent/`.

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| PV1 | medium | Patch: ExactDownDoesNotRequireManifestRecognitionAsync(foreign) returns topology 3 instead of the existing ownership usage refusal 1 after CLI ownership prevalidation was removed. Restore the structured refusal safely. |
| PV2 | medium | Patch: Guarded fallback issuance waits on the pre-held outcome lock, so the existing outcome-callback failure regression never reaches execution and cannot assert its original stopping timestamp. Preserve both synchronization safety and callback-budget coverage. |
| PV3 | medium | Patch: In the full lane, the execution-expiry regression records test-failure instead of execution-timeout. Trace the source/gate/cancellation path and retain the cause assertion. |

### Final resumed-build result

All accepted review patches and all three parent verification corrections are implemented. Final unchanged-source verification passes 598/598 Builds cases (146.897 seconds), 1/1 Platform cleanup-helper case, both individual Debug builds without warnings/errors, current schema parity and the retained-evidence audit. `evidence/2-5/review-fixes-20261025/final-parent/` retains exact commands, full XML, all seven matrix rows, 30 source fingerprints and the per-finding passing tests. The historical 595/598 run remains retained and is superseded.

The build spec is done and sprint entry moves to review. Fresh real-service and package qualification remain unavailable for the recorded Docker/Aspire and authoritative-artifact prerequisites. The two pre-existing deferrals remain; no accepted finding from this resumed review is left unfixed.

### Review Findings — committed-state review (2026-10-25)

All four configured review layers completed: Blind Hunter, Edge Case Hunter, Verification Gap Reviewer, and Acceptance Auditor. Each of the 20 observations below received an individual verdict before grouping. The result is **0 decisions, 13 patch items, 1 pre-existing deferral, and 2 rejected suggestions**. Patch items comprise three high and ten medium findings. No implementation changes were made in this review.

Disposition: the user selected **Leave as action items** (option 2). All 13 patch findings remain unchecked; none were applied. Story and sprint tracking are `in-progress` because the accepted findings remain unresolved.

The reviewed diff is `/tmp/bmad-review-2-5-z0ggtmxn.diff`: 45 files, 3,802 additions, 281 deletions. Builds is reviewed from `3052386abb54ff111f498a8f6424300ea7cdda93` through `d434d4a789661c48aaad3ddfba42f383240e8a1a`; Platform uses this spec's baseline and only its six scoped contract/proof files. Concurrent Platform commits did not change the reviewed bytes: the complete diff still passed reverse-apply verification at Platform `1b7a00381794bb7ecf40caf643c63c50758a81eb`.

Verification: the individual Builds Debug build passed with zero warnings/errors; all 598 scoped tests passed, with no failures, errors, skips, or unrun cases, in 212.734 seconds. Both Platform/Builds schema mirrors are byte-identical. Logs and full XML are `/tmp/bmad-review-2-5-build.log`, `/tmp/bmad-review-2-5-tests.log`, and `/tmp/bmad-review-2-5-tests.xml`. Fresh live-service and package qualification were not run; these checks grant no new qualification credit.

#### Patch items

- [x] [Review][Patch] R1 — **high**: Require a reader-valid v1 result before automatic environment deletion [ModuleCommandExecutionService.cs:1267]. Recovery currently obtains only `selected-test.json` after `test-result.json` publication fails, then enables cleanup. The passing `CommandRecoversCommittedPassAfterOptionalRecordingFailureAsync` regression deliberately blocks the result path with a directory and asserts exit 0 plus workspace deletion. That behavior violates AC2 and the required validated-result-before-cleanup order; preserve the first outcome while making result publication a prerequisite for automatic deletion.
- [x] [Review][Patch] R2 — **high**: Commit startup outcomes even when the original stopping allowance is exhausted [CompositionEngine.cs:1690]. The readiness deadline tail can exhaust `observationBound` before `TryRecordStoppingOutcomeAsync`, and every subsequent outcome write uses that cancelled token. The probe observed neither `selected-test.json` nor `test-result.json`. Use a separate finite metadata commit allowance without renewing resource stopping, diagnostics processing, or cleanup.
- [x] [Review][Patch] R3 — **high**: Preserve published attempt/environment identities through initialization versus exact-down races [CompositionEngine.cs:165]. State is published before the provisioning lease. Exact down can delete active tracking before package startup reaches its second `InitializeStateAsync`, which reissues identities and overwrites `owner.json`. Cleanup markers prevent lasting resource resurrection, but the later terminal rewrite changes the published result's attempt/environment identity. Reuse durable issued identities and honor completed cleanup before reinitializing.
- [x] [Review][Patch] R4 — **medium**: Preserve known container leftovers when the first cleanup scan is interrupted [CompositionEngine.cs:1319]. Cancellation before the first Docker observation leaves only `cleanup-verification`; the prior-ID merge tests only for `containers`. The probe returned an empty container list despite a recorded 64-character container ID and unverifiable deletion. Keep prior IDs until a successful observation verifies absence.
- [x] [Review][Patch] R5 — **medium**: Reconcile listing with committed outcomes after publication failure [CompositionRunListing.cs:24]. A result-publication exception occurs after `selected-test.json` commits and before the active-state update. Listing then reports success, no failed owner IDs, unavailable retention and null leftovers despite an authoritative retained failure. Read the committed record or report the unreconciled entry as incomplete while preserving other owners.
- [x] [Review][Patch] R6 — **medium**: Persist execution activation for listing while the suite is still running [NativeTestExecutor.cs:310]. Timing is written only to the session and disposable workspace until terminal recording. `list` reads `state.TestRun.ExecutionTiming`, so it cannot show an already-known active execution deadline. Persist and expose the runner-held activation before child launch.
- [x] [Review][Patch] R7 — **medium**: Preserve later archive/cleanup errors in durable diagnostics [CompositionEngine.cs:715]. `prior.DiagnosticRuleIds` freezes the first rule list and drops later `HXT008`/`HXR023` errors. A probe recorded incomplete cleanup with `HXR023`, but the referenced lifecycle diagnostic still had `ruleIds: []`. Keep terminal cause/rules immutable while retaining subsequent error diagnostics separately; do not introduce synthetic owner-cancellation causes into passed results.
- [x] [Review][Patch] R8 — **medium**: Reject null entries in persisted resource observations before teardown [CompositionEngine.cs:1130]. `ValidOutcome` checks that `Resources` exists but accepts `[null]`. The probe made exact down throw `NullReferenceException` at `TestResultWriter.cs:60` before removing owned resources. Validate nested entries and return structured retryable uncertainty.
- [x] [Review][Patch] R9 — **medium**: Finalize owners on every refusal after initial state publication [CompositionEngine.cs:181]. Descriptor refusal and final manifest-revalidation refusal clean the prepared workspace and return `RunId = null`, leaving newly initialized active tracking without its terminal outcome. Preserve the issued identity in the response and finalize or retain its tracking according to the observed cleanup result.
- [x] [Review][Patch] R10 — **medium**: Keep incomplete completion transactions discoverable across process interruption [CompositionEngine.cs:695]. Active tracking is deleted before the completed internal record and v1 result are published. Process termination during those asynchronous writes bypasses exception recovery and makes the unfinished completion disappear from active-state-only listing. Retain discoverable retry metadata until durable completion publication succeeds.
- [x] [Review][Patch] R11 — **medium**: Provide a path to verified cleanup on supported Windows/macOS hosts [CompositionRunResourceScanner.cs:34]. The new non-Linux `Unverified` flag is permanent; every retry remains nonempty and claims cannot be released. Aspire host resolution explicitly supports these hosts. Preserve honest uncertainty while implementing ownership verification that can establish completed cleanup on supported hosts.
- [x] [Review][Patch] R12 — **medium**: Assert independently activated listing deadlines in both CLI formats [CompositionRunListingTests.cs:24]. Pre-verified regression gap: all listing fixtures omit `TestRun.ExecutionTiming`, and no listing assertion checks its deadline. Substituting the earlier selection expiry for actual activation would pass the inspected tests. Add exact human/JSON assertions with activation later than selection, plus unavailable behavior before activation. This verification defect is separate from R6's missing live persistence.
- [x] [Review][Patch] R13 — **medium**: Apply the committed outcome before exporting legacy evidence [ModuleCommandExecutionService.cs:979]. `WriteResultAsync` writes legacy evidence before restoring `selectedTestRun.TerminalOutcome` in command output. The probe produced command `completed`/exit 0 while the written legacy artifact said `failed`/exit 3 after a committed pass followed by cleanup failure. Preserve the same first terminal status/outcome in both outputs and keep cleanup errors diagnostic.

#### Deferred

- [x] [Review][Defer] D1 — **medium**, pre-existing: Claim transaction contention can outlast the shared stopping allowance [CompositionResourceClaimStore.cs:231]. `ReleaseOwned` synchronously waits up to two seconds on `transaction.lock` without the stopping token. Both the teardown call and wait implementation predate this diff; the reviewer withdrew it as a regression finding. Track cancellation-aware claim operations separately.

#### Individual triage before grouping

| Observation | Source | Verdict | Route and verified evidence |
| --- | --- | --- | --- |
| BH1 | Blind Hunter | high | R2: the exhausted startup observation token prevents both terminal documents; reproduced. |
| BH2 | Blind Hunter | medium | R6: activation updates the workspace/session, while listing reads only terminal state. |
| BH3 | Blind Hunter | medium | R7: later `HXR023` is absent from the referenced diagnostic; parent probe confirmed it. |
| BH4 | Blind Hunter | medium | R9: the new initialization precedes early-return branches that neither finalize active state nor return its ID. |
| BH5 | Blind Hunter | high | R3: corrected after checking cleanup guards; lasting resurrection is prevented, but published identity mutation is reproduced. |
| BH6 | Blind Hunter | medium | R8: `[null]` passes ownership validation and crashes exact down in result publication. |
| BH7 | Blind Hunter | medium | R10: deletion precedes asynchronous completion publication, and process termination bypasses catch-based restoration. |
| BH8 | Blind Hunter | medium | R11: source inspection proves non-Linux scans can never become verified; host resolution supports Windows/macOS. |
| BH9 | Blind Hunter | low | Rejected: cancellation during a short read-only list can omit JSON, but adds no lifecycle-state risk and is uncommon; extra response machinery is disproportionate. |
| BH10 | Blind Hunter | medium | D1: the bounded-but-token-independent claim wait is real, but both call and wait predate this change. |
| BH11 | Blind Hunter | low | Rejected: extending an occasional live matrix goes beyond a direct correction; the ordinary lane covers these orderings and qualification limits remain explicit. |
| EH1 | Edge Case Hunter | high | R2: the startup probe returned `selectedTestExists=false`, `resultExists=false`, exit 3. |
| EH2 | Edge Case Hunter | medium | R4: known container `c` repeated 64 times became `returnedContainers=[]` with `cleanup-verification`, exit 6. |
| EH3 | Edge Case Hunter | medium | R8: exact down raised `NullReferenceException` in `TestResultWriter.cs:60`. |
| VG1 | Verification Gap Reviewer | medium | R12: accepted as pre-verified; no activated deadline fixture/assertion exists in the searched listing coverage. |
| VG2 | Verification Gap Reviewer | medium | R7: recorder freezes rules before `TestResultWriter` generates the retained diagnostic. |
| VG3 | Verification Gap Reviewer | high | R2: caller/test/probe confirm the native metadata allowance does not cover startup recording. |
| AA1 | Acceptance Auditor | high | R1: the passing command regression deletes the workspace with the v1 result path blocked, violating AC2. |
| AA2 | Acceptance Auditor | medium | R5: an external harness committed failed/retained while listing returned completed/no failures/unavailable retention. |
| P1 | Parent verification | medium | R13: the private command writer probe exported failed/3 and rendered the same invocation completed/0. |

Probe sources: `/tmp/edge-lifecycle-repro.91HGEL/Program.cs` (R2/R4/R8), `/tmp/blind-race-repro/Program.cs` (R3), `/tmp/acceptance-audit-25/Program.cs` (R5), and `/tmp/bmad-review-2-5-parent-probe/Program.cs` plus `/tmp/bmad-review-2-5-parent-probe.log` (R7/R13). Probes used disposable `/tmp` state and the reviewed Debug Tooling assembly.

#### Rejected

- **BH9 — low:** Cancellation while listing can omit the JSON response. The operation is read-only, the timing is uncommon in everyday use, and adding cancellation-output branches is not warranted by the negligible harm.
- **BH11 — low:** Additional live cancellation/startup-timeout scenarios would extend occasional qualification machinery; ordinary tests cover the orderings, and this review does not grant new live or package qualification.


### Committed-state review fixes — resumed build, 2026-10-25

All thirteen accepted patch items R1–R13 are implemented. Parent inspection of the implementation diff and final XML confirms that all thirteen finding mappings and all seven matrix rows reference executed passing cases. The frozen intent and original baseline revisions are unchanged.

Final unchanged-source verification: 617/617 affected Builds cases pass (260.216 seconds), the Platform cleanup-helper passes 1/1, and both individual Debug builds have zero warnings/errors. Schema parity, relocated retained-evidence validation, 33 source fingerprints and the portable-PDB audit pass. All three Tooling PDBs match all 195 current source documents; independent Platform/Builds generated version stamps and runtime hashes are retained separately. The earlier failing and superseded observations remain archived. Exact commands, full XML and per-finding evidence are in [the verification archive](evidence/2-5/committed-review-fixes-20261025/verification.json), [finding mapping](evidence/2-5/committed-review-fixes-20261025/review-findings-verification.json) and [matrix audit](evidence/2-5/committed-review-fixes-20261025/matrix-audit.json).

The older post-initialize null-read deferral is resolved by returning the durable issued state directly. The ordinary-lane non-Linux observation gap is resolved by verified/unverified process-discovery tests. Native Windows/macOS queries remain unqualified on this Linux host. D1, cancellation-aware claim transaction contention, remains the pre-existing deferred item. Docker is now available (29.8.2); installed Aspire remains 13.6.1 against catalog 13.6.3, so no fresh live qualification is claimed. Package qualification still requires authoritative published release declarations. Independent workflow review follows.


### Final workflow review triage — 2026-10-25

All three configured layers completed before triage. Each observation was graded independently before grouping. The reviewed source diff is `/tmp/story25-workflow-review-b9q6w_i_/story-2-5.diff`; the complete baseline archive, including unrelated concurrent work and historical evidence, is retained alongside it.

| Finding | Verdict | Route and verified evidence |
| --- | --- | --- |
| CB1 | medium | Patch F1: RecordTestOutcomeAsync replaces known leftovers with the executor callback's empty unverified observation while cleanup is incomplete. Preserve known identities until verified absent. |
| CB2 | high | Patch F2: Automatic-cleanup validation calls ReadResult, whose File.OpenRead blocks on a FIFO before cancellation; the edge reviewer reproduced a 200 ms token still blocked at a 2-second external timeout. Reuse bounded nonblocking input. |
| CB3 | medium | Defer: recursive synchronous Directory.Delete can exceed its cancellation allowance. The exact recursive deletion is present at the original Builds baseline; this change did not introduce it. |
| CB4 | medium | Patch F3: New acquisition receipts serialize without a size check while new ReadReceipt refuses over 64 KiB. Roughly 1,000 identities produce a self-generated unreadable receipt. Keep publication and read bounds compatible before mutation. |
| CB5 | medium | Patch F4: Enumeration validates status/resources but accepts arbitrary cleanup strings and complete with nonempty/unverified leftovers. Refuse inconsistent durable lifecycle observations while preserving healthy owners. |
| CB6 | high | Patch F5: New Windows/macOS reads treat inspection denial as absent without proving a foreign/exited process. Preserve uncertainty for unresolved live inspection failures and establish foreign ownership before excluding a process. |
| CB7 | high | Patch F6: TestResultWriter uses ordinary publication into a supplied owner directory; replacing that directory with a link redirects its fixed diagnostic/result names outside ownership. Apply existing owned-destination checks to publication and temporary cleanup. |
| CB8 | medium | Patch F7: Native reader tests use a discovery override or synthetic bytes, so no native tagged child/peer lifecycle is exercised. Add a portable supported-host regression and preserve the explicit Linux-host qualification limit. |
| CB9 | medium | Patch F8: The live peer audit compares containers/data but not process identities; it would miss termination of peer AppHost/Dapr/API processes while Redis survives. Assert peer processes using its existing before/after scanner observations. |
| CB10 | low | Rejected, carried: same missing live cancellation/startup-timeout scenarios as prior BH11; the unchanged occasional live matrix and existing ordinary coverage still support that verdict. |
| CE1 | high | Defer: the reviewer reproduced a tagged non-dumpable Linux process omitted with verified emptiness. The entire Linux environment-read/exception behavior is unchanged at the original Builds baseline; track this scanner limitation separately. |
| CE2 | high | Patch F2: independently reproduced the FIFO input block at the same new cleanup-validation call as CB2. |
| CE3 | medium | Patch F9: the new immediate callback commits generic HXT007 for Started=false before Evaluate can emit existing prerequisite HXR030. Preserve that known prerequisite classification. |
| CE4 | medium | Patch F10: a timeout callback throwing before first commit escapes before NativeTestExecutionResult returns; the service then records a generic failure and loses execution-timeout. Preserve the runner-held causal observation for retry. No earlier durable timestamp exists when the first commit failed. |
| CE5 | medium | Patch F11: the new receipt-aware FindOwned enumerates files only, so replacing a receipt-recorded claim with a directory hides a still-blocking reservation. Preserve its identity and uncertainty until verified deletion. |
| CV1 | medium | Native reader coverage gap accepted as pre-verified. F7 adds the portable host test; expanding Windows/macOS CI jobs is rejected because frozen intent explicitly excludes CI workflow changes. This build continues to claim no Windows/macOS native runtime qualification. |

After individual triage, only CB2 and CE2 are grouped by their shared FIFO-reader defect. Other accepted observations remain separate patch items.

- [x] [Review][Patch] F1: preserve known leftovers across unverified late callbacks.
- [x] [Review][Patch] F2: refuse/block-proof FIFO terminal-result input before automatic cleanup.
- [x] [Review][Patch] F3: align claim receipt publication/read bounds atomically.
- [x] [Review][Patch] F4: validate durable cleanup states and completion invariants.
- [x] [Review][Patch] F5: preserve native process-inspection uncertainty.
- [x] [Review][Patch] F6: prevent linked diagnostic/result publication outside ownership.
- [x] [Review][Patch] F7: add a portable native-host tagged-child/peer regression, without CI changes.
- [x] [Review][Patch] F8: assert real-service peer process survival.
- [x] [Review][Patch] F9: preserve native launch prerequisite classification.
- [x] [Review][Patch] F10: preserve a causal terminal observation when its first callback fails.
- [x] [Review][Patch] F11: keep receipt-recorded claim directories discoverable.


### Final workflow verification — 2026-10-25

All thirteen resumed accepted fixes and all eleven additional workflow patch groups are implemented. Parent final verification passes **632/632** affected Builds cases (243.130 seconds), without failures/errors/skips/unrun cases, plus the Platform cleanup-helper **1/1**. Both individual Debug builds have zero warnings/errors. Schema parity, retained-evidence validation, all thirteen original finding mappings, all seven matrix rows, 49 unchanged source fingerprints and all three Tooling PDB/source bindings (195 documents each) pass. The separately executed native-host case has one expected Linux skip; its implementation is complete but no native runtime qualification is claimed. The strengthened live peer-process assertions compile; fresh real-service qualification remains unavailable under the Aspire mismatch. Package qualification still requires authoritative published release declarations.

The first parent full lane (631/632) and the first test correction remain archived. The sole full-lane failure was an obsolete HXT008 proxy for an exhausted metadata read; the published finite metadata allowance permits that read. The corrected regression verifies the original committed outcome and timestamp, no repeated resource cleanup, and no fabricated recording error. The parent also guards publication-inspection I/O failures/cancellation so they leave cleanup pending without discarding a committed outcome. No production source changed after the final source snapshot.

Exact commands, full XML, runtime/source identities, individual patch verification and the seven-row audit are retained in [final verification](evidence/2-5/final-workflow-review-20261025/verification.json), [review finding verification](evidence/2-5/final-workflow-review-20261025/review-findings-verification.json), and [matrix audit](evidence/2-5/final-workflow-review-20261025/matrix-audit.json). D1 and the two separately recorded pre-existing scanner/deletion limitations remain deferred. No accepted patch remains open.

Build workflow complete: the spec is `done`, sprint tracking is `review`, and Builds source/tests are committed locally as `b215213da90f7a25f7a181f8d4b394226148ac59`. The Platform completion commit records this tracking/evidence and its Builds gitlink. No push was performed by this workflow.

### Review Findings — full-story review (2026-10-25)

This pass reviewed the whole story diff: Builds `aece0123` (the rebased equivalent of the `3052386` baseline) through `b215213`, limited to the 45 files from the story's five commits, plus Platform `654d229`..`HEAD` for the six scoped contract and proof files. That is 51 files, 7,069 diff lines. All four layers completed: Blind Hunter (BH), Verification Gap (VG), Acceptance Auditor (AA) and Edge Case Hunter (EH). Each of the 68 observations got its own verdict before grouping. The result is **3 decision-needed, 14 patch, 0 defer and 40 rejected**. The earlier deferrals D1, CB3 and CE1 still stand.

Decision resolution (owner, 2026-10-25): **all three recommendations adopted**. Q1 → option (a), now patch P15. Q2 → option (a): the owner approves the in-place v1 amendment, since no fixture consumer has adopted v1 yet; the paragraph fix is patch P16. Q3 → option (c), deferred to Windows/macOS native qualification.

- [x] [Review][Decision] Q1 — Failures after the outcome no longer fail the command, the CI job or acceptance evidence (medium; AA2+AA4+AA5+VG5+BH8+BH18+EH22+EH23) — `WriteResultAsync` replaces the command status and outcome with the committed first outcome in every mode, CI included (`ModuleCommandExecutionService.cs:945,984`). The diff deliberately documents this ("they do not relabel a completed test"). Consequences:
  - A CI pass whose cleanup fails now exits 0 with status `completed`. The `ModuleTestSelectionTests` cleanup-throws and cleanup-unverified cases changed from 3 to 0.
  - A failed `--evidence` write after a pass exits 0.
  - Archive HXT008, including a symlink planted by the child process, no longer fails the run (`NativeTestExecutor.cs`, `TestEnvironmentProvisioningReviewTests`).
  - `ModuleRunEvidenceArtifactValidator` dropped `ResourceDisposition == "removed" && RemainingResources.IsEmpty` from the `completed` check, so G4P0/readiness acceptance accepts completed evidence that still has leftovers.
  - These conflict with the constraints "Preserve CI's existing cleanup policy" and "Exclude … candidate acceptance". BH8's other claim, that exported evidence cannot show cleanup, is refuted: `resourceDisposition` and `remainingResources` are still exported.
  - Options: (a) keep the first-outcome semantics for commands (AC1), but have the G4P0 and readiness acceptance validators require `resourceDisposition: removed` and an empty remainder for `completed` evidence; (b) make the command fail after a pass whenever cleanup, evidence or archiving fails, in CI or in every mode, while `test-result.json` keeps the first outcome; (c) accept the current behaviour as documented. G4P0 matches only `finalStatus`/exit code and has no CI requirement, so today it accepts runs with leftovers.
- [x] [Review][Decision] Q2 — The published v1 schemas were widened in place (medium; AA1) — `hexalith.test-environment.v1` and `hexalith.test-result.v1` (in Platform and their Builds mirrors) now accept `environment.resources: not-yet-populated`, and the reader skips its resource and readiness checks for that value. The Builds `hexalith.module-run-evidence.v1` `remainingResources` gained fields in place under `additionalProperties: false`. The contract itself says "Changed semantics need a new published version and explicit consumer adoption". The population paragraph at `docs/contracts/test-environment-v1.md:36-39` still names only composition and catalog, so the document now contradicts itself. Earlier published readers and pinned tools reject partial-startup results that carry the same schema id. Options: (a) as contract owner, record approval of the in-place v1 amendment, since no external consumer has adopted it yet, and fix the paragraph; (b) publish the widened shapes as new versions; (c) stop emitting unpopulated resources for partial startup.
- [x] [Review][Decision] Q3 — Same-user Windows processes the runner cannot inspect leave cleanup permanently unverified (medium; EH13) — `ReadWindows` treats an `OpenProcess(0x410)` denial as unverified unless the process belongs to a foreign SID or has exited (`CompositionProcessEnvironmentReader.cs:91-95`). The F5 fix made that choice. On a UAC host, any elevated process of the same user is unreadable from a medium-integrity runner, so every scan stays `Unverified`. `down` then never completes, claims are never released, and tracking is never deleted. This conflicts with R11's "path to verified cleanup on supported hosts". Options: (a) exclude processes whose token integrity is above the runner's (a non-elevated runner cannot have started them); (b) limit discovery to the descendants or job object of the recorded AppHost; (c) defer until Windows/macOS native qualification exists.
- [x] [Review][Patch] P1 — Survivors of a retained failure are recorded with the exhausted stopping token, so they are lost after timeouts (medium; EH2+EH26) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1301]. When `stopRemaining <= 0`, which happens when an execution timeout consumes the stop grace, observing and recording survivors runs on a cancelled token. Only HXT008 is reported, and the result and listing keep the empty, unverified placeholder. Use the finite metadata allowance, as the R2 fix does.
- [x] [Review][Patch] P2 — Explicit `down` cannot stop a non-test `up`/`run` that is still starting (medium; BH3) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1257]. `cleanup-request.json` is written only for selected suites. `StartAsync` holds `provisioning.lock` through readiness, so `down` waits 5 s and returns HXR023 "Owned provisioning is still stopping" while startup carries on. Write the marker for every owned run; the startup checks already honour it.
- [x] [Review][Patch] P3 — A failed `up` records stale cleanup state, so `list` shows `not-requested` with no leftovers (medium; BH4) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1786]. `FailAndTearDownAsync` → `WriteStateAsync` copies the previous `RemainingResources`/`CleanupDisposition` instead of the `remaining`/`clean` it just observed. Only selected runs get a later correction. Persist `complete`/`incomplete` and the remainder for non-selected runs.
- [x] [Review][Patch] P4 — A failed evidence or report export drops leftovers, the result path and earlier diagnostics (medium; AA3) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1559]. On success, `MergeEvidenceFailure` builds a new result that carries only `RunId` and the evidence diagnostic. The selected-run override then restores the pass, so the output loses `RemainingResources`, `TestResultPath` and HXR023/HXI002. Carry those fields and the earlier diagnostics through. The exit-code question is part of Q1.
- [x] [Review][Patch] P5 — `list` drops unverifiable owners without naming their run IDs (medium; BH5+EH11) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionRunListing.cs:35]. An invalid committed outcome, invalid activation timing or an unreadable state file triggers `continue` plus a generic HXR024. The run ID is known from the file name but is never reported, so `down --run-id` cannot be reached from `list`. Name the unverifiable owner IDs in the incomplete-listing diagnostic.
- [x] [Review][Patch] P6 — `down --run-id --evidence` without `--manifest` writes a made-up manifest path into the evidence (medium; BH12+EH17) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs:237]. `"unused-manifest-for-exact-run"` is passed to the evidence factory and writer. They record it as the invocation's `manifestPath` and resolve the evidence root from the current directory. Refuse `--evidence` without `--manifest` with a HXC001 usage refusal.
- [x] [Review][Patch] P7 — No test runs explicit `down` while a declared suite is still starting (medium; VG1) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1437]. Removing either marker check (`:381` or `:1437`) passes every test. Add a fake AppHost that never becomes ready, call `DownAsync` concurrently, and assert HXC130 `cancelled`, `down` Success and the workspace gone.
- [x] [Review][Patch] P8 — No test asserts the IDs recorded for retained survivors (medium; VG2) [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/LocalRunLifecycleTests.cs:36]. Deleting the survivor observation at `ModuleCommandExecutionService.cs:1301-1311` passes. Assert that `workspace:<runId>` appears in `retention.retainedResourceIds`, in `cleanup.leftoverResourceIds` and in the listing entry, for both `NativeOutcomeControlsLocalCleanupAsync(fails)` and `PackageCommandFailureEstablishesLocalRetentionAsync`.
- [x] [Review][Patch] P9 — No CLI test checks the `down --run-id` result path or the human-format leftovers (medium; VG3) [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/LocalRunLifecycleTests.cs:200]. Removing `testResultPath` at `ModuleCommandExecutionService.cs:1015` or deleting formatter lines 100-115 passes. Run `DiscoveryFailureReportsClaimsAndRetryPreservesPeerAsync` in both formats and assert `testResultPath`, `test-result=` and `leftover=claim:`.
- [x] [Review][Patch] P10 — Claim receipts are never deleted (low; BH1+EH15) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionResourceClaimStore.cs:127]. `Acquire` writes `claims/owners/<runId>.json` before acquiring. Neither `ReleaseOwned` nor the rollback after a conflict removes or rewrites it. Receipts therefore pile up after verified cleanup, and a conflicting identity the run never got stays listed as expected. Delete the receipt on release, and rewrite it to the prior identities on rollback.
- [x] [Review][Patch] P11 — A lifetime-exhaustion fallback records cause `test-failure` or `startup-failure` instead of `execution-timeout` (low; AA7) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1226]. The OCE fallback passes `native?.TerminalCause`, which is null because `native` is reset for each entry. `TestResultWriter.Cause` then guesses from `ExecutionTiming`. Pass `execution-timeout` for this known cause.
- [x] [Review][Patch] P12 — The reader's new non-blocking, no-follow and shared-lock behaviour is undocumented (low; AA12) [docs/contracts/test-environment-v1.md:215]. `ReadDescriptor`/`ReadResult` now refuse a symlinked final path component and a file held under an exclusive lock. Document this next to HXT105/HXT106.
- [x] [Review][Patch] P13 — `CompositionRunState.CompositionContentIdentity` is added but never assigned (low; BH21) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionRunState.cs:55]. Only the plan property of that name is set, so every state persists null. Remove the property.
- [x] [Review][Patch] P14 — No test covers `down` with neither `--manifest` nor `--run-id` (low; VG4) [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/ModuleCommandApplicationTests.cs:133]. Removing the check at `ModuleCommandApplication.cs:230-233` passes. Add `["down", "--output", "json"]` to the missing-option HXC001 cases.
- [x] [Review][Patch] P15 — Acceptance validators accept `completed` evidence that still has leftovers (medium; resolved from Q1 option a) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Evidence/G4P0AcceptanceValidator.cs:196]. The generic artifact validator now accepts local `completed` evidence with incomplete cleanup, which is correct under the documented semantics. G4P0 (and readiness at `ReadinessEvidenceValidator.cs:1031`) must also require `selectedTestRun.resourceDisposition == "removed"` and an empty `remainingResources` before accepting `completed`.
- [x] [Review][Patch] P16 — Bring the contract's population rule into line with the owner-approved in-place v1 amendment (medium; resolved from Q2 option a) [docs/contracts/test-environment-v1.md:36]. Name `environment.resources` next to composition and catalog as accepting the explicit `not-yet-populated` object, and record the owner-approved 2026-10-25 in-place amendment, including the Builds evidence `remainingResources` fields.
- [x] [Review][Defer] Q3 — Inaccessible same-user Windows processes keep cleanup unverified [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionProcessEnvironmentReader.cs:91] — deferred: Windows/macOS native runtime is unqualified and cannot be verified on this Linux host; settle during native qualification (owner decision, 2026-10-25).

#### Rejected

- BH2 — `low`: the HXR024 branch in `DownOwnedCoreAsync` is unreachable, but CLI exact-down already refuses mismatched active state with HXR024. Only corrupt `owner.json` ends up with a "retry" hint.
- BH6 — `low`: the spec does not require run status or a versioned list schema, and no harm is named.
- BH7 — `false`: the listing's "failed runs" and the result's `retention.failureRunIds` are intentionally different fields, and they diverge only for CI failures with leftover tracking.
- BH9/AA9/EH29 — `low`: `NewRunId()` and the fresh context are reached only when both `owner.json` and active state are missing, which happens only with the test start override, because `InitializeStateAsync` writes both before provisioning.
- BH10 — `low`: no fsync is a property of the pre-existing `CompositionDocumentStore` helper and matters only after an OS crash.
- BH11 — `low`: `CopySelectedMetadataAsync` is unchanged from baseline.
- BH13 — `low`: uncertainty is still explicit through `Unverified` and the cleanup disposition. Adding a reason would need new category vocabulary and an allow-list change.
- BH14 — `low`: the hard-coded 5 s allowances have no demonstrated harm, and making them configurable would add surface.
- BH15 — `low`: a missing `wtsapi32` is implausible on supported Windows. CI expansion is excluded, and F7's portable test exists.
- BH16/EH6 — `low`: the orphaned `diagnostics/<runId>` after a non-selected mapping refusal is invisible to `list`, which reads active state only. Fixing it needs lease restructuring.
- BH17 — `low`: cancellation and startup-timeout live scenarios were already rejected (BH11/CB10), package mode is gated, a Debug AppHost is the local rule, and evidence is retained by design.
- BH19 — `low`: reflection calls with positional arguments are a test-maintenance concern only.
- BH20 — `low`: every current category is on the writer's allow-list, so nothing is dropped today.
- AA6 — `low`: `failure-retained` persists only for `incomplete` explicit cleanup, which the cleanup field already shows. Choosing a reason value is a semantics decision for Story 2.6 attachment.
- AA8 — `low`: legacy `not-requested` shows up only right after an upgrade, and a nullable disposition would change types.
- AA10 — `low`: the logic that relabels a pass as cancelled before commit predates this diff (only renamed), and cleanup happens either way.
- AA11/EH8 — `low`: retaining refused and pre-allocation-expired selected owners is the deliberate R9/BH3 behaviour, covered by the drift scenarios in the test matrix.
- AA13 — `false`: a package-mode success cannot be faked without published artifacts. Diagnostic hashes and redaction are asserted in `TestResultWriterTests` and in the retained-evidence audit, and secret-bearing copies are covered by an earlier patch.
- EH1 — `low`: HXT007 relabelling after a cleanup claim needs `down`'s own first-outcome commit to fail.
- EH3 — `low`: an unreadable active state mid-startup is corruption-only.
- EH4 — `low`: it needs legacy identities plus a state-delete failure, and the durable record still ends `incomplete`.
- EH5 — `low`: it needs an HXW009 workspace failure plus a state I/O failure, and the fix adds guards.
- EH7 — `low`: it needs `InitializeStateAsync` I/O failure during timeout reporting, and the fix adds guards.
- EH9 — `low`: the gate stays open briefly after the child has stopped, and closure diagnostics still reach later rule IDs.
- EH10 — `low`: by design the commit does not renew the stopping budget, and only a slow filesystem consumes it.
- EH12 — `low`: a transient spurious incomplete listing during a concurrent `down` clears on retry.
- EH14 — `low`: it applies only to a 32-bit runner on 64-bit Windows, and the dotnet host is x64.
- EH16 — `low`: `docker rm` failing to start right after `docker ps` succeeded is implausible, and the propagation is mostly pre-existing.
- EH18 — `low`: a selected package-mode descriptor that is unavailable reports status `failed` with the correct exit code. The committed record also says `failed`, so a one-line override cannot fix it.
- EH19 — `low`: a time-of-check race. `down` commits `cancelled` first, so the outcome stays correct.
- EH20 — `low`: it needs both `owner.json` and active state deleted while `selected-test.json` survives.
- EH21 — `low`: the stale-copy behaviour of `CopySelectedMetadataAsync` is pre-existing.
- EH24 — `low`: it applies only in CI after the first commit has already failed, and the fix adds a parameter.
- EH25 — `false`: the engine's `IsOwnedState` already compares `WorkspaceFor(runId)` exactly, and both forms normalise identically for run IDs.
- EH27 — `false`: engine-initialized selected states always carry attempt, environment, created and context, so result publication is never skipped.
- EH28 — `low`: "incomplete" rather than "pending" after a skipped teardown needs the first commit to fail, and the fix adds a branch.


### Full-story review fixes — resumed build, 2026-10-25

All sixteen accepted patch items P1–P16 are implemented. The owner decisions Q1(a), Q2(a) and Q3(c) are preserved. The broad Module lane passed 668/668 cases. A subsequent isolated `archiveMetadata:false` correction prevents optional archival from using the survivor metadata allowance; all eleven final focused retention cases pass, including a sentinel assertion and the original cause/time/survivor checks. The final full Evidence lane passes 182/182 cases, including both acceptance consumers against removed, unknown, workspace-leftover and unverified observations. Final individual Debug builds have zero warnings/errors. The broad and final-focused revisions are recorded separately; no final-source broad rerun is claimed.

Implementation logs, complete XML, exact commands, superseded observations and the isolated final correction are retained under [the verification archive](evidence/2-5/full-story-patches-20261025/). Parent Platform verification, matrix/source reconciliation and independent workflow review follow.


### Resumed full-story workflow review — 2026-10-25

All three configured review layers completed before triage. Sixteen observations received individual verdicts before grouping. Seven private correction/test groups remain; the frozen intent and owner decisions are unchanged. Two pre-existing issues are recorded separately; CE1 and the earlier occasional-live-coverage rejection are carried without duplicate action.

| Finding | Verdict | Route and verified evidence |
| --- | --- | --- |
| B1 | medium | Patch C1: The Windows branch opens an ordinary FileStream; the public reader has no preceding reparse-point check. A held no-follow handle is required for the promised HXT106 refusal. |
| B2 | high | Carried defer CE1: carried: same Linux inaccessible-environment claim and location as CE1 in the prior final workflow triage. The entire Linux environment-read/exception behavior exists at Builds baseline 3052386abb54ff111f498a8f6424300ea7cdda93. Do not patch or defer it again. |
| B3 | high | Defer D2: The baseline scanner already discovers bare PIDs and later calls GetProcessById/Kill without creation-identity validation. PID reuse can kill a peer; this is a pre-existing deletion identity defect. |
| B4 | high | Defer D3: The baseline CopySelectedMetadataAsync already reads child-writable plan/readiness JSON and uses only ManifestSecretDetector/SafeMetadata. Exact arbitrary token values in innocuous properties can escape this pre-existing filter. |
| B5 | medium | Patch C2: IsOwnedState and enumeration do not reject null ReadinessResults entries. Down passes them into the new TestResultWriter, whose resource.Name projection dereferences null before structured teardown can finish. |
| B6 | medium | Patch C3: ValidResources accepts nonempty DiscoveryFailures with Unverified=false; IsEmpty ignores these categories. Such a durable observation can incorrectly validate complete cleanup. |
| B7 | medium | Patch C4: ValidOutcome checks the presence of TerminalOutcome but not its enum values or terminal status/exit-code consistency. Typed JSON permits undefined numeric enum values, which can reach restored command outcomes. |
| B8 | medium | Patch C5: Directory.Exists also returns false for an existing non-directory state path or inaccessible directory. Enumeration then reports successful absence. A direct attributes/enumeration check can distinguish absence from failure. |
| B9 | low | Rejected: Optional final export warnings remain in command diagnostics and do not fabricate cleanup or invalidate retained result references. Updating the earlier lifecycle snapshot would add export-to-state context/callback plumbing for an occasional redundant diagnostic omission; no everyday loss of the terminal outcome or owned resources was shown. |
| B10 | low | Carried rejected BH11/CB10: carried: the same additional real-service cancellation/startup-timeout scenarios were rejected in the earlier review. Ordinary cases run and pass; fresh live/package qualification remains explicitly unavailable. |
| E1 | high | Carried defer CE1: carried: the reproduced nondumpable Linux child confirms the already logged CE1 boundary; baseline behavior is unchanged. No duplicate deferral is appended. |
| E2 | medium | Patch C1: Same Windows final-component link-following defect as B1; there is no upstream public-reader guard. |
| E3 | high | Defer D2: The new unverified-observation merge prolongs the stale-PID window, but the same bare-PID kill and discovery/deletion race are present at the original baseline. Preserve the independent-peer harm in the shared pre-existing identity-defect deferral. |
| V1 | medium | Patch C6: Pre-verified regression gap: no public descriptor/result test holds an otherwise valid file exclusively. Disabling respectSharing at the reader leaves the inspected tests passing. |
| V2 | medium | Patch C7: Pre-verified regression gap: no descriptor/result test combines unavailable resources with populated empty readiness. Removing the array-kind guard leaves the inspected tests passing. |
| VO1 | medium | Patch C1: Verified Other finding: the Windows FileStream branch follows the final link while the contract promises HXT106. Group with B1/E2 only after this individual verdict. |

- [x] [Review][Patch] C1 — refuse Windows final-component contract/report links using the held file handle; cover descriptor/result symlink refusal.
- [x] [Review][Patch] C2 — reject null durable readiness entries before down/list consumption.
- [x] [Review][Patch] C3 — reject contradictory discovery/completion observations.
- [x] [Review][Patch] C4 — validate committed terminal enum/status/exit-code combinations.
- [x] [Review][Patch] C5 — report an unreadable or non-directory state root as incomplete listing.
- [x] [Review][Patch] C6 — verify exclusive-lock refusal and post-release acceptance at both public readers.
- [x] [Review][Patch] C7 — verify unavailable resources cannot accompany populated empty readiness at either public reader.


### Final resumed workflow result — 2026-10-25

All sixteen original accepted fixes and all seven additional workflow patch groups are implemented. Final parent verification on unchanged source passes **691/691 Module cases, 182/182 Evidence cases and 1/1 Platform cleanup-helper case** (874 total), with no failures, errors, skips or unrun cases. All three individual Debug builds have zero warnings/errors. All seven lifecycle matrix rows, all accepted finding mappings, 59 source fingerprints, schema byte parity and relocated retained-evidence validation pass. Exact commands, full XML, the initial and final source phases, reviewed/final diffs and independent triage are retained in [final verification](evidence/2-5/full-story-patches-20261025/verification.json) and [workflow correction verification](evidence/2-5/full-story-patches-20261025/workflow-review-findings-verification.json).

D2 (process creation identity before PID deletion) and D3 (exact captured-secret filtering for optional metadata archives) are pre-existing and remain deferred. D1, CB3, CE1 and the owner-approved Q3 native-process limitation are carried. Fresh live qualification remains gated by Aspire 13.6.1 versus required 13.6.3; package qualification still requires authoritative published artifacts, and Windows/macOS native runtime qualification is not claimed. The build spec is done and sprint entry is review.

Builds implementation is committed locally as `7116b1672c58fda4f480ed77c9898bfec34622c9`. The Platform completion commit records its gitlink and this tracking/evidence; this workflow does not push.

### Review Findings — delta review (2026-10-25)

This pass reviewed only what changed after the full-story review: Builds `b215213`..`7116b16`, limited to the story's 54 files (22 changed, including the 1-line `0c73550` merge), plus Platform `d681029`..`HEAD` for `docs/contracts/test-environment-v1.md`. That is 24 files and 1,489 diff lines, covering fixes P1–P16 and C1–C7. All four layers completed: Blind Hunter (BH), Edge Case Hunter (EH), Verification Gap (VG) and Acceptance Auditor (AA). Each of the 34 observations got its own verdict before grouping. The result is **0 decision-needed, 9 patch, 1 defer and 21 rejected**. The owner decisions Q1(a), Q2(a) and Q3(c) are unchanged. The Auditor confirmed that every one of P1–P16 and C1–C7 has a matching change.

- [ ] [Review][Patch] W1 — No test accepts a committed `unavailable`/PrerequisiteUnavailable record (medium; VG2) [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/LocalRunCommittedReviewTests.cs:251]. C4's pairing arm at `CompositionRunStateStore.cs:137` can be deleted or mis-coded with every test still passing. If that happens, a missing-prerequisite native run (HXR030, status `unavailable`) fails its second recording, automatic cleanup stays pending, `down --run-id` returns HXR023 permanently and `list` marks the owner unverifiable. Add positive rows for `unavailable`/PrerequisiteUnavailable and `cancelled`/Cancelled. Each should assert `ValidOutcome`, `ReadCommittedOutcomeAsync`, and a successful listing that contains the owner.
- [ ] [Review][Patch] W2 — The owner naming in the listing's per-owner exception path is never tested (medium; VG3) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionRunListing.cs:68]. If the P5 `unverifiableOwners.Add` in that catch is deleted, an owner whose `selected-test.json` or `execution-activation.json` holds non-JSON bytes drops out of `list`, and `list` reports success. Add "unreadable" rows for both files to `UnverifiableObservationNamesOwnerAsync`. Assert TopologyOrLifecycle, the healthy peer, and the owner ID in HXR024.
- [ ] [Review][Patch] W3 — The session-less recording path restores an unvalidated committed outcome (low; VG other) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1273]. When `session` is null, `selected-test.json` is read with `CompositionDocumentStore.TryReadAsync` and copied into the command status and outcome at :1298. That bypasses C4's rule that undefined enums and unsupported status/exit pairs never become restored outcomes. Use the validating `engine.ReadCommittedOutcomeAsync`, which :1288 already uses.
- [ ] [Review][Patch] W4 — An exact `down` that refuses unverifiable metadata omits the run-derived workspace leftover (low; AA3) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1146]. `IncompleteDown()` reports empty lists. The C2/C3/C4 tests assert that the workspace survives, but `down.Remaining` never names `workspace:<runId>`, although `WorkspaceFor(runId)` is known without trusting state. That breaks the matrix rule "report exact known leftovers". Include the workspace when its directory exists, as `:2149` already does.
- [ ] [Review][Patch] W5 — The contract omits the reader rule that unavailable resources require unavailable readiness (low; BH8+AA4) [docs/contracts/test-environment-v1.md:39]. C7 pins HXT104 for `environment.resources: not-yet-populated` combined with populated readiness, even an empty readiness array, and the published schema accepts that shape. Schema-only fixture validators therefore accept documents the Builds reader refuses. State the rule beside the P16 paragraph.
- [ ] [Review][Patch] W6 — The README still says exact cleanup accepts an omitted manifest, with no qualification (low; AA5+BH13) [references/Hexalith.Builds/README.md:459]. Since P6, `down --run-id … --evidence` without `--manifest` is refused with HXC001. Say that `--evidence` requires `--manifest`.
- [ ] [Review][Patch] W7 — The HXE149 message and remediation do not mention the new cleanup requirement (low; BH5) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Evidence/ReadinessEvidenceValidator.cs:340]. A passed readiness row that references a `completed`, exit-0 artifact with leftovers is refused with the message "must reference a successful module-run artifact" and the remediation "Reference a completed run-evidence artifact with a successful outcome" (:377). Operators get no sign that unverified cleanup caused the refusal. Name verified selected cleanup in both texts.
- [ ] [Review][Patch] W8 — The success branch of the P4 evidence-failure merge is untested (low; BH14) [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/LocalRunLifecycleTests.cs:37]. The only `evidenceFails` row also fails cleanup, so `MergeEvidenceFailure` takes its non-success branch. Reverting the success branch at `ModuleCommandExecutionService.cs:1571` to the old construction would drop `testResultPath` and earlier diagnostics after a clean pass whose evidence write fails, and no test would catch it. Add a clean-pass, evidence-failure row that asserts `testResultPath`, the evidence diagnostic's rule ID and the Q1(a) exit code.
- [ ] [Review][Patch] W9 — The fallback reads owner cancellation twice (low; EH1) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1225]. `fallbackTerminalCause` and `result` each read `cancellationToken.IsCancellationRequested`. If the owner cancels between the two reads, a `cancelled`/HXC130 outcome gets the durable cause `execution-timeout`, or HXT007 gets `run-cancelled`. Read the value once.
- [x] [Review][Defer] W10 — The Windows no-follow, shared-read open path never runs during verification [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/SelectedNativeTestReport.cs:203] — deferred (medium; VG1+BH10): every workflow runs on `ubuntu-latest`, the spec excludes CI workflow changes, and Windows native runtime qualification is deferred by owner decision Q3. During that qualification, run the C1/C6 reader theories and runner metadata reads on Windows. Re-check two rejected findings at the same time: BH9/EH7 (non-link reparse tags are refused) and EH6 (the raw `CreateFileW` call has no long-path prefix).

#### Rejected

- AA1 — `low`: P1 prescribed the finite metadata allowance, and a retained failure stops nothing, so the declared stop limit does not apply. HXT008 occurs only when survivor discovery takes longer than 5 s. The committed outcome and retention survive, and `down` rediscovers the resources.
- EH3 — `low`: same path. If discovery plus recording exceeds the shared 5 s, the result is the explicit unverified placeholder plus HXT008, not lost retention. A second budget would add complexity for a slow-scan case.
- AA2 — `false`: no producer commits `withdrawn`. The runner writes only `completed`, `failed`, `unavailable` and `cancelled`, and the v1 writer maps exit Cancelled to `cancelled`.
- BH3/AA6/EH4 — `low`: the rollback's receipt write fails only if the same receipt write has just succeeded under the transaction lock and then a disk-full or permission change intervenes. Preserving the conflict exception would need another guard.
- BH1 — `low`: refusing deletion when ownership metadata cannot be verified is the intended fail-closed, exact-owner rule, and the situation only arises with corrupt or tampered state. A purge or recovery command would be new public surface. The reportable leftover is patch W4.
- BH2 — `false`: every runner producer pairs status and exit code within C4's arms. `NativeTestExecutor.Failed` maps PrerequisiteUnavailable to `unavailable`, `FailAndTearDownAsync` maps Cancelled to `cancelled`, and every `PlatformRefusal` caller that passes Cancelled sets `cancelled`. No writer bug is demonstrated.
- BH4/EH8 — `low`: owner decision Q1(a), implemented as P15, scoped the gate to `selectedTestRun`. Ordinary-run evidence has never carried cleanup observations, and adding them would change the evidence schema.
- BH6 — `false`: the `isEmpty` half cannot be isolated, because the artifact validator already rejects `removed` with a non-empty remainder (`ModuleRunEvidenceArtifactValidator.cs:481-482`). The `removed` success row proves the hash rebinding works, so the HXE205/HXE149 in the failing rows come from cleanup.
- BH7 — `false`: non-regular inputs are still refused. Devices fail the seekable/positive-length check (HXT105), and directories fail on read. The lock check matches the runner's .NET writers, which use flock, and published results are renamed into place atomically.
- BH9/EH7 — `low`: Windows-only, and it errs on the fail-closed side. A correct fix would have to reopen non-link reparse points without `OPEN_REPARSE_POINT` and re-verify their identity. Carried into W10.
- EH6 — `low`: Windows-only, and it needs a path of 260 or more characters on a host without LongPathsEnabled. Carried into W10.
- BH10 (remaining claims) — `false`: Unix open failures are equally generic, Windows `CreateFile` sharing already enforces exclusive writers, `FileMode.Open` equals `OPEN_EXISTING` (3), and the constants are the standard values. The untested part is W10.
- BH11/EH2 — `maybe-false`, would be `low`: the else-branch keeps the pre-existing HXT007 "execution lifetime" label, and P11 aligned the cause with that label. A mislabel would need a non-owner, non-startup OCE after the session exists but before execution timing is set. To settle it, trace which inner bounds can throw there. The reflection-based test is a maintenance concern, as with BH19 earlier.
- BH12/EH5 — `low`: adding a structured owner field would add surface. Non-run-ID file names are deliberately not echoed. The outer catch fires only on root-level failures, where nothing has been verified yet. An unbounded message needs many corrupt owners.
- BH13 (remaining claims) — `low`: HXC001 is the CLI's standard usage refusal. `--profile` and `--filter` were already ignored on exact down before this diff. The test without `compositionOptions` causes no reachable harm while the guard holds.
- BH15 — `low`: test maintenance only. Every failure mode still fails the test, and none produces a false pass.
- EH9 — `low`: the archives are optional, and the v1 result references none of them (`TestResultWriter` has no archive references). Only the retention path that falls back before the native callback lacks the copy. Restoring it would let archival use the survivor allowance again, which the isolated `archiveMetadata:false` correction removed.
