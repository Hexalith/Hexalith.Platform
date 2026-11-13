---
title: 'Story 4.17 slice: reconcile the verified broker handoff with EventStore'
type: 'chore'
created: '2026-11-13'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
review_loop_iteration: 0
baseline_commit: 'a7f901ae79ed5fd7ad2f04f0779b21108a4cdee9'
context:
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — continue Story 4.17 on main within its existing qualification constraints">

## Intent

**Problem:** Platform's completed remediation intake records candidate-02's independently verified 20/24 result, while EventStore's current parent assessment still names candidate-01's pre-allocation startup failure and locked-input drift as the latest blockers. The owner handoff must reflect the evidence before further broker work can be specified.

**Approach:** Reconcile EventStore's current assessment, deferred remediation entry and parent tracking with the existing immutable Platform assessment and next-candidate intake. Preserve historical evaluations and record the three engineering dependencies: a feasible Kafka pressure fault, Kafka rotation causality/evidence, and supported RabbitMQ publish-path fixes. This is an offline documentation correction within the continuing broker qualification work.

Keep both parent stories in-progress, all 24 vectors and five historical guarantees REJECTED/open. Distinguish the four current failures from the unadjudicated historical Kafka rotation failure. Record integrity, technical qualification, cleanup and production authority separately. Recording verified evidence facts grants no acceptance of a diagnostic plan, selected broker, inventory row, new execution or exception. Preserve owner frozen intent, acceptance criteria, completed slices, historical packets and failed assessments.

Work on main as instructed. Do not run services, Docker, Aspire, workflows or cluster operations; change a runtime, harness, profile, gitlink or guarantee; or publish remote changes. The exact candidate identities govern the historical run; the current checkout is only the documentation review baseline. Complete only this bounded reconciliation, with full broker selection and staging prerequisites still outstanding.

</frozen-after-approval>

## Implementation Notes

- Route facts: no unresolved intent choice is needed to correct evidence records; no external effects; three existing EventStore documentation/tracking files plus this Platform spec. No callable interface or behavior changes. User requests continuation on main; no remote publication is authorized.
- Owner baseline: EventStore `fd07d0ab33858818d836e43bf8c112c5d4827534`. Its producer, verifier, configuration helper and platform-config lock remain byte-identical to candidate-02 EventStore `495dd24943031ec75e2822c6168ada3c8b9fdb82`; independent offline profile admission validates 94 inputs. Source identity does not imply owner acceptance.
- Reuse Platform `docs/implementation/story-4-17-broker-candidate-02-hosted-attempt-1.md`, its `attempt-assessment.json` / `vector-assessment.json` under implementation-artifacts `evidence/4-17/candidate-02-hosted-attempt-1/`, and `docs/implementation/story-4-17-next-candidate-intake.md`. Candidate run 43386723091 binds Platform `4d66ff5584e4f62239551bb02311649334171a84`, EventStore `495dd24943031ec75e2822c6168ada3c8b9fdb82` and configuration SHA-256 `b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b`.
- Edit EventStore `_bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md`: append a dated assessment and change-log entry; identify the November 12 assessment as historical; update the current follow-up and Verification lead. Link immutable Platform source/evidence URLs so ordinary checkouts need no nested Platform submodule. Preserve all frozen text, tasks and criteria.
- Append a dated update to the existing EventStore `_bmad-output/implementation-artifacts/deferred-work.md` runtime-refresh owner-closure entry. Keep historical 19/24 facts; record current verified 20/24, four FAILs, all five open historical guarantees and unresolved remediation owners. Do not imply supported fixes or an executable successor exist.
- Add one continuation comment by EventStore `_bmad-output/implementation-artifacts/sprint-status.yaml` Story 3.21; keep its status value unchanged. Platform tracking already has the accurate assessment and needs no duplicate report.
- Verification: parse both original assessments, match counts and identities, validate all 94 current locked inputs without subprocesses/network, compare protected file hashes and frozen intent, check local links/immutable external URL paths and changed-line whitespace. Recheck both parent status values. No behavioral tests are justified by documentation-only changes.
- Full Story 4.17 remains unable to qualify a broker: another configuration-only retry cannot produce the accepted Kafka pressure error, principal/authorizer causality remains unmeasured, supported RabbitMQ fixes and AD-26/profile decisions are absent, and staging prerequisites 4.14–4.16 are unaccepted. Keep these dependencies explicit at completion.
- Implemented the three owner-record corrections. Added immutable Platform evidence links at `a71fee52cf03417e439e0b1141ccc64ae5cf3ad9`, confirmed that commit through GitHub's read-only API, and verified linked file bytes against their exact Git objects. Marked November 12 startup/drift statements historical rather than deleting them; no acceptance, new packet or instrumentation is claimed.
- Offline audit passed with inherited `PYTHONOPTIMIZE=2`: all 94 profile inputs, nine protected hashes, four candidate-source comparisons, frozen intent/tasks/ACs, 24 vector identities/counts, original configuration/run/verifier/cleanup/authority, both parent status values and main branches, and four immutable evidence file links. Exact command and limits are recorded in the owner parent spec. Both repositories pass changed-line whitespace checks.
- The explicit bounded scope preserves parent 4.17 in-progress, overriding the generic one-shot final parent transition to review. Only this reconciliation spec can finish. Local completion commits must exclude the parent EventStore gitlink: remote publication is unauthorized, and the workspace policy forbids recording an unpublished submodule commit. The owner documentation commit can remain local for later publication/adoption.
- Scope clarification: preserve the full 24-vector denominator; exactly the five historical guarantees remain REJECTED/open. The frozen paragraph's abbreviated wording must be read with its explicit verified 20/24 result, never as rejection of every vector. No frozen text was rewritten.
- Review corrections add reproducible offline audit logic directly to the owner assessment, the missing Platform 4.9 profile-inventory prerequisite, and current proposed downstream rows (Kafka 11/12; RabbitMQ 9/12). A pre-existing full-packet custody decision is deferred to Platform before the recorded December 13 expiry; one entry was appended to the Platform deferred ledger, adding that file to the original footprint. This correction supplies no storage destination, transfer or operational authorization.
- Final verification extracted and ran the exact documented audit block with `bash -eu`, including Python optimization: exit 0 and all recorded comparisons pass. Parsed sprint data is unchanged in both repositories, current proposal counts match the original assessment, and changed-line whitespace passes. Review resolved four findings by documentation corrections/clarification and deferred one pre-existing custody decision. No additional review layer or behavioral test was required.
- Commit preflight: pinned commitlint 21.2.4 accepted the exact messages in `/tmp/platform417-owner-reconciliation-elxsnl8b/{eventstore-commit.txt,platform-commit.txt}`, each exit 0 with zero problems/warnings. Only the reviewed owner documentation/tracking files and root reconciliation spec/deferred entry enter local commits; the parent gitlink and all other paths remain excluded.
- EventStore local completion commit: `5f4db84502c7c54b316dd9429dccd12ae881e759`, on main, three reviewed files; staged-file policy passed (3 checked) and the owner tree is clean. No push occurred. Platform's recorded gitlink remains `fd07d0ab33858818d836e43bf8c112c5d4827534`; publication and later gitlink adoption remain outstanding, so the owner commit is available only in this workspace until published.

## Review Triage Log

| Finding | Verdict / route | Evidence and resolution |
| --- | --- | --- |
| B1 ambiguous full-denominator wording | low / patch | The frozen approach explicitly states verified 20/24; clarify in Implementation Notes that the denominator is preserved and only the five historical guarantees remain REJECTED/open. Preserve frozen text. |
| B2 temporary audit cannot be reproduced | medium / patch | The initial command depends on a temporary script and before.json. Retain self-contained audit logic in the changed owner assessment, deriving baselines from exact Git objects and existing tracked assessments instead. |
| B3 full-packet custody before expiry has no decision | medium / defer | Original attempt-assessment artifact custody already expires 2026-12-13T07:40:43Z and contains bytesCommitted=false. Append one Platform-owned follow-up with deadline, exact artifact/digest and required custody/operator decision; no accepted storage or transfer can be invented by this correction. |
| B4 missing Platform 4.9 dependency | medium / patch | The governing intake explicitly requires the Story 4.9 profile inventory. Add it beside AD-26 and staging prerequisites in the current owner assessment. |
| B5 only historical downstream proposals retained | medium / patch | The November 12 table correctly describes its old subject but cannot serve current proposals. Add separate current, non-accepting Story 3.17 Kafka/RabbitMQ and Story 5.9 rows with verified per-broker counts and current admission limits. |
