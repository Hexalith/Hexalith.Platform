---
title: '2.7 Withdraw attached runs and keep failure retention'
type: 'feature'
created: '2026-10-30'
status: 'done'
baseline_commit: 'f60f2c7596b1fa3010c91d1d546c71600da24715'
builds_baseline_commit: '2980efdf51f305dc5c39bb854261c252d13465f5'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/docs/contracts/candidate-and-attachment-policy-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An attached run ends only through its own `attach --end`, and that end records no outcome. Expiry is only projected when holds are read; nothing is written. Owner cancellation and `down` tear the environment down without touching attachments. An attached run's failure never retains the environment.

**Approach:** Record each attached run's first terminal outcome (`passed|failed|cancelled|withdrawn`) on its attachment. Before any owner cleanup, withdraw active attachments under `attachments.lock` and report them. Treat a recorded local attached failure as failure retention, which only an explicit `down` releases.

## Boundaries & Constraints

**Always:**
- An attached run's first outcome is immutable. `attach --end` requires `--outcome passed|failed|cancelled`. At or after expiry it records `withdrawn` with reason `expired` at `ExpiresAtUtc`, whatever outcome was reported. Ending an already-ended attachment returns its first record.
- Withdrawal happens on expiry, on owner cancellation (`owner-cancelled`) and on `down` (`developer-stop`). Outcome `withdrawn` ⇔ `EndReason` ≠ `terminal-outcome`. A withdrawn run is never a pass, a failure or evidence. The expiry never moves; the actual end time is recorded.
- Owner cancellation (including during the success hold wait) and `down` withdraw every active attachment and report them before teardown starts. The report is `HXI011` with the attached run ids and the reason. Holds that have expired but were never written are recorded as `expired` in the same pass.
- Withdrawal is written under `attachments.lock`, after the owner's cancelled/`in-progress` state is recorded. A racing admission is therefore either withdrawn or refused.
- If withdrawal cannot be durably recorded, cleanup does not start. `down` refuses with `HXR037` (retryable), and owner cleanup stays pending with `HXT008`.
- A local attached `failed` outcome establishes failure retention. Owner success skips cleanup: the owner stops waiting as soon as such a failure is recorded, and records disposition `retained`. A later owner cancellation keeps retention. The environment stays attachable while ready. Only `down` releases it.
- In listings, `failed-runs` includes failed attached runs, and each attachment shows its outcome. Unended expired holds show as `withdrawn` without any write.
- Legacy 2.6 records that ended with `terminal-outcome` and no outcome read as outcome `unknown` and are still valid.
- Keep the 2.5/2.6 rules: owner first outcomes, `outcome.lock`, exact-owner bounded retryable cleanup, CI attach refusal (`HXR035`) and listing honesty.
- Decision (2026-10-30): an owner Ctrl+C during the success hold wait is owner cancellation. Active holds are withdrawn (`owner-cancelled`) and cleanup then runs within the stop budget. This replaces 2.6's pending-cleanup `HXT008` path for that case.

**Never:**
- Edit policy doc v1, the policy id, the meaning of the v1 schemas or the reader invariants.
- Terminate attached-run processes. Decision (2026-10-30): record only. The runner owns no attached work. Teardown on cancellation or `down` ends access. Stopping attached work at expiry is deferred to 2.9 through a deferred-work entry.
- Do CI withdrawal or job-bound cleanup (2.8), fixture migration or attached-run result documents (2.9), candidate acceptance, or pin/publication changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Attached end | `--end --outcome failed` before expiry | Outcome `failed`, reason `terminal-outcome` | Missing or other `--outcome`: `HXR032` |
| Expiry | Hold passes expiry | Listing shows `withdrawn` at once. The next owner, `down` or `--end` pass records it | A late `--outcome passed` stays `withdrawn` |
| Owner cancelled | Live holds | Withdrawn `owner-cancelled`, `HXI011`, then teardown | Unrecordable: no teardown, `HXT008` pending |
| `down` | Live holds, any owner state | Withdrawn `developer-stop`, `HXI011`, then teardown | Unrecordable: `HXR037`, nothing torn down |
| Attached fails, owner passes | Failure recorded during or before the hold wait | Owner exits without cleanup. Environment `failure-retained`, listed with the failed run, attachable | N/A |
| Retained, then cancelled | Attached failure, then owner cancellation | Other active holds withdrawn; resources and retention kept | Only `down` releases |

</frozen-after-approval>

## Code Map

All paths are under `references/Hexalith.Builds` (B), with R = `B/src/libraries/Hexalith.Builds.Tooling/Runtime`. Implement on Builds `main` in the checkout, as the user chose. The Platform side is this spec, evidence notes and the gitlink.

- `R/CompositionAttachment.cs` -- add `Outcome` (init) and extend `IsConsistent`: null while active; `terminal-outcome` ⇒ passed/failed/cancelled or legacy null; other reasons ⇒ `withdrawn`. `ToContract` stays as is: the end reason already encodes withdrawal and the v1 schema has no outcome field.
- `R/CompositionAttachmentStore.cs` -- add a `WithdrawAsync(directory, owner, reason, now)` that ends every unended hold under the lock (`expired` at expiry, otherwise the given reason) and returns the withdrawn records. Replace `WaitForNoLiveHoldsAsync` with a wait that also returns when a `failed` attachment is recorded, and records expiries as it observes them.
- `R/CompositionAttachAdmission.cs:145` `EndAsync` -- take the outcome and apply the expiry rule. In `FinishedOwnerReason` (:284), also treat an attached `failed` as failure-retained when the owner is not cleaning or cleaned.
- `R/ModuleCommandExecutionService.cs:1213–1450` -- on owner cancellation (`catch` :1213) and on cancelled hold waits (:1355), withdraw then clean up. Make `retentionEstablished` (:1334) include an attached failure, and in that case re-record disposition `retained` as at :1320–1332. Report `HXI011`.
- `R/CompositionEngine.cs:1311` `DownOwnedCoreAsync` -- withdraw with `developer-stop` after the `in-progress` record (:1343) and before the provisioning lock and teardown. On failure, return `HXR037` without tearing down.
- `R/CompositionRunListing.cs:81–101,124` -- add failed attached runs to `FailureRunIds`; `Retention` also derives from attached failures; project the outcome passively.
- `B/src/libraries/Hexalith.Builds.Tooling/Diagnostics/ToolDiagnosticFormatter.cs:88–120` -- add `outcome=` to attachment lines.
- `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs:149–180` -- add `--outcome`, required with `--end` and refused without it.
- Docs: `B/README.md:511–533`, the 2.6 deferred-work entries (down while live, attach race) are closed by this story.
- Tests in `B/test/Hexalith.Builds.Module.Tests/`: extend `AttachAdmissionTests.cs` (reuse `OwnerRun`, `SeedHoldAsync`, `Finished`, `ReadyOwnerAsync`) and `CompositionRunListingTests.cs`. Rewrite `CancelledWaitLeavesCleanupDeferredAndResultIntactAsync` (`AttachAdmissionTests.cs:367`) to expect withdraw-then-cleanup. There is no fake clock, so use short real holds or an explicit `now`.

## Tasks & Acceptance

**Execution:**
- [x] `R/CompositionAttachment.cs`, `R/CompositionAttachmentStore.cs` -- outcome model, withdraw, failure-aware wait -- AC1, AC3
- [x] `R/CompositionAttachAdmission.cs`, `Module.Cli/ModuleCommandApplication.cs`, `ToolDiagnosticFormatter.cs` -- `--outcome`, expiry rule, retained admission -- AC1, AC4
- [x] `R/ModuleCommandExecutionService.cs`, `R/CompositionEngine.cs` -- withdraw before cleanup, attached-failure retention, `HXI011`/`HXR037` -- AC2–AC4
- [x] `R/CompositionRunListing.cs` -- failed attached runs, retention, passive withdrawn -- AC3
- [x] Tests for every matrix row; README; `evidence/2-7/` notes; Platform gitlink bump; deferred-work entry for stopping attached work at expiry (2.9)

**Acceptance Criteria:**
- Given an attached run that reaches its hold limit, when the limit expires, then it is recorded as `withdrawn`: neither a pass nor a failure.
- Given owner cancellation or `down` with active attached runs, then they are recorded and reported as `withdrawn` before any cleanup starts.
- Given a local attached run that fails while the owner passes, when the owner finishes, then the environment is retained and listed with the failed run.
- Given an environment retained after a failure, when a later cancellation happens, then retention remains until `down`.

## Implementation Notes

Builds HEAD is `0b17f8022cdc23b8b242b000f5b2aa15580184ac`; implementation was recorded externally as `a13d7972395f989955cf00f1047f1ba50b1a6b73`, followed by local verification, cleanup-admission correction, and independent-review fixes. The Platform gitlink targets the final Builds HEAD. No remote operations were performed by this workflow.

The attachment model, CLI, listing, owner cancellation and every session/exact-down entry point implement the frozen matrix. The v1 contract hold projection and owner first-outcome semantics remain unchanged. Attached-work termination at expiry is recorded in the deferred-work ledger for Story 2.9.

Diff inspection caught an attached-failure exception bypassing pending/in-progress/incomplete cleanup refusal. The correction restores cleanup-state precedence. Five regression cases retain both the three expected failures before correction and all passes afterward in the [Builds evidence](../../references/Hexalith.Builds/evidence/2-7/README.md).

The original baselines are preserved. Their full diffs also contain pre-existing protection work and external catalog/merge changes recorded while this workflow ran; acceptance inspection is scoped to Story 2.7 source, tests, documentation, tracking and the Builds gitlink.

## Spec Change Log

## Review Triage Log

Each reviewer finding is classified independently before grouping. B = blind review, E = edge-case review, V = verification-gap review.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| B01: legacy expired attachment rejected | high | patch | The Builds baseline `EndAsync` wrote `expired` with no outcome. The new consistency switch rejects it before normalization, so valid existing owners cannot be listed or cleaned. |
| B02: owner result contradicts attached-failure retention | medium | patch | `RecordTestOutcomeAsync` updates resource disposition to retained, but `TestResultWriter` derives retention and failed ids only from the owner's immutable failure flag. The existing owner result therefore misreports the retained environment; its current retention fields can represent the attached ids without schema/reader changes. |
| B03: expiry during wait loses HXI011 | medium | patch | `ObserveAsync` records expiry during the wait, but the boolean return discards those transitions and `WithdrawAsync` later skips ended records. The cleanup report consequently omits these withdrawn run ids. |
| B04: report is unavailable before teardown | medium | patch | Both cleanup callers only accumulate diagnostics in memory; command output is written after cleanup. A durable diagnostic report must exist before resource stopping, including interrupted cleanup. |
| B05: expiry report can disagree with locked outcome | medium | patch | The owner builds an expired list from an unlocked read, then reports that list after a separate locked update. A pre-expiry end waiting on the lock can commit a terminal outcome between those operations. Reports must use the transitions committed under the lock. |
| B06: attachment inspection failure leaves not-requested cleanup | medium | patch | The initial attachment-read and wait-error catches disable cleanup and return HXT008 without persisting pending disposition, unlike the cancellation-withdrawal catch. The tracked result can contradict the returned pending-cleanup diagnostic. |
| B07: temporal validation accepts forged terminal records | medium | defer | The baseline consistency validator also allowed ended times inconsistent with expiry. Normal writers still enforce exclusive expiry in `EndTime`/`ObservedAt`; stricter validation of externally edited metadata is pre-existing hardening. |
| B08: admission/withdrawal lock order is untested | medium | patch | Current tests cover parallel admissions and static cleanup states, but neither ordering of admission against environment withdrawal. Add focused deterministic lock-order coverage. |
| B09: session AppHost refusal/stop ordering untested | medium | patch | New owner tests override cleanup, and the new down test only checks a workspace. Neither would detect moving a live session AppHost stop back ahead of durable withdrawal. |
| B10: attached-failure explicit release untested | medium | patch | The new retention tests stop before explicit down. They do not assert failed outcome preservation, peer withdrawal, resource release and removal of active tracking together. |
| E01: legacy expired attachment rejected | high | patch | The baseline expired/null record is valid and now rejected by the new consistency switch; same verified defect as B01. |
| E02: wait expiry has no HXI011 | medium | patch | The wait commits expiry and returns only a failure boolean; later withdrawal skips it. Same lost reporting transition as B03. |
| E03: report reaches output after resource stop | medium | patch | The diagnostic object is created before cleanup, but neither emitted nor durably retained there. Same reporting-order defect as B04. |
| E04: passive expiry listing does not persist after retained owner exits | false | reject | The frozen intent explicitly requires passive listing without writes and persistence on the next owner/down/end pass. An idle failure-retained environment has no background expiry writer in this story. |
| V01: live session AppHost survival on refusal untested | medium | patch | Accepted as a pre-verified regression gap: cited tests use workspace-only down, overridden cleanup or unstarted processes. Add a running fake AppHost and retry assertions. |
| V02: released attached-failure retention listing untested | medium | patch | Accepted as a pre-verified regression gap: current release coverage has owner failure only. Extend an attached-failure case through explicit down and listing. |
| V03: legacy expired attachment rejected | high | patch | Checked baseline EndAsync and current verification order; expired/null cannot reach normalization and blocks down. Same verified defect as B01. |

Grouped patch work: legacy expiry compatibility (B01/E01/V03); existing owner-result retention (B02); accurate, durable withdrawal reports (B03/B04/B05/E02/E03); pending cleanup metadata (B06); admission race coverage (B08); live session and retained-release coverage (B09/B10/V01/V02). All fixes stay within existing interfaces and the frozen behavior. B07 is deferred independently.

**Resolution:** All grouped patches are implemented and verified. Legacy expiry normalizes before validation. The existing owner result derives local attached failure retention/release from verified attachment records. Observation and withdrawal atomically retain cumulative `attachment-withdrawals.json` diagnostics under the attachment lock before cleanup; report failure prevents teardown and admission updates remain independent. Attachment inspection/report failures record pending cleanup. Named XML results cover both lock orderings, both live session cleanup entry points and explicit release. The final coordinator build has 0 warnings/errors and all 203 focused tests pass; source hashes match the committed Builds files.

## Verification

**Commands:**
- `dotnet build -nodeReuse:false` on `B/test/Hexalith.Builds.Module.Tests` -- expected: 0 warnings, 0 errors
- `dotnet exec` of the Module.Tests Debug DLL with `-class` filters for the attach, listing and lifecycle classes -- expected: all pass

**Results:** Debug build passed with 0 warnings and 0 errors. All 203 focused cases passed with 0 failed, skipped or not run. [verification.json](../../references/Hexalith.Builds/evidence/2-7/verification.json) records the exact commands; [tests.xml](../../references/Hexalith.Builds/evidence/2-7/tests.xml) records each named result. XML was parsed during the acceptance audit and all 203 results were `Pass`.

| Matrix row | Executed passing coverage (`Async` suffix retained) |
| --- | --- |
| Attached end | `AttachedFirstOutcomeIsImmutableAsync` (3 cases), `EndRequiresSupportedOutcomeAsync` (4 cases) |
| Expiry | `WaitReturnsOnlyAfterEveryHoldEndsOrExpiresAsync`, `ExpiredHoldEndsAtItsExpiryAsync`, `ListingShowsAttachedFailurePassiveWithdrawalAndUnknownLegacyOutcomeAsync`, `WithdrawalRecordsExpiryAndLiveHoldsInOnePassAsync` (2 cases) |
| Owner cancelled | `CancelledWaitWithdrawsBeforeCleanupAndPreservesFirstOutcomeAsync`, `UnrecordableCancellationWithdrawalDefersCleanupAsync` |
| down | `DownRequiresDurableWithdrawalAndReportsBeforeTeardownAsync`, `WithdrawalRecordsExpiryAndLiveHoldsInOnePassAsync` |
| Attached fails, owner passes | `AttachedFailureRetainsPassedOwnerWithoutWaitingForPeerAsync` (before/during wait) |
| Retained, then cancelled | `AttachedFailureThenOwnerCancellationKeepsRetentionAsync` |

`AttachedFailureCannotOverrideCleanupRefusalAsync` adds five passing cleanup-precedence cases. The focused run also includes existing admission, exact-owner cleanup, outcome persistence, retry and result-writer regressions. These deterministic tests do not claim live topology or candidate acceptance qualification.

Post-review coverage also passed: `LegacyExpiredAndTerminalAttachmentsUpgradeBeforeCleanupAsync`, `BlockedWithdrawalReportDoesNotChangeAdmissionSemanticsAsync`, `OwnerWaitExpiryIsDurablyReportedBeforeCleanupAsync`, `InitialAttachmentReadFailureRecordsPendingCleanupAsync`, `ExpiryObservationReportsOnlyCommittedWithdrawalsAsync`, `UnrecordableWaitExpiryReportDefersCleanupUntilDownAsync`, four cases of `AdmissionAndWithdrawalRespectBothLockOrderingsAsync`, four cases of `SessionCleanupKeepsLiveAppHostUntilWithdrawalAndReportSucceedAsync`, and two cases of `AttachedFailuresPopulateExistingRetentionAndReleaseAsync`. Existing attached-failure cases now proceed through explicit down, released retention projection/result, immutable failed outcome, withdrawn peer and removal of active listing/tracking.

Platform completion commit message passed the repository-pinned commitlint CLI 21.2.1; [validation evidence](evidence/2-7/platform-commitlint-final.json) preserves the exact message, command and successful result.
