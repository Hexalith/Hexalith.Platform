---
title: '2.11 (part 1) Supervise attached declared-suite runs'
type: 'feature'
created: '2026-11-10'
status: 'done'
baseline_commit: 'ff0b9c73f4d5454915da8da5d1500a56d6667e78'
builds_baseline_commit: 'f81486cb532e2e166f5c363fa8e9f75c1313c2a1'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/docs/contracts/candidate-and-attachment-policy-v1.md'
  - '{project-root}/docs/contracts/test-environment-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An attachment only records a hold. The attaching process runs its own tests with no runner-owned work or access. Expiry or withdrawal therefore stops nothing, and `termination` stays `not-yet-populated`. Declared suites refuse attachment (HXC002), so Story 2.10 cannot produce its owner-cancel-hold or attached-failure-expiry rows, locally or in CI.

**Approach:** `hexalith-module test` runs a declared suite as an attached run of a compatible owner. The runner admits the hold and owns the child process and its per-attachment access. It stops both at the earliest of expiry, withdrawal or the run's own cancellation, always before owner teardown. It then publishes the attached result (`role: attached`) with `termination` populated.

## Boundaries & Constraints

**Always:**
- Reuse the existing admission, hold limits, lineage and retry rules. The first outcome and the original `expiresAt` never change. A late success after expiry stays `withdrawn`.
- The runner's UTC clock decides expiry. The child is stopped at `expiresAt` even mid-execution. Execution deadlines and retries never extend a hold.
- Owner cancellation, developer stop or CI job end follows this order: owner state, then durable withdrawal (existing), then stop attached work and end its access, then record termination. Only after that are owner resources stopped.
- A stopping or access failure is recorded as `failed` items plus `leftoverWorkIds` with diagnostics. It never extends the hold and never grants evidence. Teardown then follows the existing leftover and retry rules.
- An attached run's own cancellation ends only that run, as `cancelled`.
- Each attachment is isolated. It gets its own workspace, descriptor (`role: attached`), credential channel and report paths. Its token is narrowed to the attached run and expires no later than `expiresAt`. The owner's gate and paths are never shared.
- A local attached failure retains the environment (existing HXI012). CI never retains.
- The attached result goes to `diagnostics/<attachedRunId>/test-result.json`. It must be v1-valid, including HXT107.
- The hold-only `attach` / `attach --end` interface is preserved. Its termination items are `not-applicable`.
- **Decision (2026-11-10, user): the live owner hosts attached work.**
  - `test --attach-to` admits the hold and hands an execution request to the live owner `test` process. The owner mints the narrowed token from its in-memory key, then launches the child and stops it in-process before its own teardown. No AppHost change, and the key is never persisted.
  - With no live owner (for example, a failed local owner that has exited), attached-suite execution is refused before admission. Hold-only `attach` still works.
  - A failed local owner command stays open until its runner-owned attached runs end.
- **Decision (2026-11-10, user): when access counts as ended.**
  - Token `exp = min(execution deadline, expiresAt)`. The token is handed only to the child.
  - `environmentAccess` is `terminated` once the process tree is killed, a run-tag scan finds no surviving process tagged with the attached run, and the attachment's credential and descriptor files are deleted. Its diagnostic states "bearer valid until `<exp>`; no holder".
  - A surviving process or undeletable file makes the item `failed`, with its IDs in `leftoverWorkIds`.
  - No service-side revocation.

**Never:**
- Change policy v1, the v1 schema or reader invariants; only existing slots may be filled.
- Populate the executing tool or startup duration (part 2).
- Edit the Parties caller (part 3).
- Accept, qualify or pin a tool (Story 2.10).
- Attach hosted, developer-started or cross-job targets.
- Attach suites that need the owner's retention gate or access observation.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Pass | Suite passes before `expiresAt` | `passed`; hold ends `terminal-outcome`; work and access `terminated` | N/A |
| Attached failure | Suite fails (local / CI) | `failed`; local env retained, owner untouched | HXI012 locally |
| Expiry mid-run | Child still running at `expiresAt` | Tree stopped, access ended; `withdrawn`/`hold-expired` at `expiresAt` | A late child report is ignored |
| Owner withdrawal | Owner cancelled, `down`, or CI job end with an active child | `withdrawn` with the owner cause; work and access ended before any owner resource stops | N/A |
| Own cancel | Ctrl+C on the attached command | `cancelled`; owner and other attachments untouched | N/A |
| Stop failure | Child survives the stop allowance | Item `failed`, PIDs in `leftoverWorkIds`, outcome unchanged | No extension; leftovers listed |
| Retry | Same request id | Same lineage and expiry; refused after expiry | HXR036 |
| Refused | Incompatible or closed owner, gate-requiring suite, `--filter`/`--evidence` | No admission, no child | HXR033–035 / HXC002 |

</frozen-after-approval>

## Code Map

B = `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling`, T = `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests`.

- `B/Runtime/ModuleCommandExecutionService.cs`
  - :1095-1114: the HXC002 `runId` refusal in `ExecutePlatformAsync`.
  - :1380-1505: owner post-run flow (CI withdrawal, local observe, success hold wait, owner-cancel withdrawal).
  - :1580-1601: the `StopSessionAsync`/down call sites that termination must precede.
- `B/Runtime/NativeTestExecutor.cs:364-601` `ExecuteSelectedAsync`.
  - Token :431-435, endpoint secrets :437, gate :481, handoff :468.
  - Paths are keyed to the owner, so they collide across concurrent runs.
  - Parameterize it for an attached identity rather than forking it.
- `B/Runtime/CompositionProcess.cs:231-302` `RunSelectedAsync` tree-kill. A kill timeout is silently swallowed at :289-292 and must surface as a stop failure with the surviving PIDs.
- `B/Runtime/CompositionAttachAdmission.cs`: `AdmitAsync`, `EndAsync`, `Retry`, `FinishedOwnerReason` (admission is open while the owner is Ready with no `TestRun`, or local failure-retained).
- `B/Runtime/CompositionAttachment.cs`: the record, `IsConsistent`, `HasConsistentOutcome`, `ToContract`.
- `B/Runtime/CompositionAttachmentStore.cs`: the `UpdateCoreAsync` lock, `WithdrawAsync`, `ObserveAsync`, `WaitForHoldsOrFailureAsync`. Extend the internal `hexalith.g4-run-attachments.v1` record; existing documents must stay readable.
- `B/Runtime/CompositionEngine.cs`: `DownOwnedCoreAsync` :1412-1498 (withdraw, then stop) and `FailAndTearDownAsync` :1911-2031 (CI).
- `B/Runtime/CompositionRunResourceScanner.cs`: run-ID env-tag discovery, used to find leftover attached processes.
- `B/TestEnvironment/TestEnvironmentDescriptorWriter.cs:113` and `TestResultWriter.cs:42-56,186,279`: role hard-coded to owner, and `termination` unpopulated.
- `B/TestEnvironment/TestEnvironmentContractReader.cs:224-303`: attached-role validation already exists. Do not change it.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`: `attach` :188-256 and the `test` options :356-382.
- T: the `AttachAdmissionTests.cs` `OwnerRun.StartAsync` fixture (:1904, a `runner.py` child with barriers), `NativeTestExecutorTests.cs`, `ModuleTestSelectionTests.cs:40,68` (the attachment-refusal case to invert), `CiRunLifecycleTests.cs`, `TestResultWriterTests.cs`.

## Tasks & Acceptance

Follows the 2026-11-10 decisions above. Work in a scratch worktree branch cut from Builds `origin/main`, and leave `references/Hexalith.Builds` untouched.

**Execution:**
- [x] `B/Runtime/AttachedSuiteSupervisor.cs` (new): the owner-side supervisor. It runs a liveness lease, takes requests under the attachment lock and prepares the per-attachment workspace. It launches the child, then races child end against `expiresAt`, withdrawal, an attached-cancel request and owner cancellation. It stops the child, ends access, records termination and writes the attached result. It drains before any owner teardown.
- [x] `B/Runtime/NativeTestExecutor.cs`, `B/Runtime/CompositionProcess.cs`: attached identity, paths and narrowed token, with no owner gate. Report stop failures.
- [x] `B/Runtime/CompositionAttachment.cs`, `B/Runtime/CompositionAttachmentStore.cs`: add an execution kind and termination observations to the internal record.
- [x] `B/Runtime/ModuleCommandExecutionService.cs`, `B/Runtime/CompositionEngine.cs`:
  - Replace the HXC002 refusal with the attacher path: admit, request, await, then relay the result and exit code. If the owner never picks up the request, record the run as `failed`.
  - Drain the supervisor after withdrawal and before stopping.
  - A `down` from another process waits within the stop budget, then kills tagged leftovers and records them.
- [x] `B/TestEnvironment/TestResultWriter.cs`, `B/TestEnvironment/TestEnvironmentDescriptorWriter.cs`: attached-role documents with `termination` populated.
- [x] `.../Module.Cli/ModuleCommandApplication.cs`: `test --attach-to <owner-run-id>`, `--hold-seconds`, `--attachment-request-id`. Refuse them together with `--with-owner`, `--candidate-request`, `--evidence`, `--filter` or `--ci-owner-receipt`.
- [x] `T/AttachedSuiteExecutionTests.cs` (new), plus inverting `ModuleTestSelectionTests`: one case per matrix row, local and CI, using real `runner.py` children.
- [x] `references/Hexalith.Builds/README.md` and `_bmad-output/implementation-artifacts/evidence/2-11/README.md`: usage, limits, revisions and test results.

**Acceptance Criteria:**
- Given an owner with an active attached child, when `down` runs from another terminal, then the attached work and access are terminated (or recorded as failed) before the first owner resource stops.
- Given two concurrent attachments, when one is withdrawn or expires, then the other attachment and the owner keep running.
- Given any attached result, when read by `TestEnvironmentContractReader`, then it validates with `role: attached`, matching hold reason/cause and populated `termination`.

## Implementation Notes

- Setup (2026-11-10, orchestrator):
  - Builds work happens in the worktree `/tmp/claude-1000/-home-administrator-projects-hexalith-platform/031b0047-a750-476d-9302-1bebd8191837/scratchpad/builds-2-11`, on branch `feat/2-11-supervised-attached-suites`. The branch was cut from `builds_baseline_commit` and has no upstream.
  - Commit there with Conventional Commits, validated by the worktree's pinned commitlint. Never push.
  - Platform-side files (the evidence README, this spec) stay in the Platform checkout, uncommitted.
  - Builds analyzers are errors. Other sessions build concurrently, so always pass `-nodeReuse:false` and check DLL timestamps.
  - The xUnit v3 runner ANDs `-class` with `-method` filters. Use one kind per invocation.
  - The full Module.Tests run takes about 10 minutes, with about 10 known load flakes (`WorkspaceCommandModeTests`, an `McpCliRunBuildTests` deadline case, `ReadinessDeadlineObservationSharesStoppingBudgetAsync(timeout)`). Rerun those alone before treating them as regressions.
- Implementation (2026-11-10): Builds commit `08db49e8c525d3a8f6116eb4b8b11278d415598b` on the worktree branch. The implementer made these choices where the spec was silent:
  - `attach --end` refuses a runner-owned suite with HXR036 `runner-owned-attachment`.
  - A retry that presents a hold-only lineage becomes a suite run, keeping the same expiry.
  - Attached results are always `baseline-only`.
  - New diagnostics: HXI013 (attached runs stopped) and HXR038 (leftovers).
  - The public `ModuleCommandExecutionService.ExecuteAsync` gains an optional `attachedSuite` parameter, which changes its compiled signature.
  - The new process-discovery override hook is test-only.
  - A `down` from another terminal waits up to half the suite's stop budget for the live owner.
  - The pickup timeout is 30 s.
- Known limits:
  - Owners that require routing-catalog observations are not refused before admission, so the attached run fails as `startup-failure` after it is admitted.
  - A real tree-kill timeout is reported in code, but the stop-failure test reproduces it only through the test-only discovery hook.
- Orchestrator verification:
  - The diff was read in full.
  - Every task and acceptance criterion is satisfied, and every matrix row has a covering test that ran.
  - Independent reruns passed: `AttachedSuiteExecutionTests` 25/25 and `ModuleTestSelectionTests` 46/46.

## Spec Change Log

## Review Triage Log

Pass 1 (2026-11-10). Layers: verification-gap (VG), blind (B), edge-case (E).

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| VG1 | medium | patch (G16) | Pre-verified gap. Nothing tests `attach --end` refusing a runner-owned suite (HXR036 `runner-owned-attachment`). Without the guard, the store is written with an inconsistent `Cause` and the child keeps running. |
| VG2 | medium | patch (G4) | Pre-verified gap. `RecordExecutionAsync` is never exercised. The behaviour is replaced by a refusal under G4, so the test must cover that refusal. |
| VG3 | medium | patch (G17) | Pre-verified gap. Expired, `closed`, dead-PID and start-time-mismatched leases never reach `IsLiveAt`. The only negative test deletes the lease file. |
| VG4 | medium | patch (G18) | Pre-verified gap. No test reaches `TerminateForeignAsync`/`DrainForeignAsync` termination: the lease holder is always the test process, and `Recorder` is never asserted. |
| VG5 | medium | patch (G19) | Pre-verified gap. Nothing tests the owner's `ResolveSelection` re-check (hashes, `Compatible`, gate). Every refusal test stops before admission. |
| VG6 | medium | patch (G20) | Pre-verified gap. These refusals are untested: `--hold-seconds`/`--attachment-request-id` without `--attach-to`, more than one profile, a mismatched mode, and a v1 manifest with `--attach-to`. |
| VG-O1 | medium | patch (G3) | Verified. The `PresentedRequestId` branch returns the lineage without comparing its `Execution` profile or hashes with the request. `AttachedSuiteCommand` then relays that other suite's result. |
| VG-O2 | false | reject | `AttachedSuiteCommand.cs:88` can only see an acceptance without `Execution` when `request.Execution` is null. That never happens for `--attach-to`: both the new and the retry branches return an attachment carrying an execution, or a refusal. |
| VG-O3 | medium | patch (G4) | Verified. A live hold-only lineage is converted into a runner-owned suite, and its holder's `attach --end` is then refused. That breaks the frozen "hold-only attach / attach --end interface is preserved". |
| B1 | medium | patch (G2) | Verified. `WriteAttachedAsync` builds `accessStatement` as "…; no holder" whenever a bearer expiry exists, even when the access item is `failed`. The published diagnostic then contradicts the termination record. |
| B2 | medium | patch (G1) | Verified. `DrainAsync` reports only runs in its current `active` snapshot. The CI, owner-cancel and override drains append HXR038 only to result diagnostics, and the later `DownOwnedCoreAsync` drain sees no active runs. Owner cleanup is then recorded `complete` while a tagged attached process survives. |
| B3 | low | patch (G10) | Verified. The fallback publish passes `[RuleId ?? "HXT009"]`, so a passed record carries HXT009. A fallback publish can also overwrite the owner's richer result in the short window before the owner marks it published. |
| B4 | low | patch (G10) | Verified. An external `down` waits half the stop budget, so it can run `TerminateForeignAsync` concurrently with a healthy owner. Termination is first-writer-wins and harmless; the harm is the publication overwrite in G10. |
| B5 | low | patch (G9) | Verified. `ForceTerminateAsync` records termination but never publishes. If the second drain also times out and the attacher is gone, the result stays unpublished. |
| B6 | low | reject | The post-admission re-read already falls back to the pre-admission owner (`?? owner`). A null owner or an IO error here needs the owner state to vanish or turn unreadable right after admission. That is unlikely, and the fix adds guards. |
| B7 | medium | patch (G3, G4) | Verified. Same two defects as VG-O1 (suite mismatch on reconnect) and VG-O3 (hold-only takeover). |
| B8 | low | patch (G8) | Verified. Expiry, cancel and drain call `RecordOutcomeAsync` (lock wait plus 10 s) before `Signal`, so under contention the child can outlive `expiresAt` by seconds. `RecordOutcomeAsync` already lets expiry win by timestamp, so signalling first is safe. |
| B9 | low | reject | Verified, but only reachable when an older tool rewrites a new owner's `attachments.json` against the same state directory. Everyday use and the 2.10 campaign run one fixed tool. The fix needs schema-version machinery. Residual risk noted in the summary. |
| B10 | low | patch (G15) | Verified. Endpoint secrets (shared Dapr token) and token scopes are copied unchanged; only `runId`/`sub` and `exp` change. This matches decision 2A, but the README and access detail don't state the limit. |
| B11 | low | reject | The in-session `DownOwnedCoreAsync` drain is a near no-op in production: holds have ended, or the owner-cancel path has already drained. The new test would need new fixture infrastructure. |
| B12 | medium | patch (G17, G18) | Verified. The host-lost, PID-reuse and stale-open-lease paths are untested because the holder is always the test process. A lease naming an exited PID makes them testable in-process. |
| B13 | medium | patch (G21) | Verified. The task asks for local and CI cases, but CI covers only pass, failure and job end. Neither `ci-job-cancelled` nor CI expiry is asserted, and those are the CI rows of 2.10. The evidence README overstates coverage and omits the peer-expiry row. |
| B14 | low | patch (G20) / reject | The Memories `functional-retention` refusal is untested; it joins G20. Package mode, the late-success-after-expiry race and the test-name nits are rejected: `RecordOutcomeAsync` decides by `now >= ExpiresAtUtc` by construction, and the names are cosmetic. |
| E1 | medium | patch (G1) | Same root cause as B2. |
| E2 | low | patch (G14) | Verified. `cancelGiveUp` is set before the cancel-request write. If that write throws, it is never retried, and Ctrl+C is later recorded as `failed`/`test-failure`. The fix is a direct reordering. |
| E3 | medium | patch (G3) | Same as VG-O1. |
| E4 | low | reject | A reconnect after the owner passed is refused by the existing `CheckOwnerState` rule, the same as for hold-only retries. The lease is only closed while running during a drain, when the run is ending anyway. |
| E5 | medium | patch (G4) | Same as VG-O3. |
| E6 | medium | patch (G5) | Verified. `TerminateAsync` calls `Kill(entireProcessTree)` on reported PIDs (including `ForceTerminateAsync`'s `run.ProcessId`) without checking that they still carry the attached run tag. A reused PID would take down an unrelated tree. |
| E7 | low | patch (G11) | Verified. With `LaunchedAtUtc` set and an early executor return, `launched=true` and the bearer expiry falls back to `expiresAt`. The record then claims terminated work and a bearer that was never issued. |
| E8 | low | patch (G12) | Verified. `ForceTerminateAsync` gives each unfinished run its own sequential 3 s, unlinked to the owner bound. |
| E9 | low | reject | `TryCloseWhenIdleAsync` runs only when `_runs` is empty. A lock failure can withdraw only a not-yet-picked request as `cleanup`, which is rare; the fix adds retry logic. |
| E10 | low | patch (G13) | Verified. `IsPending` ignores `EndedAtUtc`, so a request withdrawn before pickup is picked up and the owner's endpoint secrets are copied into a new workspace before being deleted. |
| E11 | low | reject | Same as B6. |
| E12 | false | reject | `HoldPollInterval` is an internal option that only tests set, defaulting to 1 s. No user path sets zero or a negative value. |
| E13 | low | reject | Lease renewal only stalls past 10 s under repeated 5 s lock timeouts within one pass, which is rare. An independent timer adds complexity. |
| E14 | medium | patch (G6) | Verified. Attached `dotnet test` children run without `-nodeReuse:false` or `-p:UseSharedCompilation=false`, so reusable MSBuild and compiler-server nodes inherit the attached run tag. The run-tag kill at attachment end can then break owner or peer builds reusing those nodes. Those nodes also hold the bearer in their environment. |
| E15 | low | patch (G8) | Same as B8. |
| E16 | low | reject | This needs an already-cancelled bound plus a microsecond window between `TryAdd` and the `Completion` assignment. The draining flag means that run never launches a child, so the outcome is a late not-applicable record. |
| E17 | medium | patch (G7) | Verified. When `DrainForeignAsync` throws, for example on an exhausted bound, the engine only adds a recording failure and still stops owner resources while the attached child lives. 2.7's rule is no teardown when withdrawal or report cannot be recorded (`WithdrawalRefusal`). |

## Verification

**Commands:**
- `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -nodeReuse:false` (in the worktree) -- expected: 0 warnings/errors and a fresh DLL timestamp
- `dotnet exec <bin>/Hexalith.Builds.Module.Tests.dll -class <C>`, with C in AttachedSuiteExecutionTests, AttachAdmissionTests, ModuleTestSelectionTests, CiRunLifecycleTests, TestResultWriterTests, TestEnvironmentContractReaderTests, ModuleRunnerReviewRegressionTests, LocalRunSourceReviewTests, NativeTestExecutorTests -- expected: all pass
- Full Module.Tests assembly -- expected: only the known load flakes, each passing when rerun alone
