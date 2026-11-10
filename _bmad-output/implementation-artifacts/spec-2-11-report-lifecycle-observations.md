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

## Implementation Notes

- Approval: user approved implementation on 2026-11-10; the frozen block is locked.
- Use Builds worktree `/tmp/hexalith-builds-2-11-part-2-20261110`, branch `feat/2-11-lifecycle-reporting`, from `builds_baseline_commit`. Preserve the existing Builds checkout and Platform gitlink. No remote operations.
- The worktree is separate from the umbrella; required AI.Tools guidance remains at the Platform context location. Do not initialize nested submodules.
- Builds analyzers are errors. Pass `-nodeReuse:false`; run xUnit v3 DLLs with one filter kind per invocation, and verify binary timestamps. Preserve CRLF source formatting.

- Implementation audit: all seven tasks completed; all six matrix rows matched to passing XML cases. Verified 26 source hashes, unchanged schemas/reader, 260 xUnit and 70 Python passes. Code and evidence were read independently of the implementation report. The implementation was then committed locally after the required final verification.

- Completion: parent Debug build passed with zero warnings/errors; nine affected xUnit classes passed 290 cases and Python passed 70. All 28 source hashes stayed stable; schemas and reader are unchanged. All 12 correction groups are resolved, including every added regression; nothing was deferred.
- Builds local commit: `f118c2440101fe67be2c7f7a569cbe2257223a16`. The complete implementation patch and initial reviewed snapshot remain in [part 2 evidence](evidence/2-11/part-2/README.md). No push or gitlink change.
- Tracking: this part is done. The established user split leaves Story 2.11 in progress while part 3 remains open; the workflow’s generic whole-story review transition does not apply to this partial-story completion.

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
