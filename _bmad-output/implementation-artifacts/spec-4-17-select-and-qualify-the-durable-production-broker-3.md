---
title: 'Story 4.17 slice: resolve Kafka rotation observation feasibility'
type: 'chore'
created: '2026-11-14'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
baseline_commit: '8bd5e7330003c04895b0b86b0f5bb04fb4cf8957'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — existing owner-approved offline investigation scope">

## Intent

Complete the next bounded offline step of Story 4.17: determine whether supported stock Kafka and existing worker observations can join the actual stale publisher/process and credential generation to its authenticated Produce request and applied authorization state. The existing logging proposal leaves three joins unresolved; implementation must not silently infer them from timing, app IDs or payload length.

Write a source-backed feasibility decision in `docs/implementation/story-4-17-kafka-rotation-observation-feasibility.md` and append its outcome to `docs/implementation/story-4-17-next-candidate-intake.md`. Identify supported observation methods, exact producer/verifier/admission changes and meaningful refusal controls if feasible; otherwise record the precise remaining blocker and useful next decision. Preserve historical evidence and the outstanding cold-generation control. This continues the recorded owner-approved offline work; it authorizes no instrumentation execution or qualification attempt.

</frozen-after-approval>

## Implementation Notes

- Intent gaps: none for this bounded feasibility assessment. An unsupported or unresolved join is a valid explicit result, never an assumed correlation.
- Irreversibles: none. Read-only primary-source inspection and local documentation only; no upstream messages, remote writes, broker/cluster runs, runtime-pin changes, candidate dispatch, commit or push.
- Footprint: this spec, one new Platform decision document and an append-only intake update. EventStore owns any later producer/verifier implementation; existing tools, locks, configuration, custody receipts, status files and gitlinks remain unchanged.
- Reuse EventStore `docs/implementation/story-3-21-kafka-bounded-investigation.md` and `story-3-21-broker-remediation-evidence-contract.md`; inspect `tools/qualify-dapr-brokers.py` (`Run.kafka`, `sidecar`, `diagnostics`, rotation in `scenario`, `finish`) and `tools/dapr_broker_qualification.py` admission. Kafka source is pinned to `26b251a451ce941d3d7a55e6487bcb7f16b5ad48`; current runtime/components releases rechecked read-only as 1.19.1/1.19.0.
- Verify immutable source links and claims, local links/whitespace and unchanged protected inputs. No new test harness is needed for documentation. Independently review the feasibility reasoning before closure.
- Parent Platform 4.17 and EventStore 3.21 remain in-progress, five historical guarantees REJECTED/open, technical FAIL (20/24), selection null, production FAIL. Only this offline assessment slice can finish; existing offline boundaries override generic parent-status/commit instructions.
- Completed the new feasibility decision and appended the intake outcome. Additional source findings: the existing forwarder emits no paired-socket receipt; the stock Log4j pattern omits thread identity; a handler TRACE entry offers a prospective audit join; the reused publisher and context-ignoring component prevent assuming quiescence after HTTP timeout. No complete HTTP/Produce discriminator or connection/applied-state join is claimed.
- Local checks passed: 18 local link targets, final newlines/whitespace, exact preservation of the historical intake prefix, only the intended tracked intake append, unchanged EventStore tree and unchanged protected inputs. Primary immutable source paths and published EventStore source were checked read-only. Documentation-only scope requires no broker or behavioral test execution.
- A filesystem-capacity failure partially wrote the new report on its first creation. The task-owned report was replaced completely through a flushed atomic write; the intake was also updated atomically. Subsequent checks covered the complete final bytes; no historical file was truncated.
- Independent blind review returned four findings; all were checked against source and patched. The report now distinguishes snapshot construction from serving-cache publication, names the additional handler logger and Root retention path, separates YAML references from resolved credential evidence, and proposes one bounded controlled-association investigation with an explicit stop criterion. Nothing newly deferred.
- Final verification passed all 18 local link targets, whitespace/newlines, the append-only historical intake check and unchanged protected-input/parent-status checks; `git diff --check` passed. Frozen intent remained SHA-256 `0907b4e68c32b737cd4ba747f6824fce5f0994c25851bed7a57e60a04365e551`. Only this assessment is done. Recorded owner-approved offline constraints preserve both parent statuses and forbid commit/push, so generic finish synchronization/commit actions do not apply.

## Review Triage Log

| Finding | Verdict / route | Evidence and disposition |
| --- | --- | --- |
| B1 snapshot trace can precede serving-cache publication | medium / patch | Confirmed `StandardAuthorizer.loadSnapshot` calls `newData.addAcl` before publishing the cache. Added explicit snapshot-trace refusal; an addition line alone is not an applied-state witness. |
| B2 handler logger and output path absent from change map | medium / patch | Confirmed `kafka.server.KafkaApis` is an additional TRACE logger, with its RequestAppender stanza commented out and Root routing to server.log/stdout. Added required logger/output and exact-image verification limit. |
| B3 loaded YAML does not establish resolved credential generation | medium / patch | Confirmed components retain unchanged OpenBao references while rotation changes the secret. Added explicit resolved-generation/authenticated-credential blocker and YAML-only attribution refusal. |
| B4 useful bounded next decision missing | low / patch | Original outcome only restated three dependencies. Added one source-only controlled-association task with an in-flight accounting invariant and stop if unsupported or incompatible with the live-session predicate. |
