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
