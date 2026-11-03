# Story 2.8 implementation and qualification evidence

Current continuation (2026-11-03): the user authorized remote package verification,
the dedicated registry reader procedure, reference caller PRs and hosted cleanup
qualification. The reader is applied and measured, with only its two pull-only
secrets delivered to EventStore and Parties. Builds v4.43.4 is published from
`26991e8c542704be44cd8d9f58084d56fa627adb`; signed package content, release inventory,
qualification payloads and embedded EventStore SDK bytes are verified. Platform main
`bf101b9d5f19ba741852b0230e526d1f01422022` already records the matching tuple and
released snapshots; redundant PR14 is closed unmerged. The reference v2 proposals were merged externally on 2026-11-03 at 14:51–14:52 UTC,
with their known native blockers still unresolved. Fresh coherent adoption and real
hosted runtime/lifecycle acceptance remain incomplete; no candidate acceptance is
claimed.

Current receipts are under [remote-resumption-20261103](release-phase/remote-resumption-20261103/):
[registry](release-phase/remote-resumption-20261103/registry-live-checkpoint.json),
[published content](release-phase/remote-resumption-20261103/builds-4434-checkpoint.json),
[independent content audit](release-phase/remote-resumption-20261103/parent-builds-4434-content-audit.json),
and [Platform supersession](release-phase/remote-resumption-20261103/parent-platform-pr14-superseded.json).
The active [implementation spec](../../spec-2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup.md)
records the next sequential work. The earlier local-only handoff below is retained
as historical provenance; its old versions and authorization limits describe that
attempt, not the current execution state.

## Current native qualification blocker

Parties [PR59](https://github.com/Hexalith/Hexalith.Parties/pull/59) at
`9c439ec5bbf2f7a79048ab797686cae6affa465f` completed all fifteen isolated projects:
6,291 executed tests passed, zero failed, and five optional tests were not executed.
The full UI and package-consumer gates passed. The first native invocation failed
before allocation with HXP020 because the published producer looked for the catalog
inside an uninitialized nested Platform checkout. Its verified classified artifact
records `not-allocated`; it supplies no real service teardown credit.

[Builds PR155](https://github.com/Hexalith/Hexalith.Builds/pull/155) merged normally as
`e07ac76e9b2a6c0ef34c02507bea054d25e48be5` after full exact-head CI and required
analysis checks passed. Its tree exactly matches reviewed head `8b99302b592acaa49937bf3f714317378c5c1d09`. The focused 83
cases pass; the parent independently reran the eight new real-Git cases. Its fresh
[main CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/41657058349)
was cancelled after an external main advance to `090797e333748b1a62db0a857d8d0f0e40b524fe`.
That incoming merge retains the runtime fix but duplicates the already approved
KeyVault package row. The unchanged central validator rejects the exact committed
catalog. A separate minimal correction takes priority before publication; the
cancelled run supplies no passing credit.
The normal prerequisite publication completed as v4.43.5 from
`89f62052c932d054bda2b435bdfef60d96e77612`, preserving the Azure catalog additions.
That version retains HXP020 and is not adopted as the corrected native producer.
A fresh qualified publication and coherent Platform/reference adoption remain.

The dependent lifecycle fixture/controller is being prepared separately. Actual
service success/failure, startup/execution cancellation, partial startup, active
attachment withdrawal, exact cleanup retry, peer preservation and verified zero
owned leftovers remain incomplete. No candidate acceptance is claimed.

Current detailed receipts include [isolated execution](release-phase/remote-resumption-20261103/parent-parties-9c-isolated-project-audit.json),
[exact native refusal](release-phase/remote-resumption-20261103/parent-parties-9c-native-first-refusal.json),
[producer correction](release-phase/remote-resumption-20261103/builds-direct-catalog-codacy-checkpoint.json),
and [immutable publication](release-phase/remote-resumption-20261103/builds-4435-observed-publication.json).

## Historical local implementation handoff

Status: local implementation and controls; hosted qualification is incomplete.
No package/image publication, remote CI dispatch, accepted-pin change or candidate
acceptance is authorized or claimed by this work.

The runner now permits compatible active same-job CI attachments, publishes an
external owner receipt before allocation, withdraws and reports active holds before
teardown, preserves first outcomes/expiry, rejects unknown cleanup discovery or
owned leftovers, and suppresses CI failure retention. The workflow requires declared
package suites and successful isolated tests before provisioning, uses the exact
pinned tool and verified Release/package preparation, and has both a helper `finally`
and an independent bounded `always()` exact-owner finalizer.

The Platform changes make the existing native preparation and catalog participant
paths available in package builds. Missing catalog reader/package/image inputs still
refuse before acceptance; released owner-snapshot provenance checks remain in force.
EventStore's descriptor SDK owns the shared exact-stream persistence observer now
used by Parties. EventStore's new suite checks the real gateway's readiness and
anonymous rejection plus actual isolated Redis end-state. Parties retains its full
real command/query/event-stream end-state suite. The EventStore infrastructure suite
alone does not establish domain command-to-event behavior or candidate acceptance.

## Local verification provenance

`focused-provenance.json` records exact commands, source SHA-256 values and the
loaded Debug assembly/dependency hashes. The fallback recompiles the changed
Tooling, CLI and Module.Tests assemblies and may reuse other fixture/dependency
binaries. It is local source diagnostics; it is neither a Release package test nor
hosted evidence. Individual `.txt` results and build output preserve successful
checks and broad blockers separately. No `.log` evidence is relied on for tracking.

The broad Module.Tests graph initially failed without mapping on missing
Commons/FrontComposer fixture references. With the existing verified source mapping
it failed `HXW005`: mapped FrontComposer source duplicates restored
`Hexalith.FrontComposer.Contracts/4.7.1`. This gate was not weakened. The focused
no-reference-rebuild lane is documented separately.

The first Platform no-reference-rebuild attempt exposed reused Tenants assembly
version drift. The subsequent full mapped graph resolves it and all 13 focused
Platform controls pass; their actual source/binary hashes are retained separately
in `platform-focused-provenance.json`.
A reference suite run without its descriptor is a required negative control and
fails with zero skips; it is never counted as an integration pass.

## Recorded local results

- Builds: 238/238 serialized cases pass (CI lifecycle 14, admission 130, engine 43,
  runner review regressions 51); CLI/Tooling and focused test builds have zero warnings/errors.
- Platform: the full mapped Debug graph builds with zero warnings/errors; native
  preparation 8/8 and package/catalog prerequisites 5/5 pass.
- EventStore: mapped SDK/reference/control builds pass with zero warnings/errors;
  descriptor controls 15/15 pass. Its absent-descriptor reference control fails
  2/2 tests with zero skips, as required.
- Parties: mapped reference build passes with zero warnings/errors, declaration
  controls 7/7 and isolated observation controls 4/4 pass. Its absent-descriptor
  native control fails 1/1 with zero skips, as required.
- Python integration helper 15/15, preparation helper 9/9, required-CI-status
  control and 223 executable/static workflow assertions pass.
- The package reference suite against current published EventStore `3.128.4`
  refuses at compile time on missing descriptor APIs; this is a publication
  prerequisite, not a verified package integration result.

`verification.json` separates positive checks, expected negative controls and the
remaining broad/publication/hosted gates. Initial failed focused results are retained
as `initial-*` and superseded by the final `.txt` results and focused provenance.

## Audit correction controls

The helper now forwards cancellation that arrives inside `Popen` immediately after
child assignment; repeated handling does not send duplicate interrupts. The 15 helper
controls include the exact constructor-time race and an actual helper subprocess
using a temporary fake `dotnet` executable. SIGTERM reaches the helper, SIGINT reaches
the child, and the real receipt-based finalizer executes exact down with cancelled
job status. No child/finalizer mock is used by that process control.

Five additional CI lifecycle cases execute `ModuleCommandExecutionService` for
success, failure, cancellation and partial-startup failure/cancellation. A live owned
AppHost process reads the committed cleanup request, withdrawal report and first
terminal record at its first stop signal. Tests assert original expiry, immutable
first outcome/time, no CI failure retention, complete cleanup and no hold waiting.
The partial-startup callback calls the real engine `FailAndTearDownAsync` path.

These are local source-mode fixtures with simulated CI identity and real native/
AppHost processes; composition observations are offline. They do not authenticate
package artifacts or qualify an Aspire/Dapr/Docker topology or hosted lifecycle.
`pre-audit-*` receipts/results retain the earlier 233-case binary provenance; the
current focused receipt and `.txt` results identify the rebuilt 238-case binary.
Hosted publication/qualification remains pending and the frozen intent is unchanged.

## Concrete publication inputs and dependency order

Existing source checkouts are identified in `release-inputs.json`, together with
changed-file hashes. They are uncommitted implementation inputs, not publishable
exact-source authority. Owners must first choose reviewed committed revisions and
release versions through their existing release decisions. No future version or
release SHA is invented here.

| Order | Owner and required source/package inputs | Existing release/check entry points |
| --- | --- | --- |
| 1 | EventStore descriptor APIs in `src/Hexalith.EventStore.Testing` and `src/Hexalith.EventStore.Testing.Integration`, including `AspireTopologyFixtureBase`, private descriptor/endpoint readers and `TestEnvironmentStreamReader`; compatible Contracts/Client/Server/ServiceDefaults/Aspire catalog/bootstrap APIs | `references/Hexalith.EventStore/.github/workflows/release.yml`; `tools/release-packages.json`; `scripts/pack-release-packages.py`; `scripts/validate-nuget-packages.py`; `scripts/validate-consumer-package-references.py` |
| 2 | Tenants Contracts/Server catalog and bootstrap delegation APIs plus the declared domain/API images; Memories Contracts/EventStore/EventStoreExtensions catalog and tenant APIs plus declared server/domain/clock/telemetry images, all compatible with step 1 | Each owner's existing `.github/workflows/release.yml`, package declarations and container provenance gates; exact main source and protected publication remain mandatory |
| 3 | Receipt-capable Builds CLI/Tooling and packaged AppHost/EventStoreHost ABI, runner helper and reusable workflow, bound to approved compatible owner-package inputs from steps 1–2 | `references/Hexalith.Builds/.github/workflows/build-release.yml`; `Tools/build-g4-tool-packages.ps1`; `Tools/test-g4-tool-package-contracts.ps1`; `Tools/publish-g4-tool-packages.ps1` |
| 4 | Platform Composition/native preparation/catalog reader changes, together with exact released EventStore/Tenants/Memories declaration snapshots, commit/hash provenance and image identities | Platform's existing declaration provenance and package composition checks; receipt-capable tool adoption/qualification decisions remain separate |
| 5 | EventStore/Parties reference enrollment, test projects and solution entries; Parties published Contracts compatible with its `CreatePartyComposite`/query/event suite; exact new Builds reusable-workflow SHA and local tool version | E/P existing `ci.yml`; E/P explicit release callers after exact-source CI; `scripts/render-platform-declaration.py --check` and strict renderer tests in Parties |
| 6 | Disposable hosted reference CI plus deliberate cancellation/partial-startup lifecycle qualification | Required observations below; no remote execution occurred in this implementation |

Prerequisite release source and caller activation must be reviewed as separate
commit groups. EventStore step 1 consists of SDK/helper and SDK-control changes;
its new native test project/solution/manifest/tool/CI enrollment belongs to step 5.
This prevents the prerequisite release from depending on the receipt-capable Builds
release it enables. Use the existing green-source release gates for each
prerequisite group. Do not solve the ordering by bypassing integration or weakening
publication/provenance checks.

At step 3 an owner-approved catalog update may be necessary before the new Builds
tool can be packaged against the released APIs. At step 4 replace snapshots only
with exact bytes from the approved owner releases and their recorded commit/digest.
At step 5 update accepted reference pins only after those releases and independent
adoption decisions exist. These are explicit future decisions; this change makes
none of them.

Current accepted values remain: EventStore `3.128.4`, Tenants `5.11.2`, Memories
`3.0.10`, Parties `2.0.0` in Builds' central family catalog; Parties' tool pin
`4.39.0` and reusable-workflow pin
`1745ea0f09df9e518aeea3eeb1a7df836f67e09d` remain unchanged. EventStore newly
enrolls the already existing `4.39.0` tool coordinate; it does not assert that this
old release supports the new receipt option. EventStore's existing workflow ref is
still `main`. The staged new inputs cannot run against an older reusable workflow;
they intentionally grant no acceptance before activation against an approved new
workflow/tool release.

Platform's existing snapshot provenance remains EventStore `3.120.0`, Tenants
`5.8.0` and Memories `2.27.1`. Those snapshots are incompatible with the current
family catalog and remain refused. Source implementation is not permission to
relabel them or select unreleased package versions.

Before changing any accepted pin, require all of:

1. Owner-selected exact committed release source with successful exact-source CI
   and existing protected publication approval; package and image content identities
   must trace to that revision's Release outputs.
2. The declared release manifest's complete family packages and downstream consumer
   checks, including exported descriptor/persistence APIs and catalog/bootstrap
   compatibility. Keep Testing and Testing.Integration on the EventStore family pin.
3. Published tool archive/AppHost ABI qualification and exact installed tool version
   verification; no global tool, floating restore or legacy execution fallback.
4. Exact declaration bytes/commit/SHA-256 and released image provenance matching the
   selected catalog family. Retained registry access must be pull-only; no staging or
   production provider credential enters integration jobs.
5. Separate Builds adoption and Platform acceptance/integration qualification
   references for the exact tool/policy. A compatible numeric range alone is
   insufficient, and baseline-only results cannot be promoted into candidate evidence.

## Hosted evidence still required for completion

Save a revision-bound record for both reference repositories that identifies source
SHA, actual reusable-workflow SHA, tool version/archive hash, all loaded package/image
identities, GitHub repository/run/attempt/job and disposable Linux runner kind.
Record isolated completion before any service allocation and nonempty complete
required package suites. Then exercise success, failing tests, cancellation during
startup, cancellation during execution and interrupted partial startup.

For each terminal path retain the durable preallocation receipt, first outcome/time,
attachment acceptance/absolute expiry, same-job positive and cross-job negative
admission, admission closure, withdrawal document/report recorded before resource
stops, exact down attempts and final verified zero owned container/process/workspace/
claim IDs. Include a failed or unknown-discovery cleanup followed by successful exact
retry and an unaffected peer-owner control. Cancellation after a recorded failure
must preserve that failure while reporting `ci-job-cancelled` withdrawal. Retain only
classified redacted documents, outside deleted workspaces.

No hosted observation in this directory currently supplies those qualifications.
Story completion therefore remains pending approved publication, reference activation
and hosted cancellation/partial-startup evidence.
