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
It cannot execute the new options/fixture support until compatible Builds, Platform
composition and EventStore Testing releases are independently published and deliberately adopted.
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

## Review pass 2 — current-main corrections (2026-11-12)

`checks/review-pass-2-input-revisions.json` records the exact current input HEADs
and incoming status, preserving the original spec baselines above. This run
implements the 21 approved second-review patches. The implementation phase
left changes uncommitted; final workflow delivery creates the local commits
recorded below. No push, nested-submodule initialization, publication, workflow
dispatch or accepted-pin change occurs.

Platform v2 now binds its EventStore project endpoint to the runner plan's
`EventStoreHttp` port. Both source and package server selection require a project
(the package path uses the seed host), so an EventStore container branch is not
reachable in the supported graph. The run-isolation model regression checks the
port annotation and the unowned default; real listener and collision proof still
requires a compatible published Platform model in the hosted campaign.

The qualification job has a 90-minute wall-clock limit. Lifecycle/policy budgets,
startup/execution deadlines and hold expiry are unchanged. Collision and expiry
begin both owners before waiting for readiness. Hold-expiry is estimated to use
160–200 seconds of about 210 remaining seconds, giving roughly 10–50 seconds of
margin; this is not a hosted measurement. The first real campaign must measure
that margin and the two already-deferred cancellation timing/order questions.

Lineage checks now require each exact HXR036 reason and retain separate probe
artifacts. Every attached CLI exit, public outcome and diagnostic category is
verified during the case and finalizer; interrupted runs without observed exit
proof fail explicitly. Journal/observations writes use the atomic writer without
the retained-copy byte cap. Documentation, parser/service guards, candidate-policy
refusals, descriptor/hold identity checks, original failure cancellation and the
C#/Python fixture handshake have direct regression coverage.

The direct Parties command `python3 -B -m unittest discover -s tests/ci-lifecycle
-v` is blocked by the empty nested Builds checkout. The saved
`checks/review-pass-2-parties-direct-blocker.txt` contains its exact result.
`checks/review-pass-2-parties-loader.py` redirects only that unavailable helper
import to this workspace's root-declared Builds source. It does not replace any
helper behavior or initialize the nested checkout. Run it from Parties with
`python3 -B <absolute-evidence-path>/checks/review-pass-2-parties-loader.py`.
These controls grant local source verification only.

The first no-restore Builds build found stale net10 assets after current main's
net11 migration. A restore-enabled Debug build refreshed the existing package
graph without pin changes; final builds use the current net11 target. Final
check commands, outputs, exit codes and modified-byte hashes are recorded in the
`review-pass-2-*` evidence files below. Compatible Builds, Platform composition
and EventStore Testing releases and their deliberate adoption remain required
before hosted qualification; no Story 2.10 qualification is claimed.

The attached gate's supervisor durably records the actual CLI exit/time on its
original launch record. Recovery checks the already-journaled PID, birth and
process group before accepting that exit. Attached signals target only the
supervisor; it forwards once to the exact muxer/tool processes. A real gated
muxer/child regression checks exit 4 and SIGINT exit 130, exactly one received
signal, reconstruction of a separate controller and refusal of a rebound record.

Normal Platform source validation is blocked by current-main framework drift:
Memories targets net10 while source EventStore targets net11 (`NU1201`). The
saved temporary props wrapper imports each project's own build props and selects
net11 for local validation only. This builds the full test project without
changing tracked build configuration. The existing full isolation theory still
reaches a generated routing-reader child build with the original incompatible
framework graph, so that broader check remains explicitly blocked. The new
focused owned/unowned EventStore endpoint theory bypasses unrelated catalog
owner generation and exercises the actual model endpoint binding.

### Final second-review checks

| Local check | Result |
| --- | --- |
| Builds affected four classes | 176/176, no skips or unexecuted tests |
| EventStore descriptor reader | 62/62, no skips or unexecuted tests |
| Platform owned/unowned endpoint model | 2/2, no skips or unexecuted tests |
| Parties focused C# controls | 28/28, no skips or unexecuted tests |
| Parties controller controls | 180/180 |
| Parties declaration controls | 7/7 |
| Five Debug builds | Zero warnings/errors |
| Declaration renderer and actionlint | Exit 0 |
| Authoritative/mirrored v1 schemas | All five unchanged against owning HEAD |

These are 455 passing cases. The Platform build/test qualification applies only
to the explicit local framework override described above; its saved default and
broader isolation blockers remain open environmental limitations.

[Verification commands and results](checks/review-pass-2-verification.json)
record the exact commands, exit codes, framework/helper overrides and remaining
hosted questions. The four `review-pass-2-*-tests.xml` files retain individual
xUnit case names/results; the Python controller/declaration logs retain named
controls. [Modified input byte identities](checks/review-pass-2-modified-files.json)
identify every changed source/document file and its owning HEAD. The schema audit
compares both the original expected hashes and owning Git objects. No hosted or
package qualification is inferred from these local checks.

### Final independent-review corrections and local delivery

All three review layers completed and their 13 findings were triaged separately.
Seven reports were corrected in three groups; six were rejected after tracing
the actual typed publisher and persisted exit-observation paths. No new work was
deferred. The already-recorded hosted timing/order measurements remain open.

The final corrections preserve early SIGINT/SIGTERM until child identity binding,
make operational journals readable under their writer contract while retaining
artifact/link checks, and verify expiry against an actual blocked HTTP request.
The orchestrator read the patch diff and reran the complete local set:

| Final check | Result |
| --- | --- |
| Builds / EventStore / Platform / Parties C# | 176 / 62 / 2 / 28 passed |
| Controller / declaration | 181 / 7 passed |
| Total local cases | 456 passed, no selected xUnit skips or failures |
| Five Debug builds | Zero warnings/errors |
| Renderer / actionlint | Exit 0 |

[Final commands and results](checks/review-pass-2-final-verification.json) and
[final matrix/correction coverage](checks/review-pass-2-final-matrix-audit.json)
retain the passing named evidence. The documented helper/framework overrides and
standard/broader blockers still apply. No hosted or package qualification follows
from these local controls. Tested source bytes stayed unchanged during final
verification; the evidence narrative and status updates were finalized afterwards.

Local commits below passed each owning repository's pinned commitlint 21.2.4.
Platform records its source/evidence/status changes separately and preserves its
existing gitlinks; unpublished submodule commits are not recorded in those pointers.

| Repository | Local correction commit |
| --- | --- |
| builds | `565b9e63bce9d3b5b43483d01da81ef7ceb33a20` |
| eventstore | `8934b4f48c3035efd11ef6b89d8c8eba9de5fbd3` |
| parties | `9a08b88e3b7ecc8aa2bb8f707dacaebefd479b28` |

[Commitlint evidence](checks/review-pass-2-commitlint.json) retains every exact
validated message. Final modified-file hashes refer to the bytes at delivery;
`headSha256` identifies each file at its saved input HEAD before these commits.
