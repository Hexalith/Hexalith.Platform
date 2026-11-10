# Story 2.11 part 2 — lifecycle observation implementation evidence

The implementation is in `/tmp/hexalith-builds-2-11-part-2-20261110`, branch
`feat/2-11-lifecycle-reporting`, at local commit
`f118c2440101fe67be2c7f7a569cbe2257223a16`, from Builds revision `52ea174c70f1afe77b52a2ccf06bb53afa5e962d`. The authoritative specification is
[spec-2-11-report-lifecycle-observations.md](../../../spec-2-11-report-lifecycle-observations.md).
Platform HEAD during verification was `d7c74e8a5d5627a8773fb36652813fe743de3d6f`. The ordinary
Builds checkout and Platform gitlink remain unchanged. No remote operations were performed;
the implementation commit is local and has not been pushed.
[builds-implementation.patch](builds-implementation.patch) preserves the complete committed
Builds change for review and recovery. [changed-files.json](changed-files.json) records its
working-tree source-file SHA-256 identities; [verification.json](verification.json) records revisions, actual CLI version,
binary timestamps and unchanged schema hashes.

## Observations implemented

Startup elapsed seconds freeze at the first readiness, failure, timeout or observed
interruption, before diagnostic collection and teardown. First/final results, owner state
and startup summaries reuse the measurement and original request/deadline/override.
Setup observations are captured before issuance bookkeeping. If an outcome lock is contended, the runner holds the first observation in memory and
persists it at the first successful publication, preserving existing refusal behavior and
stopping bounds. Recovery timestamps its observed interruption; it cannot recover an
unobserved death instant. Unknown legacy measurements remain unavailable.

The executing identity is `hexalith-module` and its actual CLI informational version
without build metadata. Its content identity is the SHA-256 of exact compact UTF-8 JSON
manifest bytes, without a BOM or final newline. Manifest property order is `schema`,
`algorithm`, `binaries`; values are `hexalith.executing-tool-manifest.v1`, `sha256`, and
an ordinal filename-sorted array of `Hexalith.Builds.Module.Cli.dll` and
`Hexalith.Builds.Tooling.dll`. Each binary entry contains `name`, then `contentIdentity`
(`sha256:` plus the lowercase SHA-256 of the entire binary file). The manifest itself
has the same `sha256:` digest form, and exact bytes remain a classified internal
`executing-tool-<digest>.json` diagnostic. Hash failure refuses fresh provisioning;
cleaners and reconnects retain the original identity. The digest identifies binary content
and supplies no package provenance or qualification. Existing qualification bindings are
preserved, and newly issued qualification fields remain unavailable.

Owner termination distinguishes observed native work, environment work and access.
Unissued items are not applicable, retained environment/access stays pending, and observed
native exit can be terminated independently. Verified exact cleanup advances environment
and access to terminated; incomplete or unverifiable cleanup reports failures, classified
diagnostics and known leftovers. Passing tests alone supply no cleanup or revocation proof.
Attached records retain part 1's stopping/access observations, original expiry, and the live
owner's executor and environment startup measurement. Missing legacy ready-owner work/access
history stays conservative until cleanup verifies absence; it never claims definite nonissuance. The hosted-workflow helper retains
these fields and original hash-checked diagnostic bytes; it produces no second identity.

## Verification

Parent verification rebuilt the corrected source and passed all **290 xUnit cases** in
nine affected classes and **70 Python tests**, with no skipped, unexecuted or failed cases.
The Debug build reported zero warnings and errors. All 28 changed source-file hashes stayed
unchanged throughout the build/test run. CLI, Tooling and test binaries are newer than their
sources. Verification ran before the local commit; its source bytes match that commit exactly.
Both v1 schemas match the baseline and Platform authority byte for byte; the reader
is unchanged. [verification.json](verification.json) and [test-results.json](test-results.json)
retain exact commands, revisions, hashes, timestamps and results.

| xUnit class | Passed | Evidence |
| --- | ---: | --- |
| `CompositionProcessTests` | 10 | [log](CompositionProcessTests.log), [XML](CompositionProcessTests.xml) |
| `ExecutingToolIdentityTests` | 10 | [log](ExecutingToolIdentityTests.log), [XML](ExecutingToolIdentityTests.xml) |
| `TestResultWriterTests` | 29 | [log](TestResultWriterTests.log), [XML](TestResultWriterTests.xml) |
| `LocalRunLifecycleTests` | 67 | [log](LocalRunLifecycleTests.log), [XML](LocalRunLifecycleTests.xml) |
| `NativeTestExecutorTests` | 50 | [log](NativeTestExecutorTests.log), [XML](NativeTestExecutorTests.xml) |
| `StartupReadinessTests` | 34 | [log](StartupReadinessTests.log), [XML](StartupReadinessTests.xml) |
| `StartupCoordinationTests` | 16 | [log](StartupCoordinationTests.log), [XML](StartupCoordinationTests.xml) |
| `CiRunLifecycleTests` | 29 | [log](CiRunLifecycleTests.log), [XML](CiRunLifecycleTests.xml) |
| `AttachedSuiteExecutionTests` | 45 | [log](AttachedSuiteExecutionTests.log), [XML](AttachedSuiteExecutionTests.xml) |

Commands ran from the isolated Builds worktree. The build used `-c Debug -nodeReuse:false`;
each xUnit command used `dotnet exec` with one `-class` filter plus verbose/XML output.
Python ran `python3 -m unittest discover -s Github/run-module-integration/tests`.
See [build.log](build.log), [python-unittest.log](python-unittest.log) and the complete command
arrays in [test-results.json](test-results.json).

The initial implementation and its verification evidence are preserved in
[pre-review.tar.gz](pre-review.tar.gz), including its source diff. The first lifecycle run had
three lock-contention regressions; the first attached run had 16 stale-fixture assertions.
Those failures are retained in [lifecycle output](LocalRunLifecycleTests.initial-failed.log)
and [attached output](AttachedSuiteExecutionTests.initial-failed.log). All corrected cases
passed in the final parent run. Review findings and individual verdicts are recorded in the
spec's Review Triage Log. [review-regressions.json](review-regressions.json) identifies each
executed added review regression. The additional legacy-ready recovery test keeps missing
work/access history conservative until verified cleanup; known unissued controls stay
not-applicable. Immediate causal outcomes still commit before separate per-entry ledger I/O.

## Review pass 2 corrections (2026-11-10)

A second code review of Builds `52ea174c..f118c244` used four independent layers. It found
15 patch entries, 1 deferred entry and 23 rejected findings; the spec's Review Findings section
records every verdict.

**Behaviour corrections**

- Selected-suite prerequisite refusals are again `unavailable` with no run, state, receipt or
  retained owner.
- A child that never launched keeps the baseline HXT007 classification. The separate
  `Launched` flag stays exact.
- Setup cancellation before the declared deadline is known propagates, so the caller reports
  it with the declared deadline and override.
- All setup-cancellation recording failures are contained in the engine and reported as
  HXC130 plus HXT008.
- Unchanged termination statuses keep their first `observedAt`.
- Pre-launch native-ledger I/O runs inside the gate's existing failure handling.
- A successful final ledger write clears an earlier launch-metadata warning.
- The unused public `CreateInitialState` parameter was removed.

**Added regressions**

- An engine-level selected prerequisite refusal (no run, state or receipt).
- A pre-resolution cancellation that propagates.
- An owner cancellation during the probe that publishes a contained `run-cancelled` result.
- The owner termination diagnostic's `runId`.
- Conflicting `tool` and `actualDurationSeconds` snapshots.
- A failing launch callback that stops its child.
- An unwritable ledger that reports HXT008.
- The retained local startup-failure row: native `not-applicable`, environment `pending`,
  access `not-applicable`.

**Verification**

The Debug build had zero warnings and errors after one SA1118 test-only correction. All
**298 xUnit cases** in the nine affected classes passed, as did the **70 Python tests**. The
full Module.Tests assembly passed 3,024 of 3,029 cases with 0 failures and 5 pre-existing
environment-gated skips.

[review-pass-2/](review-pass-2/) holds the per-class logs and XML,
[test-results.json](review-pass-2/test-results.json), [build.log](review-pass-2/build.log),
the compressed full-assembly XML and the exact
[review-pass-2.patch](review-pass-2/review-pass-2.patch), committed as Builds `4ce510b2b263f8e4ea469638a130f08776ba27a3` on
`feat/2-11-lifecycle-reporting`.

## Matrix audit

Every covering method below appears as passing in the linked class XML and verbose log.
[matrix-audit.json](matrix-audit.json) lists exact executed case names. The delayed teardown
test snapshots the first published result before a delayed cleanup helper and compares its
frozen duration with the final result, durable owner state and startup summary; its timeout
and cancellation cases both ran. Recovery and CI retry tests preserve the executor under a
cleaner whose identity-capture seam deliberately throws if invoked. Attached fallback tests
also forbid recapturing the reconnecting executor.

| Approved scenario | Executed coverage |
| --- | --- |
| Ready | `StartupReadinessTests.ReadyRunRecordsDeadlineAndDebugProcessIdsAsync` (2 passing cases); `StartupCoordinationTests.DeadlineAtFinalReadyCommitRefusesSuccessAsync` (1 passing cases) |
| Partial | `StartupReadinessTests.StartupObservationExcludesDelayedTeardownAsync` (2 passing cases); `StartupReadinessTests.StartupTaskFailureNamesTheTaskAndPreservesDiagnosticsAsync` (2 passing cases); `StartupReadinessTests.StartupTaskIsStoppedAtTheDeadlineAsync` (2 passing cases); `StartupCoordinationTests.SetupCancellationRecordsOriginalStartupAndUnissuedWorkAsync` (1 passing cases) |
| Recovery | `LocalRunLifecycleTests.RecoveryReadsNativeLedgerAfterFirstOutcomeCommitAsync` (1 passing cases); `LocalRunLifecycleTests.RecoveryPreservesExecutorAndFreezesObservedInterruptionAsync` (2 passing cases); `LocalRunLifecycleTests.LegacySelectedOwnerRecoveryPreservesUnknownFieldsAsync` (4 passing cases); `TestResultWriterTests.LegacyExecutorRemainsUnavailableWithDiagnosticsAsync` (1 passing cases) |
| Retained | `LocalRunLifecycleTests.NativeOutcomeControlsLocalCleanupAsync` (6 passing cases); `NativeTestExecutorTests.PreparedScopeBindsNativeTokenAndDomainHandoffAsync` (1 passing cases); `AttachedSuiteExecutionTests.AttachedFailureRetainsOnlyLocallyAndLeavesOwnerUntouchedAsync` (2 passing cases) |
| Cleanup | `CiRunLifecycleTests.CiTerminationDiscoveryFailureAndRetryPreserveFirstIdentityAsync` (1 passing cases); `LocalRunLifecycleTests.DiscoveryFailureReportsClaimsAndRetryPreservesPeerAsync` (2 passing cases); `LocalRunLifecycleTests.OutcomeLockImmediatelyRefusesCleanupUntilExactRetryAsync` (1 passing cases) |
| Attached | `AttachedSuiteExecutionTests.PassingAttachedSuiteEndsWorkAndAccessAsync` (2 passing cases); `AttachedSuiteExecutionTests.DownStopsAttachedWorkOfVanishedOwnerAsync` (2 passing cases); `AttachedSuiteExecutionTests.AttacherTakesOverWhenOwnerVanishesMidRunAsync` (1 passing cases); `AttachedSuiteExecutionTests.RetryReconnectsLineageAndRefusesEndedOrExpiredAsync` (1 passing cases) |

Binary identity content-change/refusal coverage is in
`ExecutingToolIdentityTests.ChangedBinaryBytesChangeManifestIdentityAsync` (both binaries)
and `HashFailureRefusesBeforeProvisioningAsync`. Artifact retention and identical-retry
coverage is in Python's
`test_published_redacted_diagnostics_survive_cleanup_byte_for_byte_and_retry`, with mismatched
and malformed manifest refusals also executed.

## Verification limits

This is local Debug implementation evidence using process, Docker and AppHost controls.
It is not hosted package-mode qualification, published package provenance, independent tool
acceptance, a Parties caller adoption or Story 2.10/part 3 completion. No policy, v1 schema,
contract reader, qualification reference, catalog/tool pin or hosted dispatch was changed.
Original termination outcomes and attachment holds remain distinct from evolving cleanup.

Local commit messages passed the owning repositories' pinned commitlint 21.2.4 policies;
[validation evidence](commitlint-validation.json) records the exact messages and commands.

## Review pass 3 corrections (2026-11-10)

All five approved review findings (R3-P1–R3-P5) are resolved in the existing Builds
worktree on top of `4ce510b2b263f8e4ea469638a130f08776ba27a3`. Verification preceded the commit. With user approval on 2026-11-11, the changes were
committed as [`7461fb05`](https://github.com/Hexalith/Hexalith.Builds/commit/7461fb05ad170375fc0df66e6a39a4d2432e6247)
and pushed to `feat/2-11-lifecycle-reporting`. The twelve source-file hashes still
match the verified run. No release or Platform gitlink update was performed.
[review-pass-3-push.json](review-pass-3-push.json) records remote verification and
successful validation of both exact commit messages with commitlint 21.2.4.
[review-pass-3.patch](review-pass-3.patch) preserves the
complete correction diff for review and recovery.

Selected native executions use a fresh private process tag to discover and stop
tagged descendants within the original stopping allowance, preserving retained
owner services and other native executions. Pre-launch PID/start observations
exclude unrelated pre-existing protected processes on supported hosts; new
unreadable work and partial scans remain unverified with known identities retained.
Causal outcomes are recorded before descendant stopping, and final termination is
persisted even when that recording throws. Unchanged native/access/environment
statuses retain their observation times, while recovery captures its interruption
before cleanup-marker I/O. Live-child coverage now asserts the durable PID, bearer
expiry and pending statuses before completion, using an atomically published marker.

The follow-up independent reviews produced seventeen findings. Thirteen formed
nine small correction groups within the five approved findings; four were rejected
and no new work was deferred. The spec's pass-3 implementation-check table records
every verdict. Existing deferred entries, including the live-startup question,
were not changed.

Final parent verification rebuilt with **zero warnings/errors**, then passed
**339/339 C# cases** in eleven affected classes, with no skips or unexecuted cases.
The **70/70 Python retention tests** also passed. All six approved matrix rows and
all five corrections have executed passing coverage, including real detached work,
protected-process uncertainty, partial-scan cancellation, expired stopping and a
throwing first-outcome callback. All twelve changed file hashes stayed stable
through the final C# run; test/CLI/Tooling binaries match and are newer than their
sources. V1 schemas are byte-identical to Platform authority and baseline; the v1
contract reader and frozen intent are unchanged.

[review-pass-3-verification.json](review-pass-3-verification.json) records commands,
case counts, matrix/regression mappings, source/binary hashes, baseline revisions,
CLI version and invariant checks. [review-pass-3-test-output.tar.gz](review-pass-3-test-output.tar.gz)
retains final XML/logs, the verification script, and initial failed runs that exposed
the protected-process scanner and missing-ledger fallback regressions.

This is focused local Linux Debug evidence. The supported-host private-tag test
also targets Windows/macOS, but their native inspection paths were not executed
here. This is not a new full-assembly run or hosted/package qualification. Discovery
proves absence of processes retaining the ownership tag; children that remove those
tags remain outside the established tag-based cleanup boundary. Part 2 is done;
Story 2.11 remains in progress for the separate Parties caller work.
