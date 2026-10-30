---
title: '2.7 Withdraw attached runs and keep failure retention'
type: 'feature'
created: '2026-10-30'
status: 'ready-for-dev'
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
- [ ] `R/CompositionAttachment.cs`, `R/CompositionAttachmentStore.cs` -- outcome model, withdraw, failure-aware wait -- AC1, AC3
- [ ] `R/CompositionAttachAdmission.cs`, `Module.Cli/ModuleCommandApplication.cs`, `ToolDiagnosticFormatter.cs` -- `--outcome`, expiry rule, retained admission -- AC1, AC4
- [ ] `R/ModuleCommandExecutionService.cs`, `R/CompositionEngine.cs` -- withdraw before cleanup, attached-failure retention, `HXI011`/`HXR037` -- AC2–AC4
- [ ] `R/CompositionRunListing.cs` -- failed attached runs, retention, passive withdrawn -- AC3
- [ ] Tests for every matrix row; README; `evidence/2-7/` notes; Platform gitlink bump; deferred-work entry for stopping attached work at expiry (2.9)

**Acceptance Criteria:**
- Given an attached run that reaches its hold limit, when the limit expires, then it is recorded as `withdrawn`: neither a pass nor a failure.
- Given owner cancellation or `down` with active attached runs, then they are recorded and reported as `withdrawn` before any cleanup starts.
- Given a local attached run that fails while the owner passes, when the owner finishes, then the environment is retained and listed with the failed run.
- Given an environment retained after a failure, when a later cancellation happens, then retention remains until `down`.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build -nodeReuse:false` on `B/test/Hexalith.Builds.Module.Tests` -- expected: 0 warnings, 0 errors
- `dotnet exec` of the Module.Tests Debug DLL with `-class` filters for the attach, listing and lifecycle classes -- expected: all pass
