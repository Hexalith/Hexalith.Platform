---
title: 'Story 4.17 slice: assess controlled Kafka publication association'
type: 'chore'
created: '2026-11-14'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
baseline_commit: '57f059f916eb2c5c76bc8efe7a11ad2bb9842fac'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — continuation of the existing bounded offline broker investigation">

## Intent

Assess the next source-only task identified by the completed rotation feasibility decision: whether controlled association can identify the stale HTTP operation's Kafka Produce request set while preserving the same live old publisher and the existing qualification predicate. Account for earlier component invocations surviving HTTP or resiliency timeouts, Sarama batching/retries and outstanding broker work. A missing supported invariant is an explicit valid outcome, not authority to infer isolation.

Record the source-backed invariant, the evidence that can and cannot establish it, and the stop decision in `docs/implementation/story-4-17-kafka-controlled-association.md`; append the result to the existing candidate intake. Preserve original evidence and the separate forwarding/principal, applied-ACL/audit and cold-old-generation dependencies. This authorizes bounded offline assessment only. No implementation, diagnostic execution, upstream message, runtime/profile/predicate change, broker selection, commit or push.

</frozen-after-approval>

## Implementation Notes

- Intent gaps: none for this assessment; unresolved instrumentation is its output. Irreversibles: none; primary-source reads and local documentation. Footprint: this slice spec, one new decision document, append-only `docs/implementation/story-4-17-next-candidate-intake.md`. Both owning repositories start clean on main. No EventStore edits or Git mutations are needed.
- Loaded valid Epic 4 context and previous completed Story 4.15 preparation continuity: unknown operational inputs remain explicit blockers, offline evidence supplies no live acceptance, and existing status/evidence/gitlinks are preserved. The current continuation is governed by the completed `spec-4-17-select-and-qualify-the-durable-production-broker-3.md` and its feasibility report.
- Reuse EventStore `tools/qualify-dapr-brokers.py`: `request`, `components`, `run_broker`, `recover`, rotation in `scenario`; `tools/dapr_broker_qualification.py`: exact scenario order and predicates; `deploy/dapr/qualification/kafka.yaml`: scope/client metadata. Preserve all inputs. Candidate-02 configuration keeps Kafka's 3s outbound timeout, two retries and 200ms retry interval; pressure follows rotation and is not its prior workload.
- Read-only delegated investigation checked the pinned component/Sarama send/retry/completion paths. Dapr `cfb5ee090dda7cfc26145c7703605d831350aa2a` selects components `1d331c389f11baaa1d664022b6b6c2f59811ce11` and Sarama `8f58ebdcefa5381716d29605b4415fe9f1d59cc1`; source declarations are not new executable attestation. Parent additionally inspected Dapr's publish wrapper and timeout/retry runner.
- Verification: inspect immutable primary-source links and source claims; check new local links, whitespace/newlines and historical intake prefix; compare 264 snapshotted protected files except the authorized intake append, unchanged Git heads/indexes and EventStore tree. Documentation checks supply no broker qualification. Use one independent blind document reviewer under the one-shot workflow.
- Preserve Platform 4.17/EventStore 3.21 in-progress, five historical guarantees REJECTED/open, technical FAIL (20/24), selection null and production FAIL. Packet custody stays complete. Only this bounded assessment can finish; repository guidance reserves commit/staging/push for explicit requirements and the existing offline scope preserves parent statuses.
- Implemented the source-only decision and appended the intake outcome. The required invariant accounts for all earlier invocations, retries and outstanding broker work before attributing the stale request set. No supported nonclosing witness was established, so the proposed construction meets its stop rule. Message-local completion is useful but cannot certify whole-publisher quiescence; this source inference establishes no historical cause.
- Direct read-only `git ls-remote` confirmed all three upstream tag revisions. Primary-source reads checked Dapr's effective module declarations and retry wrapper, the selected ordinary nontransactional publication branch, Sarama message/retry completion and the component's actual client ownership/teardown wrapper. No new binary attestation is claimed.
- Initial verification passed 19 local link targets, whitespace/newlines, exact preservation of the historical intake prefix, 263 unchanged protected files, original 24/20/4 verified assessment and both parent in-progress statuses. Both canonical Git heads and empty indexes were unchanged; EventStore remained clean. No runtime tests apply to this documentation-only slice.
- Independent blind review returned four findings. Checked each against the actual source and workflow state: clarified effective producer defaults and absent transaction flags/token; corrected the Sarama no-op closer versus component real-client closer; added the missing five-second publish citation; reserved slice completion for final checks. All findings are resolved locally; nothing newly deferred.
- Final verification after corrections passed all 19 local targets, whitespace/newlines, append-only intake, all 263 protected-file comparisons, unchanged frozen-intent hash, both canonical heads/empty indexes, clean EventStore tree and original verified 24/20/4 assessment. Exactly the three planned files changed. Only this assessment slice is done. The existing bounded offline scope preserves parent statuses and precludes commit/push, overriding generic one-shot parent-review/commit instructions; no Git mutation or diagnostic occurred.

## Review Triage Log

| Finding | Verdict / route | Evidence and disposition |
| --- | --- | --- |
| B1 effective publication branch unspecified | medium / patch | Qualification Kafka metadata and its bounded renderer enable neither producer nor consumer transactions; HTTP metadata supplies no `__txnToken`. Pinned metadata defaults select WaitForAll, five producer retries and false transaction flags. Added the configuration basis and distinguished Sarama retries from Dapr retries. |
| B2 client shutdown ownership omitted | medium / patch | `NewAsyncProducerFromClient` installs `nopCloserClient`; the component's `clientOwningSyncProducer.Close` closes the real client after SyncProducer. Corrected the teardown description and linked both paths without claiming a nonclosing drain or certain broker outcome. |
| B3 five-second timeout citation absent | low / patch | Original source link showed `request`, whose own default is 3s. Added the separate immutable `publish` lines 590–595 declaring/forwarding 5s and retained the stale-call reference. |
| B4 slice still in-progress during review | low / finalize | This was the expected review-time state. Finalize only this slice after actual final checks, recording verification here; preserve both parent statuses and original assessments. |
