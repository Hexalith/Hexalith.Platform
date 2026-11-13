---
title: 'Story 4.17 slice: offline broker remediation intake'
type: 'chore'
created: '2026-11-13'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
review_loop_iteration: 0
baseline_commit: 'e5460573be3a16e50d0728bb42942514fc07ff1f'
context:
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.17 requires a qualified durable hosted broker. Candidate-02 supplies an independently verified 20/24 baseline, but four vectors fail and the historical Kafka rotation failure remains unadjudicated. No eligible broker is selected.

**Approach:** Resolve owner decisions before specifying a successor candidate. Reuse the existing qualification/composition model and exact evidence identities; retain selection and staging prerequisites.

## Boundaries & Constraints

**Always:** Preserve all 24 vectors, predicates, historical packets and five REJECTED/open dispositions until adjudication. EventStore 3.21 owns assertions/evaluation; Platform owns configuration/execution; Builds owns runtime/tool alignment. Separate integrity, technical results and authority. Authenticated Architecture/Platform AD-26 records and the profile inventory govern selection.

**Never:** Infer authority from passing subsets, count timeouts as rejection, copy the harness/renderer, select Redis pub/sub, or claim staging from Docker. No new dispatch, private runtime, exception or cluster mutation decision is supplied. The previous attempt is consumed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior | Error Handling |
| --- | --- | --- | --- |
| Existing candidate | Verified candidate-02 packet | 20 PASS / 4 FAIL; five historical guarantees remain open | Technical FAIL; null selection |
| Configuration-only retry | Same admitted tuning surface | Identify unreachable failure mechanisms | Do not dispatch another unchanged experiment |
| New candidate | Accepted owner delivery and exact identities | Validate explicit lock/configuration and independent verification | Drift or missing evidence blocks admission |
| Missing hosted prerequisites | Unaccepted 4.14–4.16 or AD-26/profile inputs | Retain parent in-progress | No staging or production claim |

## Decisions — 2026-11-13

The user selected all three recommended routes:

- Preserve the Kafka pressure guarantee; investigate a genuinely rejecting pressure fault for EventStore 3.21. Report infeasibility or missing evidence rather than weakening the rule.
- Perform offline causal analysis of historical Kafka rotation failure versus candidate-02's verified PASS. No repeat or closure is authorized by that choice.
- Preserve RabbitMQ requirements; prepare an exact dependency intake and await supported component/runtime fixes. No exception is requested.

This accepted continuation produces an offline analysis and cross-owner intake. Full Story 4.17 selection, staging and production acceptance remain separate and in-progress.

</frozen-after-approval>

## Implementation Notes

- Route facts: all three intent choices resolved by the user's “recommended”; no irreversible effects; four documentation/tracking files plus the refreshed epic context, with no new callable interface. Follow the one-shot path for this bounded slice.
- Write `docs/implementation/story-4-17-next-candidate-intake.md`: compare retained rotation observations and exact source behavior, assess pressure-fault feasibility against primary Kafka sources, identify RabbitMQ supported-delivery requirements and explicit closure controls, and record exact evidence/source references and residual uncertainty. Do not turn unknown facts into new policy decisions.
- Reuse candidate-02's hosted report and linked assessment, the prior packet diagnosis and November 10 dispositions. EventStore tools `qualify-dapr-brokers.py`, `dapr_broker_qualification.py` and retained public identities define the accepted faults/predicates. Read them; do not edit the harness, locks or gitlinks.
- Reconcile the intake and verified candidate-02 outcome into `_bmad-output/planning-artifacts/epics.md` Story 4.17; append a continuation comment in `_bmad-output/implementation-artifacts/sprint-status.yaml`, preserving parent in-progress, five open REJECTED guarantees, null selection and production FAIL. Preserve concurrent changes.
- Existing Subscription rendering in `PlatformDaprBindings`, `PlatformHostedPublishing` and the retained chart needs no duplicate implementation. Live foundation/secrets acceptance and 4.16 remain missing; Story 4.9 owns the profile inventory.
- Validate retained assessment identities and source references; check local links, exact canonical revisions, changed-line whitespace and unchanged acceptance criteria/status values. No new behavioral tests for this documentation-only change. Complete the one-shot review and close only this bounded slice.
- Implemented the intake and parent planning/tracking reconciliation. Kafka source inspection found no demonstrated replacement fault satisfying the unchanged Produce rejection predicate. RabbitMQ's unreleased lower-level NACK fix leaves retry/cancellation and reconnect propagation requiring qualification.
- Recovered four existing artifact ZIPs into temporary storage, verified their identities and inspected entries before extraction. Equal rotation blocks/predicates and receipt/readback comparisons rule out a simple longer-wait explanation: historical failures waited about 18 s after the events deny, later passes about 12.5 s. Principal/authorizer causality remains unproved; candidate-01 stays diagnostic-only.
- Focused offline checks passed: assessment counts/statuses, original candidate JSON hash, four ZIP hashes, receipt indexes/timing/readback, equal archived rotation blocks and attribution predicates, local links and changed-file whitespace. No broker, lock, workflow, dependency, accepted predicate or historical assessment changed.
- The explicit accepted slice preserves parent 4.17 in-progress. This overrides the one-shot helper's generic final transition to review; only this slice spec can finish. Concurrent staged work and submodule edits belong to other sessions and are excluded from this change.
- Blind review completed with seven findings; all were checked and corrected, none deferred. Restored binding epic constraints from the current epics; clarified diagnostic admission versus qualification; retained sanitized source timestamps and an unambiguous `Run.scenario` hash recipe in the report.
- Final method-scoped verification corrected the initially ambiguous hash, which selected `Run.reconcile`. AST selection of `Run.scenario` confirms the 4,238-byte rotation branch is identical across all four archives at SHA-256 `4e021674fcc83ce239cc0974abd1bf4f085b1f736e58f717214a4029b3de6613`.
- Commit preflight: authoritative local commitlint 21.2.4 passed `./node_modules/.bin/commitlint --edit /tmp/platform417-rotation-research-ffwiny5v/commit-message.txt` with exit 0 for `docs(broker): prepare the next qualification intake`. Only the five owned files are eligible for this local commit; no push.

## Review Triage Log

| Finding | Verdict / route | Checked evidence and correction |
| --- | --- | --- |
| B1 external CAS and mutation fencing omitted | medium / patch | Current epics AR-20 and attempt-lock contract retain external storage and epoch checks. Restored both in the refreshed context; generic serialization was insufficient guidance for later implementation. |
| B2 environment-layer ordering/authority omitted | medium / patch | AR-23/32 require baseline/candidate union, separate forward-only attempt and no environment writes by application deploy identity. Restored those boundaries and explicit object tiers. |
| B3 executor allowlists/OIDC omitted | medium / patch | AR-28 and Story 4.20 retain job admission, credential issuance and host isolation. Restored controls rather than allowing future executor implementation to infer them. |
| B4 capture-lag dependency omitted | medium / patch | Story 4.9 fixes the bound consumed by staging identity export. Restored its dependency on Story 4.19 in the context. |
| B5 interface restriction/readiness deadline omitted | medium / patch | Hosted-interface rules and Story 4.22 retain declared public surfaces and ten-minute readiness. Restored both, including unsigned/failed-evidence refusal. |
| B6 diagnostic readiness contradicted | medium / patch | The report allowed expected failures but then made unresolved guarantees stop readiness generally. Distinguished supported-input diagnostic admission from remedy availability and qualification claims; no future run is authorized. |
| B7 comparison retention/hash recipe incomplete | medium / patch | Temporary artifacts can expire; the original hash selected `Run.reconcile`. Added durable sanitized source timestamps/ACL comparison commitment; AST-selected and rehashed `Run.scenario` with exact branch bounds and original bytes. Archive hashes remain separate from qualification authority. |
