---
title: '9.2 Run the shared-infrastructure currency check'
type: 'feature'
created: '2026-11-16'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '9-2-run-the-shared-infrastructure-currency-check'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-9-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 9.2 requires exact deployed infrastructure identities, support/security evaluation and qualified management/workload pairing at every production attempt and monthly drill. The repository has no currency evaluator, accepted hosted inventory or admission/drill caller.

**Approach:** Complete the prerequisites before designing the live checker. Preserve the original Story 9.2 production-attempt and monthly-drill acceptance criteria.

**Decision (2026-11-16):** The user chose prerequisites first: resume approved Story 9.1, then establish Story 4.9's ratified inventory before returning to 9.2. The proposed standalone checker foundation is not selected.

## Boundaries & Constraints

**Always:** Carry forward 9.1's recorded decisions: check every versioned shared component and production's environment providers; exclude configuration-only objects and deploy tools. Require exact deployed identity/pin agreement, resolved versions, upstream support and qualified management/workload pairing. Support ends at its upstream end-of-life instant. A missing advisory fix fails from its upstream release plus 30 days, using the deployed line or earliest supported line carrying the fix; ordinary updates have no lag limit while supported. Missing, duplicate, contradictory or incomplete evidence fails closed and names affected components.

Record check/trigger identity, UTC evaluation time, inventory digest, each deployed identity/version, pin, support/advisory sources and observation times, pairing and all failure reasons. Accept production-attempt, monthly-drill and weekly triggers. Weekly results also identify end of life within 30 days for future notification.

Automatic eligibility requires every comparison to pass. Approved attempts require an authenticated Administrator record naming every failing component. Preserve all existing admission preconditions. No input file, fixture, timestamp or digest authenticates evidence by itself.

**Never:** Use this pending draft to install infrastructure, change pins, grant credentials or authorize production. The currency check itself must not mutate a promotion stop. Treat historical observations as current, invent inventory entries or trust local self-assertions as live authority. Replace 4.9, 6.14 or 8.17, enable G3, or mark the full story done on local tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Current | Complete matching identities, support, fixes and qualified pair | Comparison passes; admission remains unauthorized | Record inputs and result |
| Drift or unknown | Changed pin, unresolved version, unavailable source or missing component | Named comparison failures | No cached fallback |
| Deadline | Unfixed advisory at release + 30 days, or support ended | Component fails from the boundary | Preserve advisory/fix identity |
| Pairing | Management/workload combination lacks qualification | Pair fails, naming both components | Refuse eligibility |
| Approval coverage | Failed check with all/some failed IDs named | Coverage passes/fails respectively | Never verify an invented approval format |
| Malformed | Duplicate IDs, wrong digest, future observations or invalid timestamps | Structured refusal | Nonzero exit; no partial pass |

</frozen-after-approval>

## Open Questions

1. Supply the ratified EventStore AD-26 template and agreed per-release binding contract (or an approved replacement), with its exact record/artifact reference. The pinned EventStore checkout has no canonical production profile; its 3.19 remains backlog. Without that owner input, Platform cannot ratify 4.9's actual inventory or safely design the live 9.2 input contract.

## Code Map

- `spec-9-1-decide-the-shared-infrastructure-currency-policy.md` in this directory and `docs/contracts/shared-infrastructure-currency-policy-v1.md` — completed policy implementation and verified published decisions; consume the exact policy text identity.
- `_bmad-output/planning-artifacts/epics.md:2305,3493,4489,4602` — 4.9 inventory, 6.14 admission, 8.17 drill and full 9.2 acceptance; all remain backlog.
- `eng/cluster-management/qualify.py` — `Capture`, `project_resource`, `validate_pins`: identity/evidence patterns only; three-component scope and dated review rules do not implement 9.2.
- `eng/cluster-management/evidence.py` — immutable writes/digest patterns; existing attempt categories are separately scoped.
- `eng/staging-foundation/candidate.py`, `test_candidate.py` — stdlib Python, strict input/refusal and synthetic-test conventions; preserve existing preparation inputs.
- `docs/contracts/staging-evidence-policy-v1.md` — admission precedent, not an implemented promotion hook. No common authenticated production approval parser exists.

## Tasks & Acceptance

**Execution (prerequisites-first sequence):**
- [x] `docs/contracts/shared-infrastructure-currency-policy-v1.md` — complete Story 9.1 from its approved spec, with review and verification.
- [ ] `_bmad-output/implementation-artifacts/spec-4-9-ratify-the-hosted-runtime-and-identity-event-contracts.md` — investigate and plan the separately owned inventory prerequisite through the build workflow before implementation.
- [ ] This spec — replan the live checker against the ratified inventory and actual admission/drill contracts; do not implement the unselected foundation proposal.

**Acceptance Criteria (original Story 9.2, still outstanding):**
- Given a production attempt or monthly drill, when the check runs, then every deployed shared component is compared with the supported, security-current inventory, including management/workload pairing, and exact identities and the result are recorded.
- Given a failed check, when promotion is evaluated, then automatic promotion is blocked with named components and approved attempts follow Story 9.1.

## Implementation Notes

- 2026-11-16 prerequisite progress: Story 9.1 policy implementation, verification and three-layer review are complete; its spec is done and sprint status is review per the build workflow. Story 9.2 remains backlog.
- Inventory investigation: Platform 4.9 explicitly depends on EventStore's ratified profile template and binding split. Pinned EventStore revision `df976e0051b9e2eaa9d3592b3054a0317d83d0cb` has 3.19 backlog and no `deploy/dapr/production-profile.yaml`. Its 3.19 acceptance requires the agreed template/binding contract and authenticated owner ratification; `deploy/dapr/qualification/README.md` explicitly prohibits substituting candidate files for that canonical profile. No inventory or replacement owner decision was fabricated.

- 2026-11-16: User selected prerequisites first. The initial foundation-only proposal is withdrawn; no checker code or live integration has been implemented. Story 9.2 remains backlog while this draft records the dependency order.

## Spec Change Log

## Review Triage Log

## Verification

- Follow each prerequisite's approved verification plan. No local fixture result satisfies the original Story 9.2 criteria.
- `git diff --check` — no whitespace errors.
