---
title: '2.10 Qualify the runner lifecycle on the reference composition'
type: 'feature'
created: '2026-11-08'
status: 'in-progress'
baseline_commit: '13b4f99c75978ffdf6bf9de515729aa8e7ed4055'
story_key: '2-10-qualify-the-runner-lifecycle-on-the-reference-composition'
course_correction: '../planning-artifacts/sprint-change-proposal-2026-11-08.md'
context:
  - '{project-root}/docs/contracts/candidate-and-attachment-policy-v1.md'
  - '{project-root}/docs/contracts/test-environment-v1.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.Builds/AGENTS.md'
---

# Story 2.10 execution spec

## Intent and boundaries

Qualify the complete runner lifecycle on the Parties reference composition. Preserve the original Story 2.10 acceptance criteria in [epics.md](../planning-artifacts/epics.md#story-210-qualify-the-runner-lifecycle-on-the-reference-composition). This spec records the work transferred out of Story 2.8 by the [approved course correction](../planning-artifacts/sprint-change-proposal-2026-11-08.md).

Story 2.8 delivers CI execution/finalization and focused proof of its original six edge-case rows. Story 2.9's fixture migration is already recorded done. Neither story's completion establishes full SM-3 or final accepted-tool qualification. Keep prior results under their actual source/run/decision identities; no retrospective relabelling or cross-decision reuse is permitted by policy v1.

## Execution constraints for this build

On 2026-11-10 the user answered "yes" to the explicit request to initialize
Parties' nested Builds dependency and run the required local and hosted
qualification. This authorizes that nested initialization, local services,
remote qualification operations and the preparation needed for those runs,
overriding the restrictions recorded below for this story. Preserve unrelated
workspace changes and sequential execution. Do not infer a lifecycle pass,
tool acceptance or pin approval from this authorization; applicable gates must
still pass against one exact compatible input set.

The resumed bmad-build invocation on 2026-11-09 uses the current Step 03 rules:
no pushes, no remote operations, and sequential execution only. Continue the
remaining resource-free implementation and owning checks within those limits.
The earlier publication authorization below records a completed follow-up; it
does not authorize remote qualification in this resumed workflow.

The initial bmad-build implementation step permitted no pushes or remote operations and required sequential execution. Initial implementation and verification were resource-free. The user subsequently instructed "push all changes", authorizing commits, remote synchronization and pushes of the prepared work, including the pre-existing ES416 contract proposal. Required live qualification and acceptance gates remain open; no hosted qualification workflow was dispatched and no local services were started.

## Tasks and acceptance

- [x] Inventory the reference composition's existing lifecycle evidence once. Classify each row as passed for its original recorded subject, failed, incomplete or unexecuted. Record exact source, suite/configuration, package/image, policy and run identities. Do not reset completed implementation or hide earlier failures.
- [ ] Close the complete local/reference scenario table below. Real local service execution remains subject to the existing restriction; resource-free checks may run locally, and service scenarios run in the authorized disposable hosted environment unless the user explicitly changes that restriction. An unexecuted required local row remains open.
- [x] Finish the broad manual `matrix` campaign, including both metadata-lock refusal stages, exact retry, peer preservation and partial startup. Use the genuine current tool and fixed verified caller inputs. The three failed historical runs remain failed: `42400061797`, `42408333976`, `42418257508`.
- [x] Account for real startup/execution cancellation proofs from 2.8 under their own identities. A new acceptance decision or changed relevant input requires fresh qualifying evidence; historical diagnostic observations cannot supply a new candidate pass.
- [ ] Record actual startup duration, effective deadline/override, immutable first outcome, attachment expiry/withdrawal, bounded redacted diagnostics and exact owned leftovers for every required scenario. Required missing/skipped cases do not pass.
- [ ] Record the qualified tool version as Platform-accepted and pin it in Platform and Parties only after every applicable qualification gate passes. A published version or a completed document alone supplies no acceptance.

| Required scenario | Required proof |
| --- | --- |
| Success and failure | Correct first outcome; local success cleanup and failure retention; CI cleanup in both cases |
| Startup timeout and partial startup | Original finite deadline; exact partial ownership recovery; diagnostics and correct local/CI disposition |
| Cancellation and collision | Real cancellation, exact ownership and explicit conflict; unrelated resources preserved |
| Owner finish before attachment end | Outcome recorded immediately; local cleanup waits for live holds; completed CI owner refuses new admission |
| Owner cancellation before attachment end | Active attachments withdrawn and work/access ended before owner teardown |
| Attached failure and hold expiry | First failure/local retention preserved; absolute expiry unchanged; expired work withdrawn |
| Failure followed by cancellation | Original failure and applicable local retention remain immutable |
| Metadata locks, cleanup retry and peer | Both real refusal stages observed; bounded refusal and exact retry; peer resources survive |

## Bounded optimization work

These are implementation tasks, not capabilities supplied by the planning correction. They do not block a valid qualification run when the existing implementation can complete it correctly.

- [x] In the owning Parties/Builds controller, add fixed safe failure codes/checkpoints and bounded redacted diagnostic fields. Cover the actual missing-cache, reader-directory and process-acknowledgement regressions with focused tests. Never publish arbitrary exception messages, raw startup output, descriptors or credentials.
- [x] Add an explicit focused CI qualification selection for 2.8 if the current full `matrix` controller is its only hosted entry point. Preserve the full matrix for this story. Report selected, required and completed cases; a focused pass must never appear as a full-matrix pass.
- [ ] Where setup dominates, reuse verified downloads/package inputs keyed by exact relevant toolchain/catalog/package identities. Revalidate consumed content and restore fresh job-local state. Keep isolated tests before provisioning and all fresh acceptance runs required by policy v1. Sharing an old green baseline as a new decision's test result is excluded.
- [x] In Platform CI, separate current/legacy source resolution from execution so independent lanes can run concurrently. Preserve the actual `tuple` and `legacy-public-cohort` required check contexts, all original tests, real legacy source identity and an overall failure when either lane fails or is missing. The five-SDK matrix is already parallel.

Use a focused test first for each changed mechanism, then the owning repository's required checks. Run full qualification at the final fixed input set; rerun affected evidence when relevant inputs or the acceptance decision change. Retain separate actual workflow runs for cancellation and the ready-artifact-before-cancel protocol.

### Current sequential handoff: local harness preparation

The parent has dispatched the fresh hosted `matrix` as Parties workflow run
`42727219072`, from committed source
`c9eb1fe37373b2b25d3759a4ac8f33ac46754e17` on dedicated ref
`qualify/story-2-10-20261110-c9eb1fe3`. It selects the genuine published tool
`4.47.0` and exact Builds catalog/helper
`5ec1779040606be89306129a7d2b245258c9acd2`. The independent fixed local Parties
checkout is `/tmp/hexalith-story-2-10-20261110/parties` at that same revision,
with only its direct Builds and Platform dependencies initialized at their
recorded pins. No local lifecycle services have been started.

Prepare `eng/lifecycle-qualification/local.py` and focused control tests as the
next implementation unit, then return to the parent before running services or
dispatching another workflow. The harness must use the actual published CLI,
the committed Parties `lifecycle-package` fixture and real local context, with
no fabricated GitHub metadata, fixture-only pass or source tool substitution.
Use private run-local data/state, durable actual issued owner identities,
bounded redacted result records, exact PID/birth/ownership checks for signals,
the original finite startup deadline and exact-owned cleanup in every exit.
Cover the scenario table wherever the published runner exposes the required
capability. Record absent producers or attached-work/access enforcement as
required incomplete proof; never infer them from a hold metadata transition.
Keep real peer preservation, both metadata lock refusals and exact retry.
Use the genuine existing private suite control for pass/fail/wait, preserving
suite and configuration content identities. Focused resource-free tests should
check consequential ownership, immutable timing and redaction boundaries.
Do not start a second service campaign while the hosted one is active. The
parent will prepare the pinned runtime, build the suite and run the live harness
after this handoff. Earlier evidence and the original baseline remain immutable.

## Handoff and completion

Owner: Platform/Builds developer, with Parties owner changes made in Parties. The product owner tracks the scenario table; runtime implementation follows the normal development workflow. No agent or remote campaign was launched by this course correction.

This story is ready for development, not implemented or qualified. Its evidence ledger and final acceptance must remain finite and explicit. Preserve existing budgets and credentials policy; a timeout increase is not a performance improvement. Measure setup, build, test, startup, cleanup and audit time separately before reporting savings.

## Resource-free implementation record — 2026-11-09

The [finite evidence inventory and handoff](evidence/2-10/README.md) records 20
original lifecycle observations: 8 passed for their original recorded subject,
11 failed and 1 incomplete. Selected retained receipts match their original
SHA-256 manifests. Historical passes keep their own subjects; all eight fresh
local and eight fresh CI scenario rows remain unexecuted. No new acceptance
request or final committed qualification input set has been selected.

Parties now has `focused-ci` campaign selection with explicit required, selected
and completed case reporting; it never supplies a full-matrix pass. Fixed bounded
failure metadata covers the missing Aspire cache and launch acknowledgement,
while existing reader-directory regression controls remain. Real lock stages
require their exact HXR037/HXR023 refusal. The manual workflow runs controller
controls before provisioning and retains distinct startup/execution cancellation
runs and their ready-artifact protocol.

The bounded-diagnostics task remains incomplete: the launch-acknowledgement
control does not cover the actual Builds stdin process-acknowledgement
regression, and its owning regression tests have not run in this build.

Platform resolves committed sources before independent current and real-legacy
execution. Both original execution check contexts, original tests and the five
SDK lanes remain. An always-run aggregate verdict and the automatic source
release guard require resolution and both lanes to succeed.

[Verification](evidence/2-10/resource-free-20261109/verification.json): 24 Platform
source-release/workflow tests, 10 legacy-source tests and both workflow linters
passed. Parties fixture-assisted controls passed 83/83. Canonical Parties test
discovery remains blocked: six module import errors from its uninitialized
nested Builds dependency, with four workflow-only tests passing. The temporary
harness records the exact Git source and content identities of its two extracted
Builds files; it supplies neither canonical-owner-check nor hosted credit.

Verified setup-cache reuse is deferred. Canonical Parties predates the already
recorded 2.8 signal/digest fixes; reconcile the prepared changes with that owner
branch before any hosted run. The final genuine Platform legacy/current source
cut, owning checks, full fresh manual campaign, patched-helper lifecycle rerun,
complete local/reference table and Platform-accepted tool enrollment/pins remain
open. No services, remote operations, publication, pins or budget changes were
performed. Status remains `in-progress`; Step 03 cannot advance while these
required acceptance tasks remain incomplete.

## Publication follow-up — 2026-11-09

The subsequent "push all changes" instruction required synchronizing Parties
with current remote main. Rebase resolution preserved its existing Story 2.8
signal, digest and reader controls alongside the new focused selection. The
combined controller passed 106 fixture-assisted tests with the exact current
Builds helper; workflow lint passed. Canonical discovery remains unsuccessful
without the nested Builds dependency: 75 tests ran with 27 errors and one failure.
The [separate integration record](evidence/2-10/push-integration-20261109/verification.json)
retains those results and the exact pushed Parties source. Earlier evidence
keeps its original input hashes. Full hosted qualification, the final compatible
Platform source pair, actual Builds stdin-regression verification and accepted-tool
enrollment remain open. Publishing prepared source does not complete this story.

## Resumed implementation and owning checks — 2026-11-09

The [resumed resource-free record](evidence/2-10/resource-free-resumed-20261109/README.md)
now covers the actual Builds stdin process-acknowledgement regression. Its private
controller emits fixed `HXLQ109` timeout and `HXLQ110` invalid/unreadable-record
codes, the fixed stdin checkpoint and bounded redacted observations. Private
paths, tokens, payloads and native exception messages/inner exceptions are excluded.
The original ten-second acknowledgement bound is unchanged. The developer guide
records this diagnostic-only scope.

The final owning Debug build passed without warnings/errors after repairing stale
assets solely from the existing local package cache. All 21 canonical focused
stdin controls passed with zero skips, including the actual pipe-disposal and
framed-output handshake and new exclusive-lock/redaction cases. The full owning
Module.Tests attempt remains unsuccessful: it was cancelled at a 600-second
diagnostic cap, exit 58, after the pinned-model assertion observed seven server
resources instead of six. A separate reflection-control failure was reported
after cancellation. Two skipped controls and cancelled assembly cleanup remain
recorded; the cancelled runner's zero total supplies no completed/pass count.

Canonical Parties discovery on its newly observed source ran 75 tests with 27
errors and one linked-owner-directory failure. Its missing nested Builds pin is
available locally, but the owner's explicit prohibition of nested initialization
without a user request still applies. No nested dependency was initialized.
The separate fixture-assisted controller check passed 106/106 using that source's
actual Builds pin `5ec1779040606be89306129a7d2b245258c9acd2`; it supplies no canonical
or hosted credit and does not hide the failed discovery.

An unrelated workspace operation committed this implementation's C# edits and
developer-guide paragraph while verification was in progress. The manifest retains
the original build base, exact edited-file and binary hashes, and separately
observed full revisions. The C# source matches the observed external commits and
the suite DLL/PDB remain unchanged since the focused pass. These moving sources
have not been selected as a final compatible qualification input set.

The bounded-diagnostics implementation and focused regression coverage are now
present; the task remains open because the required owning checks are not all
successful. All eight fresh local and eight fresh CI scenario rows, the full
manual campaign, patched-helper lifecycle rerun and accepted-tool enrollment/pins
remain open. Verified setup reuse remains deferred. No services, remote operation,
workflow dispatch, commit, acceptance, pin or production-budget change was
performed by this implementation. Status remains `in-progress`; Step 03 cannot
advance on these results.

## Authorized live continuation — 2026-11-10

The [live campaign record](evidence/2-10/README.md#authorized-live-campaign--2026-11-10)
records the user's explicit authorization and the actual new subjects. Canonical
Parties discovery passes 106/106 after its exact nested Builds initialization.
The isolated Builds owner project passes 2,939 tests, with zero failures/errors
and two explicit conditional skips; those checks retain their exact source and
binary identities and supply no published-tool runtime credit by themselves.

The genuine signed 4.47.0 tool and catalog/helper
`5ec1779040606be89306129a7d2b245258c9acd2` are exercised by committed Parties
caller `c9eb1fe37373b2b25d3759a4ac8f33ac46754e17`. Fresh hosted matrix run
`42727219072` passes its baseline, all four original matrix cases and exact
finalization of five owners, including both lock refusals and peer preservation.
The broad manual matrix task is complete for this recorded subject.

The new [local controller](../../eng/lifecycle-qualification/local.py) has 33
passing focused controls. The [local observation index](evidence/2-10/campaign-20261110/local-observation-index.json)
records four passed and four incomplete scenario groups under their actual
controller/source hashes. Earlier verifier failures remain separate; fresh
affected probes verify public expiry projection and real peer preservation.
No inspected owned container/workspace/active-state leftovers remain across
17 local owners, and all ten unrelated containers retain identity/status.

Qualification remains incomplete: supervised attached-suite execution and its
work/access producers belong to the explicitly split follow-up; the fixed caller
does not expose a genuine exclusive-resource collision selector, and interrupted
startup lacks reported actual duration. Hosted result tool/termination bindings
remain unavailable. Fresh GitHub startup cancellation `42737966490` and execution
cancellation `42740407192` each passed its fresh baseline, published and verified
its ready artifact, received one audited parent cancellation request and verified
independent finalization plus exact retry. The first snapshot was actually
`Starting`; the second had live native execution and an active hold, withdrawn as
`ci-job-cancelled` before its unchanged expiry. These cancelled workflow results
retain their own source/run/decision subjects and supply no passed integration
or missing producer proof. No candidate or tool is accepted, no accepted pin changes,
and no previous result is relabelled. Keep status `in-progress` and the remaining
acceptance tasks open; these diagnostic subjects do not supply a final fixed-set
qualification or a new decision's evidence.

## Authorized canonical owning checks — 2026-11-10

After the explicit user authorization above, only Parties' exact nested Builds
dependency `5ec1779040606be89306129a7d2b245258c9acd2` was initialized, using local
Git objects with no fetch or recursion. Canonical Parties discovery now passes
106/106, including the linked-owner-directory control. No Parties source or pin
change was needed; its earlier discovery failures remain recorded.

The Builds model assertion reproduced its stale expectation of six servers while
the selected declarations define seven. Its one-file correction compares exact
resource names, app IDs and readiness endpoints with those declarations, retaining
the existing gateway and real HTTP readiness checks. The earlier Docker discovery
`TargetException`, reported after cancellation, did not reproduce normally; no
speculative production change was made for it.

The [fresh owning-check evidence](evidence/2-10/owning-checks-20261110/README.md)
uses clean detached source `da4e896b164f35c0b830a32b975203bb922280f9` at
`artifacts/story-2-10-builds-da4e896b`, isolating verification from concurrent
external changes to main. The cache-only restore and Debug build passed without
warnings/errors. Correction/Docker controls passed 2/2, and actual stdin
regression classes passed 21/21, both without skips. The complete Module.Tests
owner project finished in 531.984 seconds within its existing 1,800-second budget:
2,941 total, 2,939 passed, zero failed/errors, two explicit conditional skips and
zero not-run. The catalog SDK interception and Windows/macOS native inspection
skip identities remain explicit and supply no qualification proof.

The manifest binds exact mapped source, declaration, package/catalog and binary
identities; every bound input remained unchanged through execution and the fixed
source remained clean. Earlier failed, incomplete and moving-source attempts keep
their original outcomes. This completes the bounded-diagnostics task and its
owning checks. This worker ran no local lifecycle campaign or hosted workflow;
all 16 fresh scenario rows, final compatible qualification input selection, full
matrix/cancellation campaigns and accepted-tool enrollment/pins remain open.
Status remains `in-progress` pending those gates.

## Blocked on Story 2.11 — 2026-11-10

Resuming `/bmad-build 2.10` found no qualification work that can be completed
with the published 4.47.0 tool. The [campaign ledger](evidence/2-10/campaign-20261110/README.md)
records 7 passed, 7 incomplete and 2 unexecuted local/CI rows. The missing proof
is producer capability, not skipped execution:

- supervised attached-suite execution, with per-attachment work/access ownership
  stopped at hold expiry or withdrawal (deferred follow-up
  `attached-test-execution-and-expiry-enforcement`);
- actual startup duration for interrupted or partial startup;
- executing-tool and termination bindings in local and hosted result records;
- a genuine exclusive-resource collision selector in the Parties caller, and
  hosted timeout, failure-then-cancellation and attached cases.

Policy v1 requires full qualification against one fixed compatible input set.
Any run with 4.47.0 would therefore be repeated once a new tool carries these
producers. The user chose to block this story and open
[Story 2.11](../planning-artifacts/epics.md#story-211-supervise-attached-runs-and-report-the-missing-lifecycle-producers)
for them. Status remains `in-progress`, and every open task above stays open.

Resume this story only after a Builds release carries Story 2.11. Then select
that tool, the Parties caller and the final Platform source cut as one fixed
input set. Run the complete local and hosted table and both real cancellation
runs. Record the Platform-accepted tool and its Platform and Parties pins only
after every gate passes. The 4.47.0 observations keep their own subjects and
supply no acceptance for the new tool. No code, services, remote operations,
pins or budgets changed in this resumption.

## Fixed-input preparation after Story 2.11 — 2026-11-13

The [preparation audit](evidence/2-10/preparation-20261113/README.md) confirms that
the Builds-release resume prerequisite is now met: published 5.1.0 contains the
Story 2.11 runtime producers. This is separate from the still-open compatible
Platform source/publication gate. Latest Platform source release v1.11.3 and the
current Parties Platform gitlink both predate the required live EventStore port
binding. The actual latest publisher refused its unsuccessful tuple run; that
run's Builds gitlink/publication binding disagreed, and both historical lanes
refused changed production/control/helper sources. Preserve these strict guards
and prepare an honest fresh committed source cut before publication or live
qualification.

`eng/lifecycle-qualification/prepare.py` now validates explicit exact input
selections and supports content-verified download caching keyed by source,
catalog, tool release/framework, toolchain and archive identities. Every hit and
restore revalidates content; restores use fresh private copies, never prior test
results, credentials or run state. Poisoning, links, stale/extra state and
existing or concurrently created destinations fail. The current preparation
selection matches the caller's 5.0.0 pin and its exact catalog; its source audit
correctly returns `platform-live-port-binding-unavailable`. Actual signed NuGet
package population, revalidated reuse and two fresh restores pass. Separate
operation timings supply no lifecycle or performance-savings claim.

All 45 Platform lifecycle controls (33 original plus 12 preparation) and 183
canonical Parties controls pass without failures/errors/skips. Only the exact
recorded nested Builds dependency was initialized, using local objects,
`--no-fetch` and no recursion under the existing user authorization. The earlier
151-test canonical attempt with 74 errors and one failure remains recorded.
Bound source hashes stayed unchanged during the successful checks.

The historical `local.py` remains unchanged and fixed to 4.47.0. A current local
controller, compatible committed/published Platform source and caller input set,
fresh complete local/hosted scenarios and real cancellation proofs still remain.
Setup reuse is implemented as a preparation utility but is not yet integrated
into a final fixed-set campaign, so its parent task remains open. No service,
workflow dispatch, commit/push, publication, accepted pin or budget changed.
Keep status `in-progress` and all remaining acceptance tasks open; this unit
does not permit Step 03 to advance.

## Source prerequisite continuation — 2026-11-13

The current user request narrows this continuation to preparing and publishing a
compatible Platform source release containing
`3f7659e2ff660212bc4f31c6e118a14a65f55da5`, then preparing the matching Parties
qualification caller and fixed inputs. Full lifecycle campaigns and accepted-tool
enrollment remain outside this continuation; preserve their open tasks and all
historical evidence identities. The user explicitly authorizes required commits,
pushes and guarded publication after applicable gates pass, overriding the older
no-remote restriction for this source prerequisite work.

The isolated working copy is `/tmp/hexalith-story-2-10-source-release-20261113`,
initially based on `d5928c8b106e1da4179e55dbe8390912b02a7e44` and synchronized
with main `8483559f068f8cd7282d878dcdc75ac10870c784`, then incorporated
`8cf40085848ab6b65832dbee77af696be93c792f` before the final source
freeze. It carries exact copies of
the supplied preparation work; the original workspace and its unrelated
EventStore checkout must remain unchanged. Retain exact tool pins: Platform
5.0.1 and Parties main 5.0.0. A later dedicated qualification branch may select
5.1.0 without merging accepted pins into main.

The current user instruction authorizes reconciliation of the tuple's compatibility
metadata. README.md explicitly separates matching the version range from tool
acceptance. Align `hexalith-platform.json`'s compatibility range floor to the
already configured 5.0.1 while keeping both exact tool manifests unchanged. A
clarification was raised but is not needed after this distinction was established
from repository guidance; no response or new approval is claimed. Preserve the
strict `validate_owner_pins` guard and all lifecycle acceptance gates.

The implementation worker prepares the source changes and owning checks, then
returns before commits, pushes, publication, dispatch or lifecycle services. The
parent owns exact committed-source cohort generation and guarded publication.

- `eng/builds-tuple.json`: prepare the exact official 5.0.1 release binding from
  `/tmp/hexalith-story-2-10-release-record-20261113/proposed-builds-tuple.json`.
  This binds Builds `3c2f5944aaa28dc7854a00a8a9df171aa810b0c7`; align the
  isolated direct Builds checkout and proposed gitlink with that source.
- `eng/current-tuple-source.json`: regenerate from staged Git objects using
  `eng/prepare_tuple_sources.py binding`; preserve package provenance and
  actual owner release tags. Its four current owner gitlinks are authoritative.
- `eng/legacy-tuple-source.json`, `eng/pre-extension-tuple-source.json`:
  investigate exact stale differences and document the necessary fresh legacy,
  pre-extension and package-reader companions. Do not create commits or change
  frozen anchors, original 349/412 profiles, historical tools or source guards.
- Run the relevant owning source controls and verify the proposed catalog and
  official publication binding. Retain new logs outside committed sources in
  `/tmp/hexalith-story-2-10-release-record-20261113`. The parent already has
  successful initial source controls, publisher controls and 45 lifecycle controls;
  do not broaden those without a change or concern.

The parent will freeze the final source and generate
all three immutable historical companions using the existing preparation helper.
The existing automatic publisher must require successful exact-source push/main
runs for all three tuple lanes and all six catalog SDK lanes, preserving every
before-tag/before-publish recheck. Publication must never be inferred from a
prepared hash or local pass. Only after actual publication may the parent select
the matching Parties caller and 5.1.0 fixed inputs. All acceptance flags remain
false until full lifecycle qualification.

## Compatible 5.1.0 validation transport — 2026-11-13

This is the current implementation unit, superseding the completed preparation
handoff above. The user explicitly confirmed compatibility floor 5.0.1 with both
exact main tool pins unchanged. The synchronized source base is
`1cece242bd4a82c517bb92447c6d6799f6313b6f`. Its 5.0.1 tuple and refreshed
companions are honest, but it cannot run the required genuine 5.1.0 lifecycle
owner: the published AppHost projects the claim directory during validation,
and Platform rejects the unrecognized 5.1.0 binary pair with HXP020.

The implementation worker must:

- Add exactly the measured 5.1.0 complete AppHost/Tooling pair in
  `src/Hexalith.Platform.Composition/PlatformPartiesValidationContext.cs`.
  AppHost SHA256 is
  `c5d2804f71532125f5f53dfc1ddeef8acbd2ed757201615d77a5471c28296a1b`;
  Tooling SHA256 is
  `d75fb09296700b793ecbcf437836597878705dfd7eefe3314c80171427ff38ff`.
  `docs/implementation/builds510-validation-pair.json` independently binds the
  official release, original artifact/manifest, repository-signed package and
  exact paired bytes. Do not change argument/environment checks, canonical plan
  projection, source/catalog equality, owner profile selection, or any other guard.
- Extend the existing all-pairs control in
  `tests/Hexalith.Platform.Composition.Tests/PlatformOrdinaryDeclarationCompatibilityTests.cs`
  with this complete pair, preserving every old positive and crossed-pair
  negative assertion and all original test counts. Preserve CRLF bytes.
- Run meaningful focused compiled checks, preferring the canonical owning test
  project in Debug/source mode. Initialize only missing direct root dependencies
  using available original workspace Git objects if needed, never nested owners.
  Keep NuGet auditing and warning policy intact. Retain exact commands/results
  under `/tmp/hexalith-story-2-10-release-record-20261113`; report blockers honestly.
  Source/publication controls already passed on unchanged helpers; rerun only
  checks affected by this guard change. Do not start lifecycle services or AppHost.
- Return before committing, pushing, publishing or dispatching. The parent owns
  refreshed immutable companions and all actual hosted release gates after this
  final production change. Do not edit other files or prior evidence.

The later Parties qualification caller must retain Builds/catalog/helper source
`3c2f5944aaa28dc7854a00a8a9df171aa810b0c7`, matching the exact Platform source
catalog. It selects genuine CLI 5.1.0/source
`9ebc31f606bc02f8a0ec2f1ec344127e2468c6b4` separately. Direct caller Builds 9ebc
would fail HXW009, even though the two catalog blobs and required helpers are
byte-identical. The fixed-input schema already separates `sources.builds` from
`tool.sourceRevision`; preserve this distinction. Full lifecycle acceptance and
both exact main pins remain unchanged.

The canonical owning class passed all 4 cases after the recorded red failure.
The established private .NET 11 Debug/source context built with zero warnings or
errors; its default framework mismatch remains a separate retained blocker.
The final parent incorporated concurrent main documentation
`e134616ca8a79159a93c5bebde04cde1fc7e116b` before freezing companions. Source
comparison and every hosted package qualification gate remain mandatory; these
local controls grant no runtime acceptance.

## Matching Parties qualification caller — after verified publication

This is the current implementation unit. Platform source
`32643bde39b4da0e4538f8388e225727f82122f6` is actually published as 2.0.0
(release 444010416, publisher 43396965519). The original published receipt is
`/tmp/hexalith-story-2-10-release-record-20261113/hosted-run-43396965519/14023412455-source-release.json`.
All five tuple jobs and all six SDK catalog jobs passed for that exact source.
Source release supplies no native, candidate, Story or accepted-tool authority.

Prepare only a dedicated Parties qualification branch from the recorded clean
caller `f532145038992217f6433cd3dc1e36e61a1acc38`. Author in the existing isolated
`/tmp/hexalith-story-2-10-source-release-20261113/references/Hexalith.Parties`,
whose root superproject supplies the already-read permitted AI baseline. Read
its AGENTS.md and owning Git/build/commitlint guidance before edits. Set only
this task-owned clone's origin to official Hexalith.Parties. Keep its main ref
and the actual Parties upstream main tool manifest at 5.0.0; do not merge main.
Platform's exact tool pin remains 5.0.1. Use branch
`qualify/story-2-10-builds-5-1-0-platform-2-0-0-20261113`.

Make the bounded caller changes, preserving line endings and every gate:

- Candidate `.config/dotnet-tools.json`: exact module CLI 5.1.0, unchanged command
  and rollForward:false. This is a qualification target, not main acceptance.
- Direct `references/Hexalith.Builds` gitlink: catalog/helper
  `3c2f5944aaa28dc7854a00a8a9df171aa810b0c7`, exactly matching Platform's catalog.
  Direct Platform gitlink: the actual published source above. Initialize only
  these two direct caller dependencies, with no recursion or other nested owners.
- Both regular CI and `ci-lifecycle-qualification.yml` baseline use exact
  `domain-ci-v2.yml@9ebc31f606bc02f8a0ec2f1ec344127e2468c6b4`. This is the genuine
  published 5.1.0 executable/workflow source, distinct from its catalog source.
  Retain all 15 isolated projects, consumer validation, package profile, needs:
  baseline, scenario table, independent finalization and actual cancellation.
- `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs`:
  update only BuildsCiSha and the stale exact 4.48.0 module-tool expectation to
  the new candidate values. Preserve validation/release execution pins and all
  assertions. Update active CI documentation references, tool version/range and
  actual net11 target description; retain dated historical evidence as history.
- `scripts/verify-current-owner-integration.py`: set only the schema source
  constant to the actual catalog/helper 3c2f source after rederiving its schema.
  SHA256 remains 9773678febac38025fae837154341d9036ce8aa94e66b0d30c7e15ee668d7e39;
  required key sets must stay unchanged. Never weaken source checks for equal bytes.

Use pinned Node 24.22.1/npm 11.19.1 at
`/home/administrator/.nvm/versions/node/v24.22.1/bin` for owning commitlint and
npm tooling. Validate the exact complete message before any private candidate
commit. A private candidate commit is necessary to bind HEAD gitlinks for the
canonical reader tests; it confers no acceptance and is not pushed by the worker.

After authoring/committing, create an independent shared-object checkout of that
exact caller at `/tmp/hexalith-story-2-10-qualification-20261113/parties`, OUTSIDE
all Git worktrees. Carry the already-loaded same-owner instructions into this
immutable execution artifact; do not initialize a dependency for instructions.
Require actual show-toplevel==that caller and show-superproject-working-tree
empty. The resolver otherwise climbs to the outer Platform and refuses the dirty
caller gitlink. Only its own direct Builds 3c2f and Platform 32643 clones are needed.
Do all genuine preparation and canonical checks there. If a fix is needed, edit
only authoring checkout, commit a new candidate and make another fresh independent
verification checkout; retain earlier failed observations under their actual IDs.

Required resource-free checks: canonical `python3 -B -m unittest discover -s
tests/ci-lifecycle -v`; `python3 -B -m unittest discover -s scripts -p
'test_*_integration.py'`; declaration renderer --check and declaration controls;
actionlint for both changed workflows; canonical focused C# CI workflow/history
assertions in Debug, preserving package audits and warning policy. Default CI
project has no business ProjectReferences. Record exact commands, counts, source
and input hashes under `/tmp/hexalith-story-2-10-release-record-20261113/caller-controls`.

Restore and verify the genuine published 5.1.0 tool (the independently verified
archive and extracted closure are already retained outside the immutable cache).
Require exact version 5.1.0+9ebc31f606bc02f8a0ec2f1ec344127e2468c6b4 and byte equality
with its signed archive. Run only genuine runtime-free prepare-build in package
mode on the independent clean caller, with a new private workspace. Verify the
producer-issued mapping/environment imports using the actual selected helper.
Never fabricate GitHub metadata, start services, dispatch workflows, change
budgets, accept a tool, merge/push main, or alter historical local.py 4.47.0.

Return caller full commit/branch, standalone verified checkout, all control and
genuine preparation results, and any remaining blocker. The parent owns final
review, pushing the qualification ref and closed fixed inputs/provenance. It will
retain CLI 5.1.0/source9ebc separately from catalog/helper 3c2f, Platform 32643 and
actual caller SHA. Keep every main accepted-tool pin and Story status unchanged.

## Caller upstream reconciliation — current continuation

The first caller 3a7e1d6ab2ca8375d794426fce135cbcddc2decc passed its resource-free
checks and genuine preparation. Preserve all original observations in
`caller-controls`; do not relabel them for another source. An external owner
operation advanced actual Parties main to
`1d44297aa7ed94426a51b9e4fb14b7d5896c4157`, including NET11 baseline compatibility
fixes and a 5.0.1 main tool pin. This work did not change that accepted main pin;
preserve the external state and never roll it back or claim it stayed 5.0.0.

Before pushing the qualification caller, incorporate that exact upstream source
into the private qualification branch. Keep all external business/package/test
fixes byte-identical to upstream. Reapply only the qualification target deltas:
CLI 5.1.0; both domain-ci reusable refs 9ebc31f606bc02f8a0ec2f1ec344127e2468c6b4;
matching C# BuildsCiSha/tool expectation; clear current qualification documentation.
Keep upstream validation/release/CodeQL pins and corresponding assertions, all
baseline/gate/finalization/cancellation definitions, source/catalog 3c2f, Platform 32643
and schema constants/required sets intact. Use upstream versions as the conflict
base, then apply the narrow qualification changes, rather than discarding new
upstream fixes. Update the currently descriptive release-execution paragraph to
its actual upstream pin; leave dated historical sources/results unchanged.

Validate the exact new candidate message with owning pinned commitlint, then
commit privately. Record full parent/merge identities. Verify in a NEW independent
checkout `/tmp/hexalith-story-2-10-qualification-20261113/parties-v2`; retain the
old standalone checkout and its old producer mapping unchanged. Keep same-owner
guidance already loaded; no instruction-only dependency init. Re-run the same
canonical Python, declaration, actionlint and focused compiled C# checks, then
verified genuine runtime-free package preparation against the new clean caller.
Retain all results under
`/tmp/hexalith-story-2-10-release-record-20261113/caller-controls-v2`.

Return the new full caller/branch/source and actual results. Do not push or merge
main, dispatch workflows, start services, change budgets or accept a tool. The
parent will create final fixed inputs only for this newly verified subject and
will state the external main-pin drift accurately in the continuation record.

## Source prerequisite and fixed-input outcome — 2026-11-13

- [x] Publish compatible Platform source 2.0.0 at
  `32643bde39b4da0e4538f8388e225727f82122f6` after all actual source gates.
- [x] Publish the separate Parties qualification caller
  `5f94d7827f5b1d315856bb37b64f700e3ac56910`, retaining upstream 1d44297a
  fixes and every qualification/finalization/cancellation guard.
- [x] Record the closed [fixed inputs and full handoff](evidence/2-10/source-release-20261113/qualification-5f94d782/README.md).
  Canonical input SHA256 is
  `06ba09a2179dd78dd7de29be5c9195400d172da1afa960a104aa4268a646b91a`.
  Source audit, no-superproject binding and catalog-source equality pass.
  All five archive digests, cache hit and two private fresh restores pass.
- [ ] Full 5.1.0 lifecycle qualification and accepted-tool enrollment remain open.

The actual publisher 43396965519 retained status published with both exact-source
qualifications and final mutation rechecks. Required tuple controls pass 478/97/9
for current profiles, 349 legacy and 412 pre-extension; all six catalog SDK jobs
pass. Caller checks pass 183/74/7 Python and 78 compiled cases with zero skips,
errors or failures, plus workflow lint, zero-warning Debug build and genuine
runtime-free package preparation. These remain preparation/source checks, never
new native, candidate, Story or tool acceptance.

This work changed no main accepted-tool pin. An external operation changed
Parties main to 5.0.1 at 1d44297a; that state was preserved. CLI 5.1.0 exists only
on the qualification branch, with catalog/helper 3c2f and published Platform 32643.
The old source subjects, original 4.47.0 local controller, every failed/incomplete
observation and all private first-caller 3a7e results retain their identities.
Story status remains in-progress and the full local/hosted/cancellation table is
still required. No lifecycle service or qualification workflow was launched.
