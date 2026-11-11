---
title: 'Story 4.17 slice: manual hosted CI for Platform broker candidate 01'
type: 'chore'
created: '2026-11-11'
status: 'done'
route: 'dispatch'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
review_loop_iteration: 0
baseline_commit: '15926cac7ffe7bb91926617a586f34262beb0665'
context:
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — user selected recommended manual CI implementation; dispatch pending">

## Intent

**Problem:** Candidate 01 passed offline admission but Platform has no hosted workflow accepting it. Five historical guarantees remain REJECTED; no exclusive local worker is allocated.

**Decision:** On 2026-11-11 the user said "apply recommended" after comparison of local execution and a small manual hosted workflow. Implement the recommended workflow and verify it offline. This approves implementation only; publication and live dispatch remain pending.

**Approach:** Add one manual Platform workflow/job invoking the pinned EventStore producer/verifier on a fresh standard Ubuntu VM. Bind the worker to real job/daemon identity and retain separate outcomes and sanitized evidence.

## Boundaries & Constraints

**Always:** Original candidate SHA-256 `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc`, clean EventStore revision `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213`, explicit `runtime-lock-1.19.1-platform-config-v1.json`, both brokers/all 24 vectors. Use `ubuntu-24.04`, read-only repository permissions, existing SHA-pinned actions and verified isolated tools. Record actual Platform revision, runner image, allocation and command identities. Supply original configuration independently to every verifier. Preserve both parent statuses.

**Never:** Dispatch, push, commit, publish, run Docker/infrastructure locally, modify candidate/harness/locks/gitlinks, initialize nested submodules, add automatic triggers/schedules or a runner framework, select a broker, grant exceptions or claim live/staging/production qualification. Allocation creation is permitted only within the future verified hosted VM job. Missing evidence stays incomplete.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior |
| --- | --- | --- |
| Intended run | Hosted Linux x64 VM, exact inputs, fresh paths | Bind real allocation; invoke both brokers with explicit profile/configuration |
| Unready | Wrong runner/revision/hash, dirty EventStore, reused destination | Fail before installation/producer; retain diagnostics |
| Technical rejection | Nonzero producer/technical exit | Still evaluate independently; retain statuses; final job fails |
| Broken evidence | Missing/invalid packet or verifier failure | Diagnostics retained; no integrity/qualification credit |
| Bounded success | Capture/integrity/technical succeed; authority rejects production | Technical success separately; production FAIL; null selection |
| Setup/cancellation | Earlier step fails or execution interrupted | Summary/upload attempted independently; absent statuses/cleanup incomplete |

</frozen-after-approval>

## Code Map

- `docs/implementation/story-4-17-broker-candidate-01.{md,json}`: preserve original input/handoff.
- `references/Hexalith.EventStore/.github/workflows/dapr-broker-qualification.yml`: allocation/tools/capture/verifier/upload pattern; lacks Platform input.
- EventStore `deploy/dapr/qualification/README.md:77,217` and `tools/{qualify-dapr-brokers.py,validate-dapr-broker-qualification.py}`: allocation, schema-3 evidence, producer/cleanup/verifier; consume unchanged.
- `.github/workflows/tuple-ci.yml`: existing action SHA pins and runtime-path patterns. Initialize only root EventStore without recursion.
- `tests/source-release/fixture-workflow.test.mjs`: js-yaml parsing and real shell tests. New `*.test.mjs` here is automatically included by `npm run test:source-release` and existing CI.
- `_bmad-output/planning-artifacts/epics.md:2412` and implementation-artifacts `deferred-work.md:1926`: outstanding parent criteria and missing execution acceptance.

## Tasks & Acceptance

**Execution:**
- [x] `.github/workflows/broker-candidate-qualification.yml` — add one manual job without arbitrary input/command support. Bootstrap evidence before fallible setup; validate sources/hash/clean tree, supported runner/runtime and fresh paths. Bind actual local socket/daemon/run allocation inside the hosted job; install pinned tools. Invoke existing producer and all three verifier modes, preserving raw exit codes/transcripts outside packet inventory. Summarize and upload sanitized artifacts independently on failure. Expected authority rejection cannot mask other failures; skipped steps stay incomplete. Original JSON remains outside packet. Preserve cleanup receipts; cancellation/upload cannot guarantee complete evidence.
- [x] `tests/source-release/broker-candidate-workflow.test.mjs` — parse real YAML and execute its blocks using disposable fixtures/fake commands. Cover every matrix row, guards before installer/producer, status aggregation, independent evaluations, artifact conditions and complete invocation pairing. Reuse js-yaml; stub external boundaries, not workflow logic. No live services/new dependency.
- [x] `docs/implementation/story-4-17-broker-candidate-01-ci.md` — document manual invocation after publication/authorization, fixed inputs, expected failures, separate verdicts, artifact download/retention and cleanup/cancellation limitations. State no live run occurred and no general Platform runner/tool acceptance is established.
- [x] `_bmad-output/planning-artifacts/epics.md` — add owner-accepted bounded candidate CI implementation/ownership note under 4.17, preserving selection/staging/Subscription criteria. Separate planning scope, unexecuted workflow and future results.
- [x] `_bmad-output/implementation-artifacts/{sprint-status.yaml,deferred-work.md}` — reference prepared CI and outstanding publication/dispatch/results. Preserve parent `in-progress`; keep broader diagnosis, staging and broker selection open.

**Acceptance Criteria:**
- Given the definition, when executed in future, then only manual dispatch starts its single hosted job with read-only repository permissions and exact inputs.
- Given offline controls, when run, then every matrix row passes without live services and nonzero capture cannot skip independent evaluations.
- Given failures/skips, when reporting, then separate actual statuses and incomplete evidence grant no success, with summary/upload independently attempted.
- Given completed implementation, when tracking updates, then no execution, broker selection, staging or production acceptance is claimed.

## Implementation Notes

Controller completion follows the explicitly invoked bmad-build Step 5 local-commit instruction; the implementation handoff's no-commit restriction kept Git recording with the controller. Publication and live dispatch remain unauthorized. Parent status synchronization to review is intentionally skipped: this completed slice preserves Platform 4.17 in-progress because selection, staging and Subscription acceptance remain outstanding.

Implemented the six deliverable files. Diff audit includes new files and preserves baseline, original candidate/handoff, EventStore source/gitlink and both parent statuses. Offline verification: 41 workflow controls, 70 source-release controls, actionlint, Bash syntax, whitespace, links and real pinned parser/profile admission (91 inputs) passed. No live execution or remote operation occurred. Each matrix row has executable offline coverage; cancellation checks model incomplete status reporting and do not claim real hosted cancellation proof.

The user's explicit selection resolves the planning choice and approves implementation; proceed without another approval request. This replaces the prior local-execution draft. Preserve baseline and main branch; no remote operation is authorized.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| BH1 Git status error admits cleanliness | medium | Reproduced with an empty-output failed command inside test -z under errexit. Both workflow cleanliness checks discard Git status. Patch: checked assignments and failure cases. |
| BH2 guard failures have no actionable reason | medium | Bare test failures emit no reason and raw logs are withheld. Patch: fixed safe source/setup phase codes alongside actual exits. |
| BH3 rejected inputs lose observed identities | medium | Bootstrap records expected dispatch context; actual source/configuration observations are written only after admission. Patch: retain safe observed revision/hash metadata before corresponding comparisons, without retaining rejected configuration bytes. |
| BH4 evidence read/copy errors abort reporting | medium | Report performs unguarded status/transcript reads, authority decoding and packet traversal/copy before outcomes/summary. Patch: isolate evidence-processing errors and keep the result incomplete while attempting remaining reporting. |
| BH5 invalid packet could contain arbitrary credentials | false | The only writer is the exact pinned producer: retained source files, rendered configuration and redacted application/command receipts; its private trees are outside packet. No path by which an arbitrary credential file reaches this owned packet was demonstrated. Semantic integrity failure is not a sanitization decision; adding an untrusted-writer policy would not fix a reachable defect here. |
| BH6 technical result precedes upload result | false | The JSON and summary explicitly call their result boundedTechnicalSuccess / bounded technical success. The final always gate independently requires successful reporting and upload; Actions retains those step/job outcomes. No overall success is granted before delivery. |
| BH7 upload glob implementation untested | low | The tests assert the exact include/exclude configuration and the SHA-pinned external action supplies glob semantics. No incorrect pattern or leaked file was demonstrated. Reject: testing the external action would add a new dependency solely for its implementation; literal pattern changes already fail existing controls. |
| BH8 actual report exceptions untested | medium | Tests currently change report/upload outcome variables after a successful report. Patch: execute a real summary/projection failure and assert confined evidence paths, incomplete reporting and final failure; keep remote upload inspected as an always attempt. |
| BH9 manual-dispatch prerequisites missing | medium | Handoff mentions a published ref but omits default-branch presence and maintainer write access required by GitHub workflow_dispatch. Patch the invocation instructions directly. |
| VG1 selected installed runtime unobserved | medium | Pre-verified mutation changed runtime/daprd to runtime/missing-daprd while all 41 controls passed. Patch: installer creates executable stubs; fixture capture consumes selected runtime/CLI and exposes asserted receipts. |

Edge-case review returned no findings. All three layers completed before triage. Seven findings route to local patches; no intent change, re-derivation or deferred issue is required.

Review patch completion: all seven patch findings were fixed in the existing workflow/tests/handoff. Checked Git assignments, safe phases and observed identities, isolated reporting failures, actual runtime/CLI execution fixtures and manual-dispatch prerequisites are covered. Focused controls passed 53/53; parent final full source-release run passed 82/82 (zero skipped/cancelled). Final actionlint, shell syntax, tracked/new-file whitespace, local links and real pinned profile/parser checks passed. The 91 inputs, 24-vector denominator, original configuration/handoff bytes, source baseline and EventStore clean revision/gitlink remain unchanged. No findings were deferred.

Commit-message preflight: pinned @commitlint/cli@21.2.4, exact message file /tmp/platform-broker-ci-commit-fjfr2iwx.txt, exit 0 with zero problems/warnings. Only controller-owned reviewed changes enter the local commit; no remote operation occurs.

## Verification

- Run focused Node tests, `npm run test:source-release`, YAML/shell syntax checks and `git diff --check`. Use existing dependencies and fake commands; never execute real qualification.
- Check protected hashes/gitlinks, vector/profile pairing, local links, bounded tracking and the actual complete diff.
