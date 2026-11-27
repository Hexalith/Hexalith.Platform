# Sprint change proposal for production currency readiness

**Date** 2026-11-27 · **Scope** Moderate · **Approach** Direct adjustment within the existing stories

## 1. Issue summary

| Severity | Readiness finding | Evidence |
| --- | --- | --- |
| High | The 9.2 draft waited for the 8.17 drill caller. The drill needs working production from 6.23, and the first release needs the 9.2 check at admission. | [Policy v1 decision 2](../../docs/contracts/shared-infrastructure-currency-policy-v1.md#2-cadence-and-triggers) · [9.2 draft](../implementation-artifacts/spec-9-2-run-the-shared-infrastructure-currency-check.md) and its prior wording at `f87f1593` · [Story 6.23](epics.md#story-623-deploy-the-reference-release-to-production-with-ingress-closed-and-reach-g1) |
| Medium | The stop and approval criteria described lost-window reviews without stating when the report could be absent. | [AR-44](epics.md#additional-requirements) · [Story 6.10](epics.md#story-610-keep-a-durable-per-cause-promotion-stop) · [Story 6.18](epics.md#story-618-run-administrator-approved-attempts-into-empty-or-degraded-production) · [Story 9.5](epics.md#story-95-rehearse-the-promotion-stop-and-approval-rules-sm-5-part-2) |
| Low | The 9.2 notification criterion began with a failed check. It did not exercise the passing weekly result that still requires an issue. | [Policy v1 notification rule](../../docs/contracts/shared-infrastructure-currency-policy-v1.md#2-cadence-and-triggers) · [Story 9.2](epics.md#story-92-run-the-shared-infrastructure-currency-check) |

## 2. Impact analysis

- Epic 6 owns the first-release admission integration and the no-recovery branch for stop clearance and approval. Epic 8 owns the drill integration. Epic 9 owns the shared currency contract, weekly schedule and full G3 qualification. Story 4.9 still supplies the authoritative inventory and qualified pairing. Epic 10's full-composition releases use the same gate.
- FR-8 and FR-10 in the PRD, the architecture spine's production preconditions and Owned work, and the Platform spec describe the same approval, check and delivery order. Policy v1 and AR-44 supply the requirements. The weekly notification uses the existing private GitHub issue channel, so the implementation needs a scheduled caller and delivery evidence within the existing operations design.
- Implementation spans the evaluator and evidence record, locked production admission, drill runner and scheduler. The repository assigns Platform no standalone UI for these paths. [UX coverage in the epic plan](epics.md#ux-design-requirements)

## 3. Delivery sequence

| When | Owner and exit condition |
| --- | --- |
| Before first production release | Platform and Administrator deliver the Story 9.2 evaluator and release-admission integration in 6.14. The 4.9 ratified inventory and pairing, live shared components from 6.6, deployed production-layer providers from 6.7 and lock and epoch from 6.9 are available. Qualify the admission path before 6.23. |
| First production release | Story 6.23 takes the Administrator-approved path. Admission evaluates current inputs before mutation and retains a result for the attempt ID, mode and epoch. A failed result permits approved admission only when the complete failing set is known, the Administrator record names it and all other preconditions hold. Missing, stale, mismatched or unretained results refuse admission. |
| Once production is working | Platform activates the weekly schedule against working production established in 6.23, using 6.4's private issue channel. The schedule runs independently of release attempts and drills. |
| First and monthly drills | Story 8.17 reuses the evaluator after 6.23 and the drill's recovery prerequisites. The check is bound to the deployment and inventory checked in the drill, and the result and reasons are retained with the drill evidence. |
| Before G3 | Stories 9.2 and 9.6 qualify the three triggers, mode-specific admission behavior, evidence and notifications. Story 9.8 requires that qualification and the applicable SM-5 evidence. |

This moves evaluator and admission work onto the G1 critical path. Implementation effort and integration risk are moderate because live inventory, authorization and result retention must be available together. Weekly and drill integration can proceed in parallel after working production. The adjustment fits the existing MVP and story IDs. Rollback has no delivered evaluator to remove, and a scope reduction would leave a policy obligation unmet.

## 4. Changes recorded in the plan

| Artifact | Before | After and rationale |
| --- | --- | --- |
| 6.14 and 6.23 · admission preconditions | The admission story listed its general preconditions and the first release relied on infrastructure currency being in place. | Deliver the 9.2 evaluator with 6.14 and require its current locked result explicitly at 6.23. The prepared production layer supplies the observed provider identities before application deployment. |
| 8.17 · drill acceptance | The drill listed its recovery record and recurrence. | Run the evaluator for the first and monthly drills and retain the bound result and reasons. A currency result adds no attempt block and sets no promotion stop. |
| 9.2 · draft delivery and acceptance | “Design the checker only once” the inventory and both callers exist. One criterion grouped weekly notice with drill or weekly failure. | Deliver admission first with 6.14. Reuse the evaluator in 8.17 and start weekly checks at working production. Add a separate passing-weekly criterion for a supported component with known upstream end of life more than zero and at most 2,592,000 seconds after the evaluation. Keep the pass, notify Administrator in a private issue mentioning the deputy, and record notification reason, outcome and issue reference when created. |
| 6.10 and 6.18 · clearance and approval acceptance | Clearance and approval referred to lost-window review without a no-recovery branch. | Confirm live admission matches its signed records and require review of every applicable report. Record the review as not applicable only when no recovery capable of producing a lost window occurred. A missing required report refuses clearance or approval. |
| 9.5 · rehearsal acceptance | The rehearsal required the reviews without exercising report absence. | Rehearse otherwise eligible clearance and approval without a report when no recovery could have produced one. Also exercise refusal when recovery could produce a report and it is missing or the review is unrecorded. |
| 9.8, PRD, spine and Platform spec · gates and timing | The G3 text grouped enforcement there and the approval criteria left the report exception implicit. | Require admission enforcement before the first release and full qualification before G3. Record the weekly and drill milestones and carry the AR-44 rule into the production acceptance sources. |
| Sprint tracker and Epic 9 context · handoff | The tracker listed 9.2 in Epic 9 and its context summarized the dependency on the inventory. | Record that 6.14 delivers admission with 9.2 before 6.23, and document the later weekly and drill timings. This uses the existing story keys and statuses. |

## 5. Implementation handoff

- **Product Owner and Platform Developer** carry the 9.2 admission work with 6.14 in the backlog. Verify the sequencing against the Story 4.9 inventory and Stories 6.6, 6.7 and 6.9 before qualifying admission.
- **Platform Developer, Builds and Administrator** demonstrate a first approved-release admission with its own result and positive and negative currency dispositions under policy v1. Rehearse both release modes, refusal of an invalid result or an unnamed failing component, and preservation of the existing stop and revision.
- **Platform Developer and Administrator** retain the first and monthly drill records. Demonstrate weekly failure delivery, a passing check exactly 30 days from end of life, and a passing check one second outside the notification window. Retain delivery evidence and include all three triggers in G3 qualification.
- **Administrator and Platform Developer** rehearse both AR-44 branches in 9.5. Verify the admission-match and stop-revision checks, continuation of an otherwise eligible clearance or approval without a report when no qualifying recovery occurred, and refusal when required recovery evidence is missing.

Planning edits are recorded. Live qualification and deployment remain delivery work in the existing backlog.

## Hosted-profile v2 delivery order

The November 27 architecture update supersedes the delivery-order notes at the end of the adopted hosted-profile v2 authority boundary and in the adoption record's remaining requirements. Story 6.14 delivers Story 9.2's evaluator and signed evidence contract before Story 6.23. Once 6.23 establishes working production, the weekly check starts and Story 8.17 integrates the drill caller when its recovery inputs are ready. The adopted v2 format, exact adoption bytes, independent coverage, qualification, ratification and publication requirements continue to govern Story 4.9. [Adopted profile companion](../../docs/contracts/hosted-profile-binding-v2.md#independent-coverage-and-authority-boundary) · [Adoption record](../../docs/contracts/hosted-profile-v2-adoption.json)

The [architecture validation](architecture/architecture-platform-2026-09-27/reviews/validate-2026-11-27/validation-report.md) also binds signed final admission evidence, the pre-admission refusal disposition, distinct v2 profile digests, both renewed production baseline records, the drill observation boundary and AR-44 recovery history. Stories 4.9 and 4.19 split event-export measurement from bounded staging acceptance. Story 4.9 qualifies the executor Helm pin separately. Stories 9.6 and 9.8 qualify the adopted currency cases and audit the exact final admission result for each automatic rollout. [Architecture spine](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#currency-evidence-authority)
