---
title: '9.2 Run the shared-infrastructure currency check'
type: 'feature'
created: '2026-11-16'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '9-2-run-the-shared-infrastructure-currency-check'
context:
  - '{project-root}/docs/contracts/shared-infrastructure-currency-policy-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 9.2 must check deployed shared infrastructure against policy v1 at admission of every production release attempt, at every monthly drill and on a weekly schedule. The repository has no currency evaluator, no ratified hosted inventory and no admission, drill or weekly caller.

**Approach:** Prerequisites first. Design the checker only once Story 4.9 ratifies the inventory and Stories 6.14 and 8.17 supply the admission and drill calls. Implement policy v1 as frozen at Platform `8c79c18874a3a767d60be63c2ce4020a73991f96` (SHA-256 `c75a8b0b85ee1aa73615e5906dc0d3aa8a5601743fefd9de4b6eb1d3b817a4a9`).

**Decisions:**
- 2026-11-16: prerequisites first. The standalone checker foundation was not selected.
- 2026-11-16 ("adopt v2"): the Story 4.9 inventory uses `docs/contracts/hosted-profile-binding-v2.md` with EventStore's v2 schema and codec, as recorded in `docs/contracts/hosted-profile-v2-adoption.json`. v1 was not adopted because of its node-scope gap. This selects the format only.
- 2026-11-26 (Story 9.1 decision, applied here): `production-attempt` means admission of a production release promotion, automatic or Administrator-approved. Rollbacks, configuration-only changes, rotations, catalog activations outside a release, and infrastructure- or recovery-kind attempts are outside the trigger. Monthly-drill and weekly results are recorded but add no attempt block and set no promotion stop.
- 2026-11-26: no prerequisite had moved. The user chose to record the blockers and stop, and to cut this draft down to decisions and current state.

## Boundaries & Constraints

**Always:** Follow policy v1 exactly. Cover every versioned shared-facet component and production's environment-layer providers (OpenBao, PostgreSQL, FalkorDB, Redis). Exclude configuration-only objects and the deploy toolchain. Require each deployed identity to equal its pin, every version to resolve, upstream support, and the qualified management/workload pairing. Support lag is zero. A missing advisory fix fails from `fixReleasedAt` + 2,592,000 seconds. Missing, duplicate, contradictory or incomplete evidence fails closed and names the affected components. Retain every result with the fields from policy v1's "Required result record". Automatic eligibility needs every comparison to pass. An approved release attempt needs an Administrator record that names every failing component. Keep all other admission preconditions. No input file, fixture, timestamp or digest authenticates evidence by itself.

**Never:** Install infrastructure, change pins, grant credentials or authorize production. Create or change a promotion stop. Treat historical observations as current, invent inventory entries, or trust local self-assertions as live authority. Replace 4.9, 6.14 or 8.17, enable G3, or mark the story done on local tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Current | Matching identities, support, fixes and qualified pair | Comparison passes; admission is not authorized by this alone | Record inputs and result |
| Drift or unknown | Changed pin, unresolved version, unavailable source or missing component | Named comparison failures | No cached fallback |
| Deadline | Unfixed advisory at release + 30 days, or support ended | Component fails from the boundary | Keep advisory and fix identity |
| Pairing | Unqualified management/workload combination | Pair fails, naming both components | Not eligible |
| Approval coverage | Failed release check with all or only some failing IDs named | Coverage passes or fails | Never accept an invented approval format |
| Drill or weekly failure | Failed monthly-drill or weekly result | Recorded with reasons; weekly notifies Administrator | No attempt block, no stop |
| Malformed | Duplicate IDs, wrong digest, future observations or invalid timestamps | Structured refusal | Nonzero exit; no partial pass |

</frozen-after-approval>

## Code Map

- `docs/contracts/shared-infrastructure-currency-policy-v1.md`: the policy to implement, including its result record and acceptance/refusal table.
- `docs/contracts/hosted-profile-binding-v2.md`, `hosted-profile-v2-adoption.json`: the adopted inventory format. Each instance carries its support/advisory sources and currency/renewal owners. The management/workload/Rancher pair is a declared topology tuple. These files define representation only; qualification is separate.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileV2Codec.cs` and validator: the only codec to use. The canonical slot `deploy/dapr/production-profile.yaml` is absent, and EventStore 3.19 is in progress.
- `references/Hexalith.EventStore/tools/publication_authority.py`: keeps the `PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE` refusal and the missing Builds `ReleaseEvidenceCodec` refusal. Do not remove them to enable 9.2.
- `_bmad-output/implementation-artifacts/spec-4-9-ratify-the-hosted-runtime-and-identity-event-contracts.md`: the inventory prerequisite, a draft with its 2026-11-24 blockers.
- Reusable patterns only: `eng/cluster-management/qualify.py` (`Capture`, `validate_pins`) and `evidence.py` for identities and digests; `eng/staging-foundation/candidate.py` and its tests for stdlib strict-input and refusal conventions. No production approval parser exists yet.

## Tasks & Acceptance

**Execution:**
- [x] Prerequisite engineering (2026-11-16/17): Story 9.1 policy, AD-26 preparation package (`docs/contracts/ad26-preparation/`), v2 format adoption, and EventStore 3.19 v2 construction, bridge isolation and SDK/import proof (published as EventStore `96363934c6fd1da4d50c2078b13929c473ecab7f`).
- [ ] This spec: replan the checker once the 4.9 inventory, 6.14 admission and 8.17 drill exist. Do not build the unselected standalone foundation.

**Acceptance Criteria (Story 9.2, worded per policy v1):**
- Given admission of a production release attempt, a monthly drill or the weekly schedule, when the check runs, then it compares each deployed shared component (including Rancher, required agents, the management-cluster distribution and the backup operator) and each environment-layer provider with the supported, security-current inventory, checks the qualified management/workload pairing, and records exact deployed identities and the result.
- Given a failed check at release admission, when automatic promotion is evaluated, then the automatic path is blocked and the out-of-date components are named, and an approved attempt proceeds only if its Administrator record names every failing component.
- Given a failed monthly-drill or weekly check, when the result is recorded, then it adds no attempt block and sets no promotion stop, and a weekly failure, or an end of life within 30 days, notifies Administrator.

## Implementation Notes

- 2026-11-26 re-investigation at Platform `89301183f086486108faf1c1bd8c90daaaa5c39f` (EventStore gitlink `295b5a6ba584167f21cfef40787477b8814e32da`) found nothing buildable:
  - **Inventory:** Story 4.9 is backlog. Its four owner gaps from 2026-11-24 are still open: hosted symmetric JWT signing keys, missing `DAPR_API_TOKEN` declarations, per-secret facts, and the Development-only catalog/idempotency binder. Its inputs wait on 4.14, 4.15, 4.17, 4.18 and 4.28. EventStore 3.19 is in progress, with no canonical profile.
  - **Callers:** 6.14 (admission), 6.9 (production lock) and 8.17 (drill) are backlog. Epics 6 and 8 have not started.
  - **Weekly check:** there is no production deployment, and the 6.4 GitHub notification channel is backlog.
- The user chose to record these blockers and stop, and to cut this draft down. The full 2026-11-16/17 history is in git at `0259a9c4435f4ae37cef29437ab58e16f7f1b559` and in `evidence/9-2/`. The planning-source half of the trigger-wording deferral (PRD, spine, epics, sequencing) stays in `deferred-work.md` for a correct-course.

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `git diff --check`: expected no whitespace errors.
