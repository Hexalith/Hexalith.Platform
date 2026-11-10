---
title: '2.11 (part 2) Report lifecycle observations'
type: 'feature'
created: '2026-11-10'
status: 'done'
baseline_commit: 'd7c74e8a5d5627a8773fb36652813fe743de3d6f'
route: 'dispatch'
review_loop_iteration: 0
story_key: '2-11-supervise-attached-runs-and-report-the-missing-lifecycle-pr'
builds_baseline_commit: '52ea174c70f1afe77b52a2ccf06bb53afa5e962d'
builds_implementation_commit: 'f118c2440101fe67be2c7f7a569cbe2257223a16'
context:
  - '{project-root}/docs/contracts/test-environment-v1.md'
  - '{project-root}/references/Hexalith.Builds/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Partial startup results omit actual duration, some failure timings include teardown, and owner results leave tool identity and termination unavailable. These gaps block Story 2.10.

**Approach:** Capture original execution identity and startup timing, persist observed termination, and project them into existing v1 local and CI records.

## Boundaries & Constraints

**Always:**
- Freeze startup elapsed once at readiness, failure, timeout or observed interruption, before diagnostics/teardown. Preserve original request time, deadline and justified override. Reuse the measurement through result refresh and cleanup. Recovery records its observed interruption; it cannot reconstruct an unobserved death instant.
- Attached startup bindings describe the owner's environment startup, using its measurement and original deadline; they never mislabel attachment preparation as environment startup.
- Capture `hexalith-module`, the actual CLI version and a documented SHA256 manifest of executing CLI/Tooling binary names and hashes before allocation. Retain the manifest as classified diagnostics. Hash failure refuses new provisioning. Preserve original identity through later cleaners, reconnects and attached fallback publication; legacy unknown identities remain unavailable with diagnostics. Attached suites bind the live owner's executor.
- Populate termination from observations: distinguish native child work, environment work and access. Unissued items are `not-applicable`; retained services/access are `pending`; verified stopping/deletion is `terminated`; failed or unverifiable stopping is `failed` with leftovers/diagnostics. A passing test alone proves no termination.
- Preserve first outcomes, holds, failure retention, bounded stopping, ownership checks and part 1's attachment/access rules. All emitted results must pass the unchanged reader.

**Never:** Change policy, v1 schemas/readers, qualification bindings or pins; claim package identity from a binary digest; edit the Parties caller; publish or dispatch hosted runs. Part 3 and Story 2.10 remain separate.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Ready | Successful startup | Frozen readiness duration | Exclude execution/cleanup |
| Partial | Timeout, task failure or cancellation, including setup | Duration and original deadline/override in first result | Preserve through deletion |
| Recovery | Interrupted startup, later `down` | Observed interruption duration; original executor | Diagnose unavailable legacy facts |
| Retained | Local test/startup failure | Stopped child distinguished from pending environment/access | Preserve first failure |
| Cleanup | Local/CI stop, partial deletion or discovery failure | Observed termination, explicit failures/leftovers | Never claim complete absence without proof |
| Attached | Execution or fallback publication | Original owner tool/startup; existing attached termination | No expiry extension |

</frozen-after-approval>

## Code Map

Paths below are relative to Builds: B = `src/libraries/Hexalith.Builds.Tooling`, R = `B/Runtime`, W = `B/TestEnvironment`, T = `test/Hexalith.Builds.Module.Tests`.

- `R/CompositionStartupRequest.cs` already preserves request origin/override; `CompositionEngine.cs` computes failure timing too late and `WriteStateAsync` can discard it.
- `W/TestResultWriter.cs`: `PartialBindings` omits known duration; owner termination is unavailable. `TestEnvironmentDescriptorWriter.cs` already projects successful timing.
- `B/Workspace/PlatformIdentityResolver.cs:ToolVersion` obtains actual version. No content-digest producer exists.
- `R/NativeTestExecutionResult.cs` carries launch/survivor/bearer observations; `NativeTestExecutor.cs` has process callbacks. Reuse part 1's recorded attachment termination.
- `Github/run-module-integration/run_module_integration.py` retains receipt-bound results and hash-checked diagnostics; it needs preservation coverage, not a second producer.

## Tasks & Acceptance

**Execution:**
- [x] `R/CompositionEngine.cs`, `CompositionRunState.cs`, `ModuleCommandExecutionService.cs` — freeze/persist startup timing before first publication, handle setup cancellation through the existing timeout issuance pattern, preserve timing on recovery/refresh.
- [x] New `W/ExecutingToolIdentity.cs`; `R/CompositionRunPlan.cs`, `CompositionRunState.cs`, `CompositionRunPlanFactory.cs`, `CompositionEngineOptions.cs` — capture and persist original binary identity at issuance; use actual CLI version logic.
- [x] New `R/CompositionOwnerTermination.cs`; `NativeTestExecutionResult.cs`, `NativeTestExecutor.cs`, `ModuleTestRunRecord.cs`, `CompositionEngine.cs`, `ModuleCommandExecutionService.cs` — persist child/process, access and cleanup observations without changing stop ordering.
- [x] `W/TestEnvironmentDescriptorWriter.cs`, `TestResultWriter.cs` — project original tool/startup and observed owner termination; preserve attached publication and classified diagnostic custody.
- [x] `T/TestResultWriterTests.cs`, `StartupReadinessTests.cs`, `StartupCoordinationTests.cs`, `NativeTestExecutorTests.cs`, `LocalRunLifecycleTests.cs`, `CiRunLifecycleTests.cs`, `AttachedSuiteExecutionTests.cs`; new `T/ExecutingToolIdentityTests.cs` — cover every matrix row, delayed teardown, cleanup under a different executor, changed binary bytes, retained resources and stop/discovery failures.
- [x] `Github/run-module-integration/tests/test_run_module_integration.py` — prove new fields and diagnostic bytes survive hosted-workflow artifact retention/retry.
- [x] `README.md`, Platform `_bmad-output/implementation-artifacts/evidence/2-11/part-2/README.md` — document digest subject/algorithm, observations, revisions and verification limits.

**Acceptance Criteria:**
- Given local or CI runs, when their first and refreshed results are read, then timing, executor and termination agree with recorded observations and validate as v1.
- Given cleanup/reconnect by another process, when it updates results, then original startup/tool/terminal identities remain unchanged while termination advances honestly.

### Review Findings

Code review pass 2 (2026-11-10) of Builds `52ea174c..f118c244` (28 files, +1660/-48). Layers: blind-hunter, edge-case-hunter, verification-gap and acceptance-auditor, with no layer failures. They raised 49 findings: 25 merge into 15 patch entries, 1 is deferred and 23 are rejected, with none needing a decision. Paths are relative to the Builds repository.

- [x] [Review][Patch] Selected-suite prerequisite refusals now create, publish and locally retain an owner run [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:80] (medium; acceptance-auditor+blind-hunter+edge-case-hunter)
  - When no run exists yet, `RefuseUnstartedAsync` now mints a run ID and runs `InitializeStateAsync`. That writes state, `owner.json`, the executing-tool diagnostic and any CI receipt. `RefusePreparedRunAsync` then sends the run to `FailAndTearDownAsync`. This applies to Aspire unavailable, Docker/Dapr missing, HXR014 AppHost not built, and Aspire host paths.
  - Before, these refusals returned `unavailable` with no run. Now they report `failed` with cause `startup-failure`. Locally, `RetainOnFailure` leaves a failure-retained ghost owner that needs an explicit `down`. In CI a receipt is written. If `InitializeStateAsync` throws, the generic catch replaces the original prerequisite diagnostic with HXR020.
  - The spec scoped issuance to setup cancellation and timeout, and the evidence README promises "preserving existing refusal behavior".
  - Fix: restore the pre-change no-run refusal when no run identity exists yet. Keep the `_startupObservations.TryAdd` for runs that already have one.
- [x] [Review][Patch] A pre-launch execution timeout or I/O failure is reclassified HXR030 prerequisite-unavailable [src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:569] (medium; blind-hunter+verification-gap+acceptance-auditor+edge-case-hunter)
  - The expired-before-launch branch (`:569`) and both catches (`:608`, `:613`) now set `CompositionProcessResult.Started` from `launchedProcessId`. Baseline set it to `true`.
  - With `Started` false, the immediate first outcome and `Evaluate` return HXR030 / PrerequisiteUnavailable, with the ".NET SDK" hint. `TerminalCause` stays `execution-timeout`, so a first outcome that can never change carries a contradictory rule and cause. Baseline reported HXT007 / ProductOrTest.
  - `Launched` is already derived from `launchedProcessId`, so the change is not needed.
  - Fix: restore `Started: true` in those three branches.
- [x] [Review][Patch] Engine-level setup cancellation can publish the default startup deadline instead of the declared override [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:453] (medium; acceptance-auditor)
  - `StartAsync` starts from `Resolve(null, ReadinessTimeout)` and resolves the declared deadline only at `:125`.
  - A cancellation in between reaches `ReportSetupCancellationAsync(requestedAt, deadline)`. That covers the `:97` check and `PlatformManifestValidator.Validate`. The engine handles it internally, so the service's correct `startupDeadline` is never used.
  - The result is an immutable first result showing the default deadline as `not-overridden`. That contradicts the Partial matrix row.
  - Fix: report engine-level setup cancellation with the declared deadline the service already resolved, or let it propagate to the service's wrapper while the declared deadline is still unresolved.
- [x] [Review][Patch] Setup-cancellation recording failures can still escape the cancellation path [src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1076] (low; blind-hunter+edge-case-hunter)
  - Pass-1 G10 covered only the service call sites. The engine's `:453` call has no wrapper, so an IOException or a timeout from the 5 s recording window leaves the OCE catch. That reports a lifecycle failure instead of HXC130 Cancelled, or mints a second cancellation run.
  - The wrapper's filter also omits `InvalidDataException` and `JsonException`, which CI receipt validation can throw.
  - Fix: route the engine call through the same containment and widen the filter.
- [x] [Review][Patch] Owner termination `observedAt` is re-stamped on every result refresh [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionOwnerTermination.cs:34] (low; blind-hunter+acceptance-auditor)
  - `Observe` rebuilds the environment and access items, and native items upgraded on cleanup, with `UtcNow` on each `RecordTestOutcomeAsync`. That includes items already `terminated` or `not-applicable`.
  - Refreshes within one run and retried `down`s therefore move the recorded termination time, and each one retains a new content-addressed termination diagnostic.
  - Fix: carry the prior item, including its `ObservedAtUtc`, whenever its status is unchanged.
- [x] [Review][Patch] The pre-launch native ledger read/write sits outside the gate's try/finally [src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:549] (low; blind-hunter+edge-case-hunter)
  - A corrupt ledger (`JsonException`) or a failed write escapes `ExecuteSelectedAsync` after the gate is constructed. The gate is never disposed, and the failure bypasses the existing launch-failure classification.
  - Fix: move both calls inside the existing `try`.
- [x] [Review][Patch] A spurious HXT008 "launch metadata could not be durably retained" persists after the final ledger write succeeds [src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:676] (low; edge-case-hunter)
  - `launchMetadataFailed` is set when the launch-time write is interrupted and is never cleared after the later final write lands.
  - Fix: clear it after a successful final write.
- [x] [Review][Patch] New timing assertions have 50–300 ms margins and will flake under the suite's known cross-session load [test/Hexalith.Builds.Module.Tests/CompositionProcessTests.cs:42] (low; blind-hunter)
  - `ReviewSlowLaunchMetadataDoesNotRenewExecutionAsync` requires a python3 spawn plus a kill in under 400 ms. `StartupObservationExcludesDelayedTeardownAsync` and `ReviewSetupObservationPrecedesDelayedCaptureAsync` compare 0.3 s sleeps against 0.25 s thresholds (`StartupReadinessTests.cs:57,104`).
  - Fix: widen the delay/threshold ratios, for example a 5 s callback delay against a 3 s bound.
  - Applied: the slow-launch test now uses a 5 s callback delay against a 3 s bound. The two startup assertions were left unchanged on re-inspection. Their 0.3 s delays run after the freeze, so load only widens the observed gap; they are lower-bounded, not flaky.
- [x] [Review][Patch] `CompositionRunPlanFactory.CreateInitialState` gains an unused public optional `executingTool` parameter [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionRunPlanFactory.cs:31] (low; blind-hunter)
  - Every caller sets `with { ExecutingTool = ... }` instead. The new parameter changes the method's compiled signature for no use.
  - Fix: remove it.
- [x] [Review][Patch] No engine-level test covers selected-suite prerequisite refusal or owner cancellation before the run exists [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:453] (medium; verification-gap, pre-verified)
  - Deleting the new `RefuseUnstartedAsync` block, or reverting `:453`, fails no test.
  - Fix:
    - Add a declared-suite variant of `MissingPrerequisitesStopBeforeAnyResourceAsync` that asserts the restored refusal (no run, no state).
    - Add an owner-cancelled-during-probe variant that asserts cause `run-cancelled` with the declared deadline/override.
- [x] [Review][Patch] No C# test asserts the owner `termination` diagnostic's `runId`, which the CI helper requires [src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestResultWriter.cs:166] (medium; verification-gap, pre-verified)
  - Renaming or dropping the field passes every C# and Python test. In hosted CI, `retain_artifacts` would then refuse the diagnostic and `finalize` would exit 1.
  - Fix: assert the field in `CiTerminationDiscoveryFailureAndRetryPreserveFirstIdentityAsync`.
- [x] [Review][Patch] `PopulateOriginalObservations` has no tests for its conflicting-tool and frozen-duration refusals [src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestResultWriter.cs:452] (medium; verification-gap, pre-verified)
  - Fix: add `tool` and `actualDurationSeconds` rows to `ReviewConflictingKnownStartupBindingIsRejectedAsync`, with state carrying a captured `ExecutingTool`.
- [x] [Review][Patch] No test covers the new kill-and-rethrow catch in `RunCoreAsync` for a throwing launch callback [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionProcess.cs:319] (low; verification-gap, pre-verified)
  - Fix: add a `CompositionProcessTests` case whose `onStartedAsync` throws `InvalidOperationException`. Assert that the exception propagates and that the child has exited.
- [x] [Review][Patch] No test makes a native ledger write fail and asserts the HXT008 launch-metadata diagnostic [src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:729] (low; verification-gap, pre-verified)
  - Fix: for example, have a child replace `<archive>/native-termination.json` with a directory before it exits.
- [x] [Review][Patch] The Retained matrix row lacks a retained local startup-failure termination assertion [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionOwnerTermination.cs:32] (low; acceptance-auditor)
  - Environment/access `pending` is asserted only for a retained test failure. No test asserts native `not-applicable` with environment `pending` for a `RetainOnFailure` startup failure, although the evidence README claims every row is covered.
- [x] [Review][Defer] A `down` from another terminal during a live owner's startup freezes an "interruption" duration before waiting for the provisioning lock [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1503] — deferred: maybe-false, medium if confirmed
  - The freeze precedes provisioning-lock acquisition (`:1577`), so a live owner still starting gets down's arrival time recorded as its startup end.
  - Settle by checking whether that owner, after readiness or failure, can refresh a result whose readiness-based descriptor snapshot conflicts with the persisted duration. That conflict would trigger `PopulateOriginalObservations` "frozen startup duration" refusals.

**Rejected** (one line each):
- B3 low: the wrapper's fallback drops the run ID only if `FailAndTearDownAsync` itself throws past its internal guards, which is rare. Fixing it adds return surface.
- B5 false: recovery prefers the `selected-test.json` record's `Termination` and recomputes environment items in `Observe`, and the engine's final write lands after the executor's. No reader depends on the overwritten shape.
- B8/E15 false: contract v1 makes diagnostic references opaque ("never fetched by the reader"). The result writer retains its own manifest copy in the diagnostics directory, which survives workspace deletion.
- B9 false: the CI helper is a custody step. It binds the retained bytes to the declared and tool content identity, and the producer canonicalizes and validates the manifest before writing.
- B10 false: `Capture()` reads the CLI assembly version, which is what the spec asks for. A malformed persisted version requires tampered local state, which is not a demonstrated path.
- B11 low: hash failure already refuses before any state, workspace or receipt (the test asserts all three are absent). A dedicated rule ID would add public diagnostic surface.
- B12/E9 low: the unlocked `MarkEnvironmentIssuedAsync` read-modify-write and the owner.json-only observation persistence can lose an update only in narrow owner/cleaner races. A fix needs new lock coordination.
- B14 false: HXT008 already denotes "could not be durably retained/recorded" evidence failures. See the README cancellation/cleanup sections and the existing service and attached-command uses.
- B15 low: the undocumented items are internal files and fields. The one user-visible change (prerequisite refusals creating runs) is reverted by the first patch.
- B17 false/low:
  - Clearing caller-supplied selections has no demonstrated harm, because the platform path re-selects.
  - The method-wide CA2000 suppression names no concrete leak.
  - The early-selection drift is rejected with E6.
- B18 false: `Capture()` emits the canonical manifest by construction for the fixed shipped file names, so validation in `RetainAsync` cannot fail after allocation on a production path.
- B19 false: the spec scopes the manifest to the executing CLI/Tooling binaries, and the README states exactly that coverage.
- A2/A3/E10 low:
  - The `Observe` issuance heuristics lack a new-versus-legacy discriminator.
  - The wrong output appears only for legacy owners recovered across a tool upgrade, or for a cancellation between Ready and child launch.
  - The fix adds a discriminator branch. The test asserting legacy `not-applicable` is noted as residual risk.
- E6 (+B17 early selection) low: an owner cancellation during setup that publishes a cancelled run for an invocation that later refusals would reject needs cancel-plus-invalid-arguments timing. A fix duplicates the refusal guards.
- E7 low: recreating tracking requires a legacy `owner.json` still in Starting with no duration and a retried `down` after complete cleanup. New runs persist the duration at the first freeze or publication.
- E11 low: the crash window between `Process.Start` and the launch-ledger write is milliseconds. Recording prepared access as issued would reintroduce pass-1 B3/G2.
- E14 low: abandoning a cancellation-ignoring launch write that then lands after the final ledger is a theoretical race. A fix requires tracking and awaiting the abandoned task.
- E16 low: installed dotnet-tool binaries are regular files. Following links would loosen a deliberate no-follow open.
- E17 low: `PreserveDiagnosticsAsync` loses `summary.json` only when owned state is already unverifiable, and the caller then records a recording failure.

## Implementation Notes

- Approval: user approved implementation on 2026-11-10; the frozen block is locked.
- Use Builds worktree `/tmp/hexalith-builds-2-11-part-2-20261110`, branch `feat/2-11-lifecycle-reporting`, from `builds_baseline_commit`. Preserve the existing Builds checkout and Platform gitlink. No remote operations.
- The worktree is separate from the umbrella; required AI.Tools guidance remains at the Platform context location. Do not initialize nested submodules.
- Builds analyzers are errors. Pass `-nodeReuse:false`; run xUnit v3 DLLs with one filter kind per invocation, and verify binary timestamps. Preserve CRLF source formatting.

- Implementation audit: all seven tasks completed; all six matrix rows matched to passing XML cases. Verified 26 source hashes, unchanged schemas/reader, 260 xUnit and 70 Python passes. Code and evidence were read independently of the implementation report. The implementation was then committed locally after the required final verification.

- Completion: parent Debug build passed with zero warnings/errors; nine affected xUnit classes passed 290 cases and Python passed 70. All 28 source hashes stayed stable; schemas and reader are unchanged. All 12 correction groups are resolved, including every added regression; nothing was deferred.
- Builds local commit: `f118c2440101fe67be2c7f7a569cbe2257223a16`. The complete implementation patch and initial reviewed snapshot remain in [part 2 evidence](evidence/2-11/part-2/README.md). No push or gitlink change.
- Tracking: this part is done. The established user split leaves Story 2.11 in progress while part 3 remains open; the workflow’s generic whole-story review transition does not apply to this partial-story completion.
- Code review pass 2 (2026-11-10): the user chose to apply every patch.
  - All 15 patch entries are fixed in the worktree on top of `f118c244`. The user approved commit and push: Builds `4ce510b2b263f8e4ea469638a130f08776ba27a3` is on `feat/2-11-lifecycle-reporting` and pushed. Merging it into Builds main and releasing are separate steps.
  - Debug build: zero warnings and errors after one SA1118 test fix.
  - The nine affected classes passed 298/298 and Python passed 70/70. The full assembly passed 3,024/3,029 with 0 failures and 5 pre-existing environment skips.
  - Evidence: [review pass 2](evidence/2-11/part-2/README.md#review-pass-2-corrections-2026-11-10).
  - One maybe-false item is deferred to `deferred-work.md`.

## Spec Change Log

## Review Triage Log

Pass 1 (2026-11-10): independent blind (B), edge (E), verification-gap (V) reviews of the staged implementation; the spec was excluded from the shared review diff and supplied only to the edge layer.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | medium | patch G1 | `RunCoreAsync` awaits launch metadata before arming its timeout; the parent token covers combined execution/stopping and cannot enforce the entry deadline. The isolated reviewer probe reproduced a 100 ms timeout extended by a 300 ms callback. |
| B2 | false | reject | The public selected-owner path subsequently writes observed process/termination facts through `RecordTestOutcomeAsync`; a persistent ledger failure makes that publication fail. A transient prelaunch-ledger warning with successful durable final observations does not invalidate an observed passing test or authorize replacing its immutable outcome. No unrecoverable tracking loss with successful publication was demonstrated. |
| B3 | medium | patch G2 | `Observe` falls back to the prepared ledger expiry/access when `native.Launched` is explicitly false. The prepared-only ledger therefore reports issued access without a credential holder. |
| B4 | medium | patch G3 | Setup reporters initialize/hash/persist before `FailAndTearDownAsync` first freezes timing. That bookkeeping occurs after the interruption was observed; the review probe reproduced its inclusion in startup duration. |
| B5 | medium | patch G4 | The newly inserted selection calls `Validate` before the existing root/recorded-gitlink determination. A cancellation during mapping can consequently publish the stale working-tree selection rather than facts from the authoritative declaration. |
| B6 | medium | patch G5 | `RetainAsync` checks only a self-declared digest. Persisted arbitrary manifest JSON can reach a diagnostic marked redacted; no upstream state validation checks this new field's closed manifest shape or canonical bytes. |
| B7 | medium | patch G6 | `PopulateOriginalObservations` verifies original tool identity but only replaces startup duration. A conflicting, internally valid startup origin/deadline/override remains accepted despite known owner values. |
| B8 | medium | patch G7 | Recovery carries prior native PIDs internally but omits them from leftovers when process discovery is unavailable. The probe preserved PID 12345 while emitting no leftover IDs. |
| B9 | false | reject | The cited branches fail state finalization or result/diagnostic publication after physical cleanup has already been observed. They introduce metadata tracking uncertainty, not new uncertainty about stopped processes or deleted access files. Preserving the independently verified termination while cleanup bookkeeping remains incomplete is intentional. |
| B10 | medium | patch G10 | The new cancellation reporter can throw an I/O/receipt persistence error from inside the cancellation catch; sibling catches cannot handle it. Preserve the cancellation response and diagnose unavailable recording. |
| E1 | medium | patch G1 | Independently reproduced B1's timeout extension with a slow launch callback. |
| E2 | medium | patch G2 | Independently reproduced B3's prepared-access/definitive-nonlaunch contradiction. |
| E3 | medium | patch G2 | A later entry's nonlaunch replaces earlier observed native work with `not-applicable`. Preserve actual launch/access history across batch entries; preparing the next entry must not erase it. |
| E4 | medium | patch G7 | Independently reproduced B8's omission of known unresolved native PIDs during discovery failure. |
| V1 | medium | patch G8 | Pre-verified regression gap: result repair masks a missing tool binding in the descriptor actually received by the child. Assert the captured original descriptor, including owner and attached cases. |
| V2 | medium | patch G8 | Pre-verified regression gap: result repair masks missing attached startup measurements. Assert original owner duration, request and deadline in the child-copied descriptor. |
| V3 | medium | patch G9 | Pre-verified regression gap: no owner result test exercises unobserved native stopping or native survivors; attached survivor tests exercise a different implementation. |
| V-O1 | medium | patch G2 | The verification reviewer independently reproduced prepared access remaining pending after a definitive nonlaunch. |
| P1 | medium | patch G11 | Parent audit of legacy recovery: a previously Ready owner can have no committed outcome, execution timing or new native ledger after its old runner exits mid-test. `DownOwnedCoreAsync` calls `RecordTestOutcomeAsync` without native observations, and the fallback in `Observe` labels that missing history `not-applicable` with "No native child was issued." Missing legacy proof must stay unknown until observed cleanup, rather than claim definite nonissuance. |
| P2 | low | patch G12 | The batch-history correction added a separate bounded ledger write before the already-causal cancellation/timeout/nonzero outcome callback. Reorder that write after the immediate callback, preserving the original first-outcome commit precedence while still persisting every nonfinal entry. |

Surviving entries group into G1 (execution timeout), G2 (prepared versus issued native history), G3 (pre-issuance observation), G4 (authoritative selection), G5 (manifest validation), G6 (startup binding consistency), G7 (known process leftovers), G8 (original descriptor assertions), G9 (owner stop-failure assertions), G10 (recording-failure cancellation). These are direct corrections using demonstrated states and existing internal surfaces; no intent or public API change is needed.

G11 preserves unknown legacy native/access history during recovery; explicit new-run nonlaunch and prelaunch setup cancellation remain definite unissued observations.

G12 restores first-outcome publication before the additional per-entry ledger persistence; this is a direct block reorder with unchanged data and budgets.

Resolution: G1–G12 are fixed and parent-verified. The final nine-class run passed all 290 cases, including child-descriptor, timeout, failed-launch/batch history, legacy recovery, malformed-manifest, conflicting-startup and cancellation-recording regressions. B2 and B9 remain rejected on the documented refutations. No deferred entries resulted from review.

## Verification

- Implement in an isolated Builds worktree from the recorded baseline; preserve the Platform gitlink and checked-out Builds files. No push/publication.
- Run `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -nodeReuse:false`; execute the built xUnit v3 assembly with `-class` for each affected test class listed above.
- Run `python3 -m unittest discover -s Github/run-module-integration/tests`; validate emitted documents with `TestEnvironmentContractReader` and confirm schema bytes unchanged.
