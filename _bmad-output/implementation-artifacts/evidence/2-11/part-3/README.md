# Story 2.11 part 3 — lifecycle caller preparation

This evidence records source implementation and local controls on 2026-11-11.
It supplies no hosted/package lifecycle qualification, Story 2.10 qualification,
accepted tool qualification, candidate acceptance or publication proof. No workflow
was dispatched, package published, or tool/catalog pin updated.

## Exact source basis

| Repository | Spec baseline revision |
| --- | --- |
| Platform | `34dfbcb9a584fd0c3a71d449c75e8e290b9c47bb` |
| Builds | `b1681870d0b08a5fe103133214c67fda7a449eb1` |
| EventStore | `b882c1005050aaebfa70618b5a72c1c8be452281` |
| Parties | `fc9ea3f3b7b8544f8a8f7b8f7912ceb45489766c` |

The resumed implementation run started at actual Platform HEAD
`2251a6078361e42dc62158f8443bd760bb021b3e`; `34dfbcb9…` above remains the
approved spec baseline, not the resumed-run checkout. Builds, EventStore and
Parties started at the exact baseline HEADs shown above.

Implementation is recorded in the local commits below. `checks/worktree-files.sha256` identifies the
changed source/document bytes, including the new opt-in suite and controller
controls. These hashes identify local inputs; they are not release attestations.
The implementation agent preserved the frozen intent and task markers; the
orchestrator then marked tasks after reading the complete diff and auditing the
executed matrix coverage.

## Verification commands and observations

Commands below run from the named repository. Every C# build used Debug and
`-nodeReuse:false`; no service-dependent attached test was run without its real
runner handoff. These local checks allocate only temporary test-controlled
processes/files/sockets, never a qualifying real-service environment.

Builds:

```bash
dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false --nologo -v:q
dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class Hexalith.Builds.ModuleTool.Tests.CompositionResourceClaimStoreTests -class Hexalith.Builds.ModuleTool.Tests.NativeTestExecutorTests -class Hexalith.Builds.ModuleTool.Tests.StartupReadinessTests -class Hexalith.Builds.ModuleTool.Tests.AttachedSuiteExecutionTests
```

The production implementation and initial focused controls built with zero
warnings/errors and passed 160/160 tests in
`checks/builds-four-class-{build,tests}.txt`. The final attached-suite-only rebuild/run
adds both local and CI real-Python immutable-routing cases and passes 47/47 with
zero build warnings/errors, saved as `checks/builds-final-attached-{build,tests}.txt`.
The exact final invocation uses the same build command followed by the test
assembly with `-class Hexalith.Builds.ModuleTool.Tests.AttachedSuiteExecutionTests`. This run uses source project references from the
Builds test project. A first restore-enabled build populated the existing graph;
subsequent builds use `--no-restore` without changing dependency versions.

EventStore:

```bash
dotnet build tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -m:1 -nodeReuse:false -p:UseSharedCompilation=false --nologo -v:q
dotnet tests/Hexalith.EventStore.Testing.Tests/bin/Debug/net10.0/Hexalith.EventStore.Testing.Tests.dll -class Hexalith.EventStore.Testing.Tests.TestEnvironmentDescriptorReaderTests
```

Zero build warnings/errors; 62/62 descriptor-reader controls passed. Outputs:
`checks/eventstore-build.txt`, `checks/eventstore-descriptor-tests.txt`.

Parties (the absolute source root is intentional for this workspace):

```bash
dotnet build tests/Hexalith.Parties.Platform.AttachedLifecycleTests/Hexalith.Parties.Platform.AttachedLifecycleTests.csproj -c Debug -p:HexalithEventStoreFromSource=true -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/platform/references/Hexalith.EventStore -p:UseHexalithProjectReferences=true -m:1 -nodeReuse:false -p:UseSharedCompilation=false --nologo -v:q
dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj -c Debug -m:1 -nodeReuse:false -p:UseSharedCompilation=false --nologo -v:q
dotnet tests/Hexalith.Parties.Ci.Tests/bin/Debug/net10.0/Hexalith.Parties.Ci.Tests.dll -class Hexalith.Parties.Ci.Tests.StandaloneSolutionTests -class Hexalith.Parties.Ci.Tests.PublicRuntimeImageObservationTests
python3 -B -m unittest discover -s tests/ci-lifecycle -v
python3 -B scripts/render-platform-declaration.py --check
python3 -B scripts/test_platform_declaration.py
actionlint .github/workflows/ci-lifecycle-qualification.yml
```

Both C# projects built with zero warnings/errors. Focused Ci.Tests passed 28/28;
controller controls passed 154/154; declaration controls passed 7/7. Renderer
check and actionlint exited zero with no output. Saved outputs are the
`checks/parties-*.txt` files. The Ci.Tests build compiles the changed owner-only
native diagnostic binder without starting its service-dependent integration fixture.

## Matrix-to-control mapping

All automated fixtures below are local controls and supply no hosted credit.

| Required mechanism | Runtime caller case | Focused evidence |
| --- | --- | --- |
| Live-owner port collision and preserved peer | `collision` | Builds `ExplicitEndpointConflictPreservesOwnerAndPeer`, `ExplicitOccupiedSocketRollsBackClaims`, `InvalidExplicitEndpointIssuesNothing`, `ExplicitPortParsingRefusesInvalidRequestsAsync`; `ParentStartupTranslatesExclusiveClaimFailuresAsync` checks HXR031 propagation. |
| Exact original startup deadline/override | `startup-timeout` | Builds `StartupTaskIsStoppedAtTheDeadlineAsync`, `SelectedStartupTokenExpiresDuringModelBuildAndPreservesSummaryAsync`, `StartupObservationExcludesDelayedTeardownAsync`; Python `test_timeout_requires_original_clock_duration_and_override`. |
| Failure preceding operator cancellation | Separate `failure-then-cancellation` dispatch | Python `test_failure_then_operator_cancellation_preserves_failure_without_cancellation_credit`; existing cancellation-terminal/cleanup tests reject changed first outcome/time. Ready artifact and actual operator API receipt remain required externally. |
| Active attached work withdrawn before owner teardown | `owner-cancellation`, `cancel-execution` | Builds `OwnerCancellationEndsAttachedWorkBeforeOwnerTeardownAsync`, `CiOwnerCancellationWithdrawsActiveAttachmentAsJobCancelledAsync`, `DownFromAnotherTerminalEndsAttachedWorkBeforeOwnerResourcesStopAsync`; Python phase checks require the original attached process. |
| Intentional attached failure and independent owner outcome | `attached-failure` | Builds `AttachedFailureRetainsOnlyLocallyAndLeavesOwnerUntouchedAsync`; Python `AttachedResultTests` require original ledger/terminal, real native report and content hashes, and preserve the retained first result. |
| Original hold expiry, work/access stop, peer preservation | `hold-expiry` | Builds `ExpiryStopsRunningChildAndIgnoresLateReportAsync`, `ExpiringAttachmentLeavesPeerAndOwnerRunningAsync`, `SurvivingChildIsReportedWithoutChangingOutcomeOrExpiryAsync`; SDK `AttachedExpiryCancelsFixtureRequests`; Python result/expiry guards. |
| Expired/ended/foreign/mismatched handoff before clients | `invalid-handoff`, expired reconnect within `hold-expiry` | SDK `AttachedFixturesRequireOriginalActiveHold` covers active, expired, ended, foreign, missing and snapshot mismatch; Builds owner reselection and malformed attachment controls remain required; Python rejects rebound job/revision/profile/lineage/expiry. |
| Attachment launch interruption and exact admission recovery | All attached cases and finalizer | Python `test_post_constructor_interruption_recovers_the_real_attached_gate_without_receipt`, `test_recovery_uses_exact_admission_without_an_attached_owner_receipt`, ambiguity/identity/expired snapshot controls, failed intent save, and missing-admission-after-release refusal. |
| Immutable public routing proof with isolated outputs | All attached cases | Builds `AttachedRoutingSnapshotsRequireExactOwnerAsync`, `NativeExecutionArchivesThePreChildRoutingSnapshotAsync`, `OpaqueHandoffCredentialsCannotEnterRoutingSnapshotsAsync`; final attached passing theory exercises required routing in local and CI contexts. Owner gates/live access observations remain refused. |
| Cleanup failure never passes or skips sibling cleanup | Every case and independent finalizer | Existing cleanup matrix/lock/recovery controls plus Python `test_attached_finalization_failure_does_not_skip_any_owner_cleanup`, failed/leftover work/access and surviving private-file controls. |

Existing owner persistence assertions, ordinary passing/failing cases, metadata
lock retries with active peers, and partial-startup recovery remain enrolled.
Both solution files exclude the new attached fixture from ordinary builds.

## Immutable schema bytes

`checks/schema-bytes.sha256` records Platform schemas, Builds mirrors and the SDK
embedded descriptor mirror. Each file was compared byte-for-byte to its owning
repository's unchanged HEAD; mirrors agree:

- Descriptor: `bf1e46acc773991037daaae20dff1d815faf4af13ab7887d88339609607ff357`
- Result: `9773678febac38025fae837154341d9036ce8aa94e66b0d30c7e15ee668d7e39`

Policy v1, schemas, pins and local failure retention semantics are unchanged.

## Release and hosted evidence dependencies

The committed package workflow still selects its existing public tool/SDK pins.
It cannot execute the new options/fixture support until compatible Builds and
EventStore Testing releases are independently published and deliberately adopted.
This change neither publishes those prerequisites nor updates pins.

No live hosted or local real-service campaign, cancellation API call, published
artifact verification, package parity run or complete Story 2.10 qualification was
performed. Each selected hosted case must still record exact source/run/attempt/job,
actual mechanisms, original clocks/expiry, classified results and verified cleanup.
The three operator cancellation dispatches require their ready artifact before
normal cancellation and the actual API request/response/time retained externally.
Any missing proof, missed Starting/hold window, ambiguous launch, unavailable
prerequisite, failed termination, discovery uncertainty or leftover is a failure.
No retry-to-green or increased budget supplies credit.

## Review corrections and final verification

Three independent review layers reported 15 findings. The orchestrator recorded
each verdict in the spec, grouped 14 into 11 corrections, and rejected one low
finding under the workflow's complexity rule. The complete v1 reader remains in
the shared producer, which validates results before atomic publication.

Corrections cover the actual CI withdrawal cause, original running resource
inventory at termination, held artifact bytes, the shared diagnostic allowlist,
private-file entries, original CLI failures, an identifiable intentional native
assertion, selection-specific fixture builds, and executable orchestration,
signal-time identity and valid-port command regressions. A missed teardown
observation window fails instead of inferring order from stopped containers or
metadata locks.

After reading the correction diff, the orchestrator reran all required checks
from the final source tree. The `checks/review-pass-1-*.txt` files include exact
commands and exit codes:

| Final check | Result |
| --- | --- |
| Builds affected four classes | 163/163, no skips or unexecuted tests |
| EventStore descriptor reader | 62/62, no skips or unexecuted tests |
| Parties focused C# controls | 28/28, no skips or unexecuted tests |
| Parties controller controls | 167/167 |
| Parties declaration controls | 7/7 |
| Four Debug builds | Zero warnings/errors |
| Declaration renderer and actionlint | Exit 0 |

These are 427 passing final test cases; the earlier 160/160 and 47/47 runs are
initial evidence and are not added again. `checks/review-pass-1-matrix-audit.json`
maps every frozen row to passing final xUnit XML entries or the named passing
Python case. All five schema hashes still match owning HEAD bytes. The 33 tested
inputs are retained in `checks/worktree-files-final-verification.sha256`;
`checks/worktree-files-pre-review.sha256` preserves the original 32 inputs.
The final `checks/worktree-files.sha256` differs from the tested inputs only in
the Parties documentation's removal of the word “uncommitted” after local delivery.

No real-service campaign or hosted qualification was added by these corrections.

## Local delivery

| Owning repository | Local commit |
| --- | --- |
| Builds | `bfda07eea7dcc8ef0a6da0fa14643aa70eebec78` |
| EventStore | `04e56e1c1d4657d6fbdea0aa03a5f434c0fe51e5` |
| Parties | `e35b8c1eff0184c95444af9a01a8beba78f87110` |

The exact messages passed each repository's pinned commitlint 21.2.4 before
commit. `checks/local-commit-messages.json` and `checks/local-commits.json` retain
the validation and canonical identifiers. These local-only workflow commits do
not push, publish, update tool/catalog pins, or advance Platform's gitlinks.
