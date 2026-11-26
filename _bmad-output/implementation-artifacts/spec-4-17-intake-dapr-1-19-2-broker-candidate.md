---
title: 'Story 4.17 slice: Prepare the Dapr 1.19.2 broker candidate'
type: 'chore'
created: '2026-11-26'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
baseline_commit: '4d5bbd13fea875467a1c4964e07639fec6fc8543'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — existing approval for offline 1.19.2 intake preparation">

## Intent

Advance the next external prerequisite for EventStore 8.7 and Parties DW-97 by preparing Platform 4.17's intake of the EventStore 3.21 Dapr 1.19.2 candidate. Verify the runtime/control plane and embedded components-contrib at 1.19.2 and CLI and .NET SDK at 1.19.1. Bind the proposed hosted attempt to exact source, runtime lock, bounded component inputs, worker and evidence. Preserve the 24 existing vectors, predicates, historical packets and candidate-02 result of 20/24. Record supported fixes and the owner, required proof and resume condition for each acceptance blocker. Seek separate execution approval only once the subject is ready. Limit this slice to preparation and focused offline verification. Broker selection, production eligibility, target custody and G5 remain pending, with G5 requiring EventStore 8.11 approval at the exact provider package or source revision.

</frozen-after-approval>

## Implementation Notes

- The requested intake decision is reversible documentation. The missing execution inputs are findings for the owner handoff. The approved preparation scope has no unresolved intent choices or external side effects.
- Update `docs/implementation/story-4-17-next-candidate-intake.md` and link it from Story 4.17 and the sprint tracker. Use the EventStore runtime lock and broker acceptance contract, candidate-02 workflow, configuration and retained assessments. Keep the executable workflow and cross-repository pins intact while the owning profile refuses the candidate. Distinguish the committed intake revision from the independently edited EventStore 8.7 prerequisite review.
- Verify in a disposable local Git archive of EventStore. Check the runtime lock, exact binaries and retained identities, full matrix and historical assessments, admission refusal, existing broker controls and Platform workflow fixture tests. Verify the story statuses and changed-file scope.
- Recorded the blocked attempt in the [4.17 intake](../../docs/implementation/story-4-17-next-candidate-intake.md#dapr-1192-candidate-intake--2026-11-26). Reused the exact candidate-02 configuration as the comparison baseline. The admitted successor must freeze its final emitted input bytes and both execution revisions before seeking approval.
- Offline verification passed the documented lock/configuration/refusal reproduction, three EventStore suites (25 broker, 32 configuration and 102 runtime tests) and 53 Platform workflow fixtures. Confirmed the exact release tuple, the 24-vector inventory, the retained 20/4 result, five historical open guarantees and the original acceptance criteria. The parsed Platform tracker is identical to the baseline. Platform 4.17 stays in progress under the existing acceptance criteria.

## Review Triage Log

| Finding | Verdict and resolution |
| --- | --- |
| Kafka rotation request attribution | Medium. Restored the existing stop rule for prior invocations and detached retries on the same live publisher. |
| RabbitMQ pressure fix provenance | Medium. Distinguished the released NACK helper behavior from the still-required public publish, retry, reconnect and cancellation path. |
| Independent packet replay | Medium. Bound the provider ZIP digest and verifier output commitments. Replay takes configuration from the dispatch revision. |
| Protected writes | Medium. Recorded the exact 1.19.2 tuple's current ineligibility under the EventStore capability screen. |
| Same-inventory handoff | Medium. Bound the accepted Story 4.5 record and named the remaining 4.9/4.16/4.22 prerequisites. |
| Repeatability | Low. Added the rendered Kafka hash and executed the documented offline reproduction. |
