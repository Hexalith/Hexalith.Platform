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
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-27.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 9.2 must check deployed shared infrastructure against policy v1 at admission of every production release attempt, at every monthly drill and on a weekly schedule. The repository has no currency evaluator, no ratified hosted inventory and no admission, drill or weekly caller.

**Approach:** Deliver each trigger with its caller. Implement the evaluator, result record and production admission integration in Story 6.14 after Story 4.9 ratifies the inventory and pairing and Stories 6.6, 6.7 and 6.9 supply the deployed inputs and production lock. Qualify the admission path before the first release in Story 6.23, using the infrastructure and environment-layer providers already in place. Story 8.17 adds the first and monthly drill calls after 6.23 establishes working production and recovery inputs are available. Start independent weekly checks against active production as soon as 6.23 establishes it, with notifications through Story 6.4. Implement policy v1 as frozen at Platform `8c79c18874a3a767d60be63c2ce4020a73991f96` (SHA-256 `c75a8b0b85ee1aa73615e5906dc0d3aa8a5601743fefd9de4b6eb1d3b817a4a9`).

**Decisions:**
- 2026-11-16: prerequisites first. The standalone checker foundation was not selected.
- 2026-11-16 ("adopt v2"): the Story 4.9 inventory uses `docs/contracts/hosted-profile-binding-v2.md` with EventStore's v2 schema and codec, as recorded in `docs/contracts/hosted-profile-v2-adoption.json`. v1 was not adopted because of its node-scope gap. This selects the format only.
- 2026-11-26 (Story 9.1 decision, applied here): `production-attempt` means admission of a production release promotion, automatic or Administrator-approved. Rollbacks, configuration-only changes, rotations, catalog activations outside a release, and infrastructure- or recovery-kind attempts are outside the trigger. Monthly-drill and weekly results are recorded but add no attempt block and set no promotion stop.
- 2026-11-26: no prerequisite had moved. The user chose to record the blockers and stop, and to cut this draft down to decisions and current state.
- 2026-11-27: the readiness gate found that waiting for the Story 8.17 caller would block the first release. Story 6.14 delivers the admission integration with the evaluator. Story 8.17 consumes it after the first release. Weekly operation starts independently once the production deployment is active. Each integration still requires its authoritative inputs.

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
| First release admission | Empty production with live infrastructure and the production layer applied | Story 6.14 runs and retains the current release-admission check under the production lock | Refuse a missing, unretained, stale or mismatched result. On failure, refuse if the complete failing set is unknown or the Administrator record omits a failing component |
| Drill or weekly failure | Failed monthly-drill or weekly result | Recorded with reasons; weekly notifies Administrator | No attempt block, no stop |
| Passing weekly check near end of life | A supported component has a known upstream end-of-life instant more than zero and at most 2,592,000 seconds after `checkedAt` | Keep the pass and open a private issue assigned to Administrator and mentioning the deputy | Record notification reason, delivery outcome and issue reference when created |
| Passing weekly check outside the notice window | Nearest known end of life is 2,592,001 seconds away, or the authoritative sources confirm support without an announced end-of-life date | Keep the pass and record that no notice is due | Retain the upstream support evidence |
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
- [ ] With Story 6.14, deliver the evaluator and result record and qualify production release admission after Stories 4.9, 6.6, 6.7 and 6.9 supply the inputs. This work gates the first release in 6.23.
- [ ] With Story 8.17, reuse the evaluator and retain the bound check result and reasons with the first drill and every monthly drill.
- [ ] With Story 6.4's private issue channel available, prepare the weekly schedule and notification path to start when 6.23 establishes active production. Qualify all triggers and the Story 9.6 rehearsals before G3.

**Acceptance Criteria (Story 9.2, worded per policy v1):**
- Given admission of a production release attempt, a monthly drill or the weekly schedule, when the check runs, then it compares each deployed shared component (including Rancher, required agents, the management-cluster distribution and the backup operator) and each environment-layer provider with the supported, security-current inventory, checks the qualified management/workload pairing, and records exact deployed identities and the result.
- Given either release mode, including the first approved installation, when Story 6.14 evaluates admission, then the check runs before mutation at the admission's UTC evaluation time and retains a fresh result bound to the attempt ID, release mode and current lock epoch. Missing, stale, mismatched or unretained results refuse admission.
- Given a failed check at release admission, when admission evaluates the result, then automatic promotion is blocked and the failing components are named. An approved attempt meets the currency rule only if the complete failing set is known and its Administrator record names every failing component. The check sets no promotion stop.
- Given a failed monthly-drill or weekly check, when the result is recorded, then it adds no attempt block and sets no promotion stop. A weekly failure notifies Administrator through the private GitHub issue channel. The first and monthly drill results and reasons are retained with their drill evidence, bound to the checked deployment and inventory.
- Given a passing weekly check and a supported component whose known upstream end-of-life instant is after `checkedAt` and at most 2,592,000 seconds away, when the result is recorded, then it remains passing and the issues-only credential opens a private issue assigned to Administrator and mentioning the recovery deputy. Record the notification reason, delivery outcome and issue reference when created.

## November 27 architecture handoff

The [hosted-profile delivery order](../planning-artifacts/sprint-change-proposal-2026-11-27.md#hosted-profile-v2-delivery-order) supersedes the historical caller-order note while retaining owner v2 profile adoption and all authority gates. The [spine's currency evidence authority](../planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#currency-evidence-authority) binds the observe, sign, retain and verify boundary. At admission, retain the exact final check reference and applicable approval disposition in signed attempt evidence. Story 6.9 owns terminal pre-admission refusal and lock release. The drill control job checks the actual isolated drill deployment, with substitutions and missing observations recorded. Qualify all three signing scopes and the exact-result G3 audit in their owning stories.

## Implementation Notes

- 2026-11-26 re-investigation at Platform `89301183f086486108faf1c1bd8c90daaaa5c39f` (EventStore gitlink `295b5a6ba584167f21cfef40787477b8814e32da`) found nothing buildable:
  - **Inventory:** Story 4.9 is backlog. Its four owner gaps from 2026-11-24 are still open: hosted symmetric JWT signing keys, missing `DAPR_API_TOKEN` declarations, per-secret facts, and the Development-only catalog/idempotency binder. Its inputs wait on 4.14, 4.15, 4.17, 4.18 and 4.28. EventStore 3.19 is in progress, with no canonical profile.
  - **Callers:** 6.14 (admission), 6.9 (production lock) and 8.17 (drill) are backlog. Epics 6 and 8 have not started.
  - **Weekly check:** there is no production deployment, and the 6.4 GitHub notification channel is backlog.
- The full 2026-11-16/17 history is in git at `0259a9c4435f4ae37cef29437ab58e16f7f1b559` and in `evidence/9-2/`. The [November 26 proposal](../planning-artifacts/sprint-change-proposal-2026-11-26.md) resolved the trigger-wording deferral.

## Spec Change Log

- 2026-11-27: Sequenced the evaluator and admission integration before the first release, assigned the drill call to Story 8.17 and made the passing weekly notification case explicit.

## Review Triage Log

## Verification

**Commands:**
- `git diff --check`: expected no whitespace errors.
