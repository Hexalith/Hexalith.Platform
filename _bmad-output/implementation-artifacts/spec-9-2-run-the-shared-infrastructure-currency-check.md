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

**Preparation authorization (2026-11-16):** The user requested and then authorized preparation of the profile template, release-binding contract, content digests and separate Architecture/Platform deployment approval-record drafts. This authorizes review preparation; it records no technical selection or authenticated ratification.

**Draft design approval (2026-11-16):** The user said “I approve” after preparation and explanation of the owner-approval requirement. Approval of the presented draft design is recorded in `docs/contracts/ad26-preparation/design-approval.json`, bound to the preserved review index and exact draft bytes. Missing selections and technical proof are not attested; authenticated per-role ratification remains unissued.

**Mapping decision (2026-11-16, user: “apply recommended”):** Check a standalone provider, bundled constituent and management/workload pair before final format adoption. Adopt v1 only if those mappings are clean; otherwise prepare a focused owner v2 for the demonstrated gap. The containerd mapping exposes mandatory inapplicable namespace/configuration fields, so v1 adoption is withheld and the focused v2 construction spec is prepared. This selects the construction direction, not implementation approval, technical qualification or production authority.

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

## Pending prerequisite records

The user authorized preparing the missing records rather than locating existing ones. The review package is now `docs/contracts/ad26-preparation/README.md`, with proposed template/binding/target documents, two unissued role drafts and exact source/file digests. Conversation approval of the draft design is recorded against exact bytes; prescribed role authentication and actual technical qualification remain required. Broker/runtime selection, qualified mandatory append protection, accepted restore posture, real inventory/pairing and owner-role authentication/timing/assurance cannot be populated from the available evidence.

## Code Map

- `spec-9-1-decide-the-shared-infrastructure-currency-policy.md` in this directory and `docs/contracts/shared-infrastructure-currency-policy-v1.md` — completed policy implementation and verified published decisions; consume the exact policy text identity.
- `_bmad-output/planning-artifacts/epics.md:2305,3493,4489,4602` — 4.9 inventory, 6.14 admission, 8.17 drill and full 9.2 acceptance; all remain backlog.
- `docs/contracts/hosted-profile-binding-v1.md`, `docs/implementation/hosted-profile-v1-mapping-assessment.md` — assessed v1 proposal and demonstrated node-scope gap; conditional adoption choice is resolved, but v1 is not adopted. Preserve approved preparation bytes and qualification/issuance boundaries.
- `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-production-profile-v2-infrastructure-scope.md` — focused draft for the next owner implementation checkpoint; no v2 code or accepted inventory exists.
- `references/Hexalith.EventStore/docs/architecture/{production-profile-v1,publication-authority-v1}.md` and `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfile{Codec,Validator,ValidationContext,ValidationResult}.cs` in that owner — implemented construction contracts at `54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41`; reuse exact codec and external instance coverage. Both focused construction specs are done, parent 3.19 remains in-progress, and both canonical profile/coverage slots remain absent. Structural success leaves authorization/qualification unevaluated.
- `references/Hexalith.EventStore/tools/publication_authority.py` — authenticated profile build refuses `PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE`; Builds codec and fixed qualification/ratification verifiers remain missing. Do not remove those refusals to enable 9.2.
- `eng/cluster-management/qualify.py` — `Capture`, `project_resource`, `validate_pins`: identity/evidence patterns only; three-component scope and dated review rules do not implement 9.2.
- `eng/cluster-management/evidence.py` — immutable writes/digest patterns; existing attempt categories are separately scoped.
- `eng/staging-foundation/candidate.py`, `test_candidate.py` — stdlib Python, strict input/refusal and synthetic-test conventions; preserve existing preparation inputs.
- `docs/contracts/staging-evidence-policy-v1.md` — admission precedent, not an implemented promotion hook. No common authenticated production approval parser exists.

## Tasks & Acceptance

**Execution (prerequisites-first sequence):**
- [x] `docs/contracts/shared-infrastructure-currency-policy-v1.md` — complete Story 9.1 from its approved spec, with review and verification.
- [x] `docs/contracts/ad26-preparation/README.md` and its six companion files — prepare the user-requested review package, content-bound per-role drafts and source identities without issuing authority.
- [x] `_bmad-output/implementation-artifacts/spec-4-9-ratify-the-hosted-runtime-and-identity-event-contracts.md` — investigate and draft the inventory prerequisite. The user selected owner schema/validator preparation and a measured capture-lag proposal; full inventory acceptance remains outstanding.
- [x] `docs/contracts/hosted-profile-binding-v1.md` — prepare a concrete proposal against the now-completed owner construction implementation; no adoption or inventory acceptance recorded.
- [x] `docs/contracts/hosted-profile-binding-v1.md`, `docs/implementation/hosted-profile-v1-mapping-assessment.md` and the 4.9 readiness plan — apply the user's conditional mapping decision; document the three cases and withhold v1 adoption on the concrete scope gap.
- [ ] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-production-profile-v2-infrastructure-scope.md` — implement the approved focused v2 before final Platform contract adoption; retain all remaining qualified-input tasks.
- [ ] This spec — replan the live checker against the ratified inventory and actual admission/drill contracts; do not implement the unselected foundation proposal.

**Acceptance Criteria (original Story 9.2, still outstanding):**
- Given a production attempt or monthly drill, when the check runs, then every deployed shared component is compared with the supported, security-current inventory, including management/workload pairing, and exact identities and the result are recorded.
- Given a failed check, when promotion is evaluated, then automatic promotion is blocked with named components and approved attempts follow Story 9.1.

## Implementation Notes

- 2026-11-16 prerequisite progress: Story 9.1 policy implementation, verification and three-layer review are complete; its spec is done and sprint status is review per the build workflow. Story 9.2 remains backlog.
- Inventory investigation: Platform 4.9 explicitly depends on EventStore's ratified profile template and binding split. Pinned EventStore revision `df976e0051b9e2eaa9d3592b3054a0317d83d0cb` has 3.19 backlog and no `deploy/dapr/production-profile.yaml`. Its 3.19 acceptance requires the agreed template/binding contract and authenticated owner ratification; `deploy/dapr/qualification/README.md` explicitly prohibits substituting candidate files for that canonical profile. No inventory or replacement owner decision was fabricated.

- 2026-11-16: User selected prerequisites first. The initial foundation-only proposal is withdrawn; no checker code or live integration has been implemented. Story 9.2 remains backlog while this draft records the dependency order.

- 2026-11-16: Prepared the authorized AD-26 review package. Sources are fixed at Platform `2752ef60cf821accbab1937b4ce94d584da3ea6a` and EventStore `df976e0051b9e2eaa9d3592b3054a0317d83d0cb`; the independently extracted ratification subject is SHA-256 `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16` (10,000 canonical bytes). No canonical production profile, schema/validator authority, authentication, selection or ratification was fabricated.
- Preparation verification: six package-content hashes, eight committed-source hashes, thirteen local links/anchors, all JSON and whitespace checks pass. An independent source check recomputed the same subject and confirmed both role drafts remain unissued with unset authority fields. All disposable verification work ran under `run-with-temp`; requested draft outputs remain in the repository.

- Final source-consistency corrections: kept activated catalog generations/root digest environment-current and attempt-bound, retained the explicit Dapr 1.18.3 floor separately from the unset runtime selection, and carried the proposed unratified `oq8-postgresql-v1` target. Refreshed dependent record/index hashes; final narrow verification passes all counts above, and independent source checks confirm the corrections. No source-owner repository, runtime pin or sprint acceptance changed.

- 2026-11-16: Recorded the user’s “I approve” as the presented draft-design approval. Preserved `approved-review-index.json` and all six reviewed document bytes; added `design-approval.json` and refreshed only the current index. Both per-role records remain unissued and all technical, authentication, timing and assurance unknowns remain explicit.

- 2026-11-16 resumed prerequisite planning: created the Story 4.9 draft readiness plan named in Execution. EventStore 3.19 explicitly permits schema/validator construction before its issue-step prerequisites; qualification and authenticated issuance remain separate gates. Joint schema/encoding implementation and the finite capture-lag decision remain unresolved. No canonical profile, accepted inventory, live checker or admission/drill caller was created. Preparation consistency is recorded in `evidence/9-2/prerequisite-readiness-20261116.json`; both stories remain backlog and the approved draft bytes and frozen intent are preserved.

- 2026-11-16: User said “apply recommended” after the option analysis. Recorded the selected owner schema/validator construction path and capture-lag measurement proposal in the 4.9 frozen decisions; removed its resolved Open Questions. Prepared the focused EventStore 3.19 construction spec and measurement proposal. Retained this full history despite its size, as selected; no original intent or approval-bound package bytes changed. These preparation deliverables do not complete 4.9, 3.19 or 9.2.

- 2026-11-16: User said “approve and stop” at the concrete profile-validation specification checkpoint. Re-read the owner spec and marked it `ready-for-dev`; its reviewed body is unchanged. Approval covers this construction spec only, not AD-26 ratification, qualified inventory, publication authority or completion of 3.19/4.9/9.2. Resume implementation from `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-production-profile-validation.md`; no implementation was started.

- 2026-11-16 resumed 9.2 investigation at Platform `575aa1889b0dc654b36de2e6958878c5eadbc871` and EventStore `54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41`: both owner construction specs are now done. That supersedes the earlier next-action note, without changing its historical approval. Parent 3.19 remains in-progress; no canonical profile, production composing coverage, Platform hosted inventory, currency evaluator or production/monthly/weekly caller exists. Stories 4.9/6.14/8.17/9.2 remain backlog.
- Prepared the concrete owner-v1 binding proposal and its adoption question. Intent gaps: final Platform schema/encoding adoption needs the user's choice; actual qualified inputs and caller implementations remain engineering dependencies, not facts an approval can supply. Irreversibles: none in this preparation. Footprint: proposed contract, this draft, 4.9 progress notes and a retained readiness observation; no executable/public API, pin, approved-package or production mutation.
- Retain the full history under the user's earlier “Keep” decision. This draft remains unready; approving a construction format cannot approve full 9.2 implementation against absent prerequisites. Revisions and source hashes are retained in `evidence/9-2/current-prerequisite-readiness-20261116.json`.

- 2026-11-16: Applied “apply recommended” to the mapping-first path, resolving the adoption Open Question and recording only that authorized decision in the frozen block. Independent source inspection confirms a narrow v1 representation gap for non-namespaced shared instances; standalone provider and pair identities fit. Prepared the owner v2 construction spec without implementing it, adopting a final format, changing owner code or claiming qualification. Full prerequisite acceptance and the original 9.2 criteria remain outstanding.

- 2026-11-16: User said “approve and stop” at the focused v2 checkpoint. Re-read the owner construction spec and changed only its status to `ready-for-dev`; the reviewed body is unchanged. Approval covers that implementation spec only. No implementation started, final format adopted, inventory qualified or production authority issued. Resume from `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-19-production-profile-v2-infrastructure-scope.md`; this parent remains draft/backlog.

## Spec Change Log

## Review Triage Log

## Verification

- Follow each prerequisite's approved verification plan. No local fixture result satisfies the original Story 9.2 criteria.
- `git diff --check` — no whitespace errors.
