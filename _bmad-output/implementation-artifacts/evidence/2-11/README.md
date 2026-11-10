# Story 2.11 (part 1) — supervised attached declared-suite runs

Specification: [spec-2-11-supervise-attached-declared-suite-runs.md](../../spec-2-11-supervise-attached-declared-suite-runs.md).
This is offline Builds implementation evidence. No live service, package-mode CI,
tool acceptance, qualification or pin is claimed, and Story 2.10 is not closed here.

## Revisions

| Item | Value |
| --- | --- |
| Builds baseline | `f81486cb532e2e166f5c363fa8e9f75c1313c2a1` |
| Builds branch | `feat/2-11-supervised-attached-suites` (scratch worktree, no upstream, not pushed) |
| Builds commits | `08db49e8c525d3a8f6116eb4b8b11278d415598b` `feat(module): supervise runner-owned attached declared-suite runs`; `ca842264ae91b38fd35c095147d8fbcbda79bb6e` `fix(module): harden attached-suite supervision after review` (both commitlint-validated) |
| Platform baseline | `ff0b9c73f4d5454915da8da5d1500a56d6667e78` |
| `references/Hexalith.Builds` | untouched (gitlink unchanged) |

## What changed

`hexalith-module test --manifest <owner-manifest> --profile <suite> --attach-to <owner-run-id>
[--hold-seconds N] [--attachment-request-id <issued-id>]` now runs one declared suite as a
runner-owned attached run of a **live** owner `test` command, replacing the former HXC002
blanket refusal.

- **Attacher** (`AttachedSuiteCommand`): validates the suite (single profile, same mode,
  compatible with the owner suite, not gate-requiring), admits the hold through the existing
  `CompositionAttachAdmission` with an execution request recorded on the hold, waits for the
  owner to pick it up, relays the attached result and exit code (`0` passed, the failing exit for
  `failed`, `130` for `cancelled`/`withdrawn`). Its own Ctrl+C records a cancel request. A
  request the owner never picks up is recorded `failed` (`startup-failure`) with not-applicable
  termination; if the owner vanishes mid-run the attacher stops tagged leftovers and records them.
- **Owner** (`AttachedSuiteSupervisor`): renews a liveness lease in the owner diagnostics
  directory, picks requests up under the attachment lock, re-selects the suite from its own
  manifest, creates `<owner-workspace>/attachments/<attachedRunId>/` with its own descriptor
  (`role: attached`, baseline-only), credential channel (bound to the attached run), report
  paths and plan/readiness snapshots. It mints the bearer from its in-memory key with subject
  `g4-attached-tests:<attachedRunId>` and `exp = min(execution deadline, expiresAt)`, tags the
  child with the attached run id, then races child end against `expiresAt` (runner UTC clock),
  withdrawal, the attacher's cancel request and owner drains. It records the first outcome, ends
  access (tree kill, run-tag scan, credential and descriptor deletion), records termination and
  publishes `diagnostics/<attachedRunId>/test-result.json`.
- **Ordering**: owner cancellation, explicit `down` and CI job end keep owner state → durable
  withdrawal, then drain attached work and access and record termination, and only then stop
  owner resources. An in-process `down` drains the session's supervisor; a `down` from another
  terminal waits up to half the stop budget for the live owner, then kills processes still
  tagged with the attached run and records them (`HXI013`; `HXR038` with leftovers keeps cleanup
  incomplete and retryable).
- **Records**: the internal `hexalith.g4-run-attachments.v1` record gains optional `execution`
  (execution kind `attached-suite`, request, pickup, attempt, launch, cancel request, cause,
  exit, publication) and `termination` (owned work, environment access, leftovers, recorder,
  bearer expiry). Existing documents stay readable; ended hold-only holds record
  `not-applicable` items. `attach --end` refuses a runner-owned suite (`HXR036`
  `runner-owned-attachment`).
- **Stop failures**: `CompositionProcess.RunSelectedAsync` now surfaces an unconfirmed tree kill
  as surviving PIDs instead of swallowing it. Survivors or undeletable access files are
  `failed` termination items with `leftoverWorkIds`; outcome and expiry never change.
- **No live owner**: attached-suite execution is refused before admission (`HXR033`
  `owner-not-live`); hold-only `attach` still works. A failed local owner command stays open
  until its runner-owned attached runs end.

- **Review hardening (`ca842264`)**: drains retry and report every recorded runner-owned run
  whose termination failed or whose result is unpublished (HXR038 keeps cleanup incomplete); an
  unverifiable drain refuses teardown like an unrecorded withdrawal (HXR037); children are
  signalled before outcomes are recorded; forced stops share one allowance and publish; only
  tag-scan-listed processes are killed or reported; holds ended before pickup get no workspace or
  credentials; fallback publication never overwrites an owner result; hold-only lineages stay
  hold-only (`hold-only-lineage`) and reconnects naming another suite refuse
  (`lineage-suite-mismatch`); attached `dotnet test` children disable node reuse, the MSBuild
  server and shared compilation.

Not changed: policy v1, the v1 schemas and reader invariants, the executing-tool binding and an
attached run's own startup duration (both stay unpopulated; part 2), the Parties caller (part 3),
tool acceptance/qualification/pins (Story 2.10). Hosted, developer-started and cross-job targets
and gate/access-observation suites remain refused.

## Matrix coverage (`AttachedSuiteExecutionTests`, real Python children)

| Matrix row | Local | CI |
| --- | --- | --- |
| Pass | `PassingAttachedSuiteEndsWorkAndAccessAsync(false)` | `PassingAttachedSuiteEndsWorkAndAccessAsync(true)` |
| Attached failure (local retained, CI not) | `AttachedFailureRetainsOnlyLocallyAndLeavesOwnerUntouchedAsync(false)` | `…(true)` |
| Expiry mid-run, late passing report ignored | `ExpiryStopsRunningChildAndIgnoresLateReportAsync(false)` | `…(true)` |
| Peer expiry leaves the other attachment and owner running (AC2) | `ExpiringAttachmentLeavesPeerAndOwnerRunningAsync` | — |
| Owner withdrawal: owner cancelled | `OwnerCancellationEndsAttachedWorkBeforeOwnerTeardownAsync` (`owner-cancelled`) | `CiOwnerCancellationWithdrawsActiveAttachmentAsJobCancelledAsync` (`ci-job-cancelled`) |
| Owner withdrawal: `down` from another terminal (AC1) | `DownFromAnotherTerminalEndsAttachedWorkBeforeOwnerResourcesStopAsync` | — |
| Owner withdrawal: CI job end | — | `CiJobEndWithdrawsActiveAttachmentBeforeCleanupAsync` (`ci-job-completed`) |
| Own cancel (owner and peer untouched) | `OwnCancellationEndsOnlyThatRunAsync` | — |
| Stop failure (survivor, outcome unchanged, no extension) | `SurvivingChildIsReportedWithoutChangingOutcomeOrExpiryAsync`; vanished owner: `DownStopsAttachedWorkOfVanishedOwnerAsync(true)` (HXR038, down not complete) | — |
| Retry: same lineage and expiry, no second child; refused after end/expiry | `RetryReconnectsLineageAndRefusesEndedOrExpiredAsync`; `ReconnectNamingAnotherSuiteIsRefusedAsync` (`lineage-suite-mismatch`); `HoldOnlyLineageNeverBecomesRunnerOwnedAsync` (`hold-only-lineage`) | — |
| Refused: filter, evidence, candidate, receipt, with-owner, gate suite, incompatible owner, owner-passed | `RefusedRequestAdmitsNothingAndStartsNoChildAsync` (8 cases) | — |
| Refused: usage (hold/request id without `--attach-to`, two profiles, mode mismatch, v1 manifest, Memories `functional-retention`) | `UnsupportedAttachedCommandIsUsageRefusalAsync` (6 cases) | — |
| Refused: no live owner / stale lease (expired, closed, exited holder, reused PID); hold-only still works | `FailedLocalOwnerThatExitedRefusesAttachedSuitesButKeepsHoldOnlyAttachAsync`; `StaleLeaseRefusesAttachedSuiteBeforeAdmissionAsync` (4 cases) | — |
| Owner re-selection refuses gate suite / changed entry before launch | `OwnerReselectionRefusesUnsupportedRequestBeforeLaunchAsync` (2 cases) | — |
| Vanished owner: `down` stops tagged child, records (`down`) and publishes | `DownStopsAttachedWorkOfVanishedOwnerAsync(false)` | — |
| Vanished owner mid-run: attacher stops tagged child, records (`attacher`) and publishes | `AttacherTakesOverWhenOwnerVanishesMidRunAsync` | — |
| Never picked up → `failed`/`startup-failure`, not-applicable termination | `RequestNeverPickedUpIsRecordedAsFailedAsync` | — |
| Failed local owner stays open until attached runs end | `FailedLocalOwnerStaysOpenUntilAttachedRunsEndAsync` | — |
| Own credential channel bound to the attached run, deleted at access end | `AttachedRunGetsItsOwnCredentialChannelAsync` | — |
| Hold-only attach preserved: not-applicable termination; `attach --end` refuses a running suite | `HoldOnlyAttachRecordsNotApplicableTerminationAsync`; `AttachEndRefusesRunningRunnerOwnedSuiteAsync` | — |

CI rows run with simulated GitHub Actions variables. AC3 is asserted for every published attached
result through `TestEnvironmentContractReader.ReadResult` (`role: attached`, matching hold end
reason/cause, populated termination, unpopulated tool and attached startup duration).
`ModuleTestSelectionTests.AttachmentRunsAsAttachedSuiteInsteadOfRefusingAsync` inverts the former
attachment refusal. Per-case timings: [AttachedSuiteExecutionTests-cases.txt](AttachedSuiteExecutionTests-cases.txt).

## Test results

All runs use the worktree Debug build (`dotnet build test/Hexalith.Builds.Module.Tests -c Debug
-nodeReuse:false`: 0 warnings, 0 errors, fresh DLL timestamps checked before each run) via
`dotnet exec bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll`, one `-class` per invocation.

Final build `ca842264` (2026-11-10 11:31 CET), classes covering the post-review edits:

| Class | Total | Failed | Log |
| --- | --- | --- | --- |
| AttachedSuiteExecutionTests | 45 | 0 | [log](AttachedSuiteExecutionTests.txt) |
| AttachAdmissionTests | 133 | 0 | [log](AttachAdmissionTests.txt) |
| TestResultWriterTests | 24 | 0 | [log](TestResultWriterTests.txt) |
| NativeTestExecutorTests | 48 | 0 | [log](NativeTestExecutorTests.txt) |
| CiRunLifecycleTests | 28 | 0 | [log](CiRunLifecycleTests.txt) |

Build `08db49e8` (before review fixes), remaining spec-listed classes, whose covered files the
review fixes did not change:

| Class | Total | Failed | Log |
| --- | --- | --- | --- |
| ModuleTestSelectionTests | 46 | 0 | [log](ModuleTestSelectionTests.txt) |
| TestEnvironmentContractReaderTests | 238 | 0 | [log](TestEnvironmentContractReaderTests.txt) |
| ModuleRunnerReviewRegressionTests | 51 | 0 | [log](ModuleRunnerReviewRegressionTests.txt) |
| LocalRunSourceReviewTests | 24 | 0 | [log](LocalRunSourceReviewTests.txt) |
| LocalRunLifecycleTests | 59 | 0 | [log](LocalRunLifecycleTests.txt) |

Full assembly on `08db49e8`: **2,967 total, 0 failed, 5 skipped (pre-existing gated skips),
561 s** — [summary](full-module-tests.txt).

Coordinator verification on `ca842264`:
- **Build:** `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -nodeReuse:false` gave 0 warnings and 0 errors, with fresh DLLs.
- **Full assembly:** 2,987 total, 1 failed, 5 skipped (pre-existing gated skips), 628 s.
- **The one failure:** `ModuleTestSelectionTests.PartialStartupFailureRetainsOwnedProcessAsync` (`process.HasExited` true at line 506). It passed 3 of 3 times when rerun alone.
  - Its process double is tagged with the shared fixture constant run ID `0123456789abcdef0123456789abcdef`. Other test classes, running in parallel, use the same ID when they scan for tagged processes and kill them.
  - The new attached-suite tests give every owner a fresh run ID, and kill only processes tagged with an attached run's ID.
  - It is therefore a pre-existing test-isolation flake, not a regression.

An earlier full run on an intermediate build (2,966 tests) had two failures.
`LocalRunLifecycleTests.ExhaustedExecutionLifetimeBeforeNativeReturnRecordsTimeoutCauseAsync`
was a real regression: it invokes the private `ExecutePlatformAsync` by reflection and the new
optional parameter changed its arity; the call now passes the extra argument (59/59).
`ModuleTestSelectionTests.PartialStartupFailureRetainsOwnedProcessAsync` (a process double tagged
with the shared fixture run id ended early under parallel load) passed alone and in both later
runs; it is treated as a load flake, not a regression.

## Limits and risks

- Offline proof only: Python children and process doubles, no live services, AppHost or
  package-mode CI. CI cases use simulated GitHub Actions variables.
- The stop-failure cases use a test-only discovery seam that reports an unkillable tagged
  process; a real tree-kill timeout is surfaced by `RunSelectedAsync` but not reproduced. Only
  processes the run-tag scan lists are killed or reported, so a reused PID is never touched.
- Only the run binding (`runId`, token subject) and expiry are narrowed: the copied endpoint
  secrets (environment-wide Dapr API token) and the bearer's tenant/domain/permission scopes are
  the environment's own; the access termination detail and the Builds README state this.
- A `down` from another terminal gives the live owner half the stop budget before killing
  tagged leftovers itself; very short stop budgets leave less time for the remaining teardown.
- Owners whose readiness requires routing-catalog observations fail an attached run as
  `startup-failure` (after admission) rather than refusing it before admission.
- The bearer is not revoked service-side; access ends when its holder is gone and its files are
  deleted, as decided on 2026-11-10.
